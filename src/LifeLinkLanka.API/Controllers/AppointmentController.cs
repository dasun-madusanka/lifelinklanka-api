using LifeLinkLanka.Application.DTOs.Appointment;
using LifeLinkLanka.Domain.Constants;
using LifeLinkLanka.Domain.Entities;
using LifeLinkLanka.Domain.Enums;
using LifeLinkLanka.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using LifeLinkLanka.API.Extensions;

namespace LifeLinkLanka.API.Controllers;

[ApiController]
[Route("api/v1/appointments")]
[Authorize]
public class AppointmentController : ControllerBase
{
    private readonly ApplicationDbContext _db;

    public AppointmentController(ApplicationDbContext db) => _db = db;

    private Guid CurrentUserId => User.GetUserId();

    [HttpGet("my")]
    public async Task<IActionResult> GetMyAppointments()
    {
        var appts = await _db.DonationAppointments
            .Include(a => a.BloodBank)
            .Include(a => a.BloodCamp)
            .Include(a => a.User).ThenInclude(u => u.DonorProfile)
            .Where(a => a.DonorUserId == CurrentUserId)
            .OrderByDescending(a => a.ScheduledSlotUtc)
            .Select(a => new AppointmentDto(
                a.Id,
                a.DonorUserId,
                a.User.FullName,
                a.User.Email!,
                a.User.PhoneNumber ?? "N/A",
                a.User.DonorProfile != null ? a.User.DonorProfile.BloodType : BloodType.OPositive,
                a.BloodBankId,
                a.BloodBank != null ? a.BloodBank.Name : null,
                a.BloodCampId,
                a.BloodCamp != null ? a.BloodCamp.Title : null,
                a.ScheduledSlotUtc,
                a.Status,
                a.PreScreeningPassed,
                a.Notes,
                a.CreatedAtUtc
            ))
            .ToListAsync();

        return Ok(appts);
    }

    [HttpPost]
    public async Task<IActionResult> BookAppointment([FromBody] BookAppointmentDto dto)
    {
        if (dto.ScheduledSlotUtc < DateTime.UtcNow)
            return BadRequest("Appointment slot must be in the future.");

        var appt = new DonationAppointment
        {
            DonorUserId = CurrentUserId,
            BloodBankId = dto.BloodBankId,
            BloodCampId = dto.BloodCampId,
            ScheduledSlotUtc = dto.ScheduledSlotUtc,
            PreScreeningPassed = dto.PreScreeningPassed,
            PreScreeningAnswersJson = dto.PreScreeningAnswersJson,
            Notes = dto.Notes,
            Status = AppointmentStatus.Scheduled
        };

        _db.DonationAppointments.Add(appt);
        await _db.SaveChangesAsync();

        return Ok(new
        {
            message = "Donation appointment confirmed successfully.",
            appointmentId = appt.Id,
            slot = appt.ScheduledSlotUtc
        });
    }

    [HttpGet]
    [Authorize(Roles = $"{Roles.BloodBank},{Roles.Admin}")]
    public async Task<IActionResult> GetAllAppointments([FromQuery] Guid? bloodBankId)
    {
        var query = _db.DonationAppointments
            .Include(a => a.BloodBank)
            .Include(a => a.BloodCamp)
            .Include(a => a.User).ThenInclude(u => u.DonorProfile)
            .AsQueryable();

        if (bloodBankId.HasValue)
            query = query.Where(a => a.BloodBankId == bloodBankId.Value);

        var appts = await query
            .OrderByDescending(a => a.ScheduledSlotUtc)
            .Select(a => new AppointmentDto(
                a.Id,
                a.DonorUserId,
                a.User.FullName,
                a.User.Email!,
                a.User.PhoneNumber ?? "N/A",
                a.User.DonorProfile != null ? a.User.DonorProfile.BloodType : BloodType.OPositive,
                a.BloodBankId,
                a.BloodBank != null ? a.BloodBank.Name : null,
                a.BloodCampId,
                a.BloodCamp != null ? a.BloodCamp.Title : null,
                a.ScheduledSlotUtc,
                a.Status,
                a.PreScreeningPassed,
                a.Notes,
                a.CreatedAtUtc
            ))
            .ToListAsync();

        return Ok(appts);
    }

