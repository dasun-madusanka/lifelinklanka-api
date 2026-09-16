using LifeLinkLanka.Application.DTOs.BloodCamp;
using LifeLinkLanka.Application.Interfaces;
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
[Route("api/v1/camps")]
public class BloodCampController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly IAuditService _auditService;

    public BloodCampController(ApplicationDbContext db, IAuditService auditService)
    {
        _db = db;
        _auditService = auditService;
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> GetAllCamps([FromQuery] string? district, [FromQuery] CampStatus? status)
    {
        var query = _db.BloodCamps
            .Include(c => c.Registrations)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(district))
            query = query.Where(c => c.District == district);

        if (status.HasValue)
            query = query.Where(c => c.Status == status.Value);

        var camps = await query
            .OrderBy(c => c.StartDateUtc)
            .Select(c => new BloodCampDto(
                c.Id,
                c.Title,
                c.OrganizerName,
                c.District,
                c.VenueAddress,
                c.Latitude,
                c.Longitude,
                c.StartDateUtc,
                c.EndDateUtc,
                c.TargetUnits,
                c.UnitsCollected,
                c.ContactPhone,
                c.SpecialInstructions,
                c.Status,
                c.Registrations.Count
            ))
            .ToListAsync();

        return Ok(camps);
    }

    [HttpGet("{id:guid}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetById(Guid id)
    {
        var camp = await _db.BloodCamps
            .Include(c => c.Registrations)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (camp is null) return NotFound("Blood camp not found.");

        var dto = new BloodCampDto(
            camp.Id,
            camp.Title,
            camp.OrganizerName,
            camp.District,
            camp.VenueAddress,
            camp.Latitude,
            camp.Longitude,
            camp.StartDateUtc,
            camp.EndDateUtc,
            camp.TargetUnits,
            camp.UnitsCollected,
            camp.ContactPhone,
            camp.SpecialInstructions,
            camp.Status,
            camp.Registrations.Count
        );

        return Ok(dto);
    }

    [HttpPost]
    [Authorize(Roles = $"{Roles.Admin},{Roles.BloodBank},{Roles.HospitalStaff}")]
    public async Task<IActionResult> Create([FromBody] CreateBloodCampDto dto)
    {
        var camp = new BloodCamp
        {
            Title = dto.Title,
            OrganizerName = dto.OrganizerName,
            District = dto.District,
            VenueAddress = dto.VenueAddress,
            Latitude = dto.Latitude,
            Longitude = dto.Longitude,
            StartDateUtc = dto.StartDateUtc,
            EndDateUtc = dto.EndDateUtc,
            TargetUnits = dto.TargetUnits,
            ContactPhone = dto.ContactPhone,
            SpecialInstructions = dto.SpecialInstructions,
            Status = CampStatus.Upcoming
        };

        _db.BloodCamps.Add(camp);
        await _db.SaveChangesAsync();

        var userId = User.GetUserId();
        await _auditService.LogAsync(userId, "CAMP_CREATED", $"Created blood camp '{dto.Title}' in {dto.District}");

        return CreatedAtAction(nameof(GetById), new { id = camp.Id }, camp);
    }

    [HttpPost("{id:guid}/register")]
    public async Task<IActionResult> RegisterForCamp(Guid id, [FromBody] RegisterCampDto dto)
    {
        var camp = await _db.BloodCamps.FindAsync(id);
        if (camp is null) return NotFound("Blood camp not found.");

        Guid? currentUserId = null;
        if (User.Identity?.IsAuthenticated == true)
        {
            var parsed = User.GetUserId();
            if (parsed != Guid.Empty)
                currentUserId = parsed;
        }

        var registration = new CampRegistration
        {
            BloodCampId = id,
            UserId = currentUserId,
            DonorName = dto.DonorName,
            ContactPhone = dto.ContactPhone,
            BloodTypePledged = dto.BloodTypePledged,
            RegisteredAtUtc = DateTime.UtcNow
        };

        _db.CampRegistrations.Add(registration);
        await _db.SaveChangesAsync();

        return Ok(new
        {
            message = "Registration successful! You have pledged to donate at this camp.",
            registrationId = registration.Id,
            campTitle = camp.Title
        });
    }

    [HttpGet("{id:guid}/registrations")]
    [Authorize(Roles = $"{Roles.Admin},{Roles.BloodBank},{Roles.HospitalStaff}")]
    public async Task<IActionResult> GetRegistrations(Guid id)
    {
        var regs = await _db.CampRegistrations
            .Include(r => r.BloodCamp)
            .Where(r => r.BloodCampId == id)
            .OrderByDescending(r => r.RegisteredAtUtc)
            .Select(r => new CampRegistrationDto(
                r.Id,
                r.BloodCampId,
                r.BloodCamp.Title,
                r.DonorName,
                r.ContactPhone,
                r.BloodTypePledged,
                r.RegisteredAtUtc,
                r.Attended
            ))
            .ToListAsync();

        return Ok(regs);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = $"{Roles.BloodBank},{Roles.Admin}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] CreateBloodCampDto dto)
    {
        var camp = await _db.BloodCamps.FindAsync(id);
        if (camp is null) return NotFound("Blood camp not found.");

        camp.Title = dto.Title;
        camp.OrganizerName = dto.OrganizerName;
        camp.District = dto.District;
        camp.VenueAddress = dto.VenueAddress;
        camp.StartDateUtc = dto.StartDateUtc;
        camp.EndDateUtc = dto.EndDateUtc;
        camp.TargetUnits = dto.TargetUnits;
        camp.ContactPhone = dto.ContactPhone;
        camp.SpecialInstructions = dto.SpecialInstructions;

        await _db.SaveChangesAsync();

        var userId = User.GetUserId();
        await _auditService.LogAsync(userId, "CAMP_UPDATED", $"Updated blood camp '{dto.Title}'");

        return Ok(camp);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = $"{Roles.BloodBank},{Roles.Admin}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var camp = await _db.BloodCamps.FindAsync(id);
        if (camp is null) return NotFound("Blood camp not found.");

        _db.BloodCamps.Remove(camp);
        await _db.SaveChangesAsync();

        var userId = User.GetUserId();
        await _auditService.LogAsync(userId, "CAMP_DELETED", $"Deleted blood camp ID: {id}");

        return NoContent();
    }
}
