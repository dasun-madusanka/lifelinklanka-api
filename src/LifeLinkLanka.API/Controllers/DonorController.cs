using LifeLinkLanka.Domain.Constants;
using LifeLinkLanka.Domain.Entities;
using LifeLinkLanka.Domain.Enums;
using LifeLinkLanka.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LifeLinkLanka.API.Controllers;

public record UpsertDonorProfileDto(BloodType BloodType, double WeightKg, bool ConsentToBeContacted, string? MedicalNotes);

[ApiController]
[Route("api/v1/donors")]
[Authorize(Roles = Roles.Donor)]
public class DonorController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    public DonorController(ApplicationDbContext db) => _db = db;

    private Guid CurrentUserId => Guid.Parse(User.FindFirst("sub")!.Value);

    /// <summary>
    /// Creates or updates the logged-in donor's profile. This MUST be called after registration
    /// for a donor to ever be matched to a blood request — BloodRequestController only queries
    /// DonorProfiles, so a user with no profile is invisible to the matching system.
    /// </summary>
    [HttpPut("profile")]
    public async Task<IActionResult> UpsertProfile(UpsertDonorProfileDto dto)
    {
        if (dto.WeightKg < 45)
            return BadRequest("Minimum donor weight is 45kg per WHO/Sri Lanka Blood Transfusion Service guidelines.");

        var profile = await _db.DonorProfiles.FirstOrDefaultAsync(p => p.UserId == CurrentUserId);

        if (profile is null)
        {
            profile = new DonorProfile
            {
                UserId = CurrentUserId,
                BloodType = dto.BloodType,
                WeightKg = dto.WeightKg,
                ConsentToBeContacted = dto.ConsentToBeContacted,
                MedicalNotes = dto.MedicalNotes
            };
            _db.DonorProfiles.Add(profile);
        }
        else
        {
            profile.BloodType = dto.BloodType;
            profile.WeightKg = dto.WeightKg;
            profile.ConsentToBeContacted = dto.ConsentToBeContacted;
            profile.MedicalNotes = dto.MedicalNotes;
        }

        await _db.SaveChangesAsync();
        return Ok(profile);
    }

    [HttpGet("profile")]
    public async Task<IActionResult> GetMyProfile()
    {
        var profile = await _db.DonorProfiles
            .Include(p => p.DonationHistory)
            .FirstOrDefaultAsync(p => p.UserId == CurrentUserId);

        return profile is null ? NotFound("No donor profile yet — call PUT /profile first.") : Ok(profile);
    }

    /// <summary>
    /// Recalculates eligibility based on the 4-month (≈120 day) minimum donation interval
    /// used by Sri Lanka's National Blood Transfusion Service. Normally run by a Hangfire
    /// background job on a schedule — exposed here too so you can test it manually.
    /// </summary>
    [HttpPost("profile/recalculate-eligibility")]
    public async Task<IActionResult> RecalculateEligibility()
    {
        var profile = await _db.DonorProfiles.FirstOrDefaultAsync(p => p.UserId == CurrentUserId);
        if (profile is null) return NotFound();

        profile.IsEligibleToDonate = profile.LastDonationDateUtc is null
            || (DateTime.UtcNow - profile.LastDonationDateUtc.Value).TotalDays >= 120;

        await _db.SaveChangesAsync();
        return Ok(new { profile.IsEligibleToDonate, profile.LastDonationDateUtc });
    }

    [HttpGet("my-matches")]
    public async Task<IActionResult> GetMyMatches()
    {
        var matches = await _db.DonorMatches
            .Include(m => m.BloodRequest).ThenInclude(r => r.Hospital)
            .Where(m => m.DonorUserId == CurrentUserId)
            .OrderByDescending(m => m.CreatedAtUtc)
            .ToListAsync();

        return Ok(matches);
    }
}