    [HttpPut("{id:guid}/cancel")]
    [Authorize]
    public async Task<IActionResult> CancelAppointment(Guid id)
    {
        var appt = await _db.DonationAppointments.FirstOrDefaultAsync(a => a.Id == id);
        if (appt is null) return NotFound("Appointment not found.");

        var userId = CurrentUserId;
        var isAdmin = User.IsInRole(Roles.Admin);
        var isBloodBank = User.IsInRole(Roles.BloodBank);
        if (appt.DonorUserId != userId && !isAdmin && !isBloodBank)
            return StatusCode(403, "You do not have permission to cancel this appointment.");

        appt.Status = AppointmentStatus.Cancelled;
        await _db.SaveChangesAsync();

        return Ok(new { message = "Appointment cancelled." });
    }

    [HttpPut("{id:guid}/status")]
    [Authorize(Roles = $"{Roles.BloodBank},{Roles.Admin}")]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromQuery] AppointmentStatus status)
    {
        var appt = await _db.DonationAppointments.FindAsync(id);
        if (appt is null) return NotFound("Appointment not found.");

        appt.Status = status;
        await _db.SaveChangesAsync();

        return Ok(new { message = $"Appointment status updated to {status}." });
    }

    [HttpDelete("{id:guid}")]
    [Authorize]
    public async Task<IActionResult> Delete(Guid id)
    {
        var appt = await _db.DonationAppointments.FindAsync(id);
        if (appt is null) return NotFound("Appointment not found.");

        var userId = CurrentUserId;
        var isAdmin = User.IsInRole(Roles.Admin);
        var isBloodBank = User.IsInRole(Roles.BloodBank);
        if (appt.DonorUserId != userId && !isAdmin && !isBloodBank)
            return StatusCode(403, "You do not have permission to delete this appointment.");

        _db.DonationAppointments.Remove(appt);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpGet("screening-questions")]
    [AllowAnonymous]
    public IActionResult GetScreeningQuestions()
    {
        var questions = new List<PreScreeningQuestionDto>
        {
            new(1, "Are you feeling healthy, well, and free of any symptoms of fever or cold today?", "General Health", false, "Donors must feel fully well on the donation day."),
            new(2, "Do you weigh at least 45 kg (or 50 kg for platelet donation)?", "Physical Criteria", false, "Per NBTS guidelines, minimum weight is 45 kg."),
            new(3, "Have you donated blood within the last 4 months (120 days)?", "Donation History", true, "Sri Lanka requires a 120-day interval for male and female whole blood donors."),
            new(4, "Have you had a tattoo, body piercing, or acupuncture in the past 6 months?", "Safety & Risk", true, "Temporary deferral of 6 months is required after skin penetration procedures."),
            new(5, "Have you undergone major surgery or received a blood transfusion in the past 12 months?", "Medical History", true, "Transfusion recipients require 12 months deferral."),
            new(6, "Are you currently taking antibiotics, anticoagulants, or medication for a chronic infection?", "Medications", true, "Certain medications require a wash-out window before donation."),
            new(7, "Have you had dental extraction or minor oral surgery in the past 72 hours?", "Dental", true, "Short deferral until healing is complete."),
            new(8, "Have you ever tested positive for Hepatitis B, Hepatitis C, HIV, or Syphilis?", "Infectious Diseases", true, "Permanent disqualification per NBTS safety standards."),
            new(9, "For female donors: Are you currently pregnant, or have you delivered a baby within the last 12 months?", "Obstetric", true, "Pregnancy and postpartum period require a 12-month deferral."),
            new(10, "Have you had malaria or traveled to a high-risk endemic zone in the past 12 months?", "Travel & Exposure", true, "Malaria history requires specific screening and clearance.")
        };

        return Ok(questions);
    }
}
