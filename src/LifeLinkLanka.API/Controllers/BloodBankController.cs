using LifeLinkLanka.Domain.Constants;
using LifeLinkLanka.Domain.Entities;
using LifeLinkLanka.Domain.Enums;
using LifeLinkLanka.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using LifeLinkLanka.Application.DTOs.BloodBank;

namespace LifeLinkLanka.API.Controllers;

[ApiController]
[Route("api/v1/blood-banks")]
public class BloodBankController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    public BloodBankController(ApplicationDbContext db) => _db = db;

    [HttpPost]
    [Authorize(Roles = $"{Roles.BloodBank},{Roles.Admin}")]
    public async Task<IActionResult> Create(CreateBloodBankDto dto)
    {
        var bank = new BloodBank
        {
            Name = dto.Name,
            District = dto.District,
            ContactPhone = dto.ContactPhone,
            VerificationStatus = VerificationStatus.Verified
        };
        _db.BloodBanks.Add(bank);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetById), new { id = bank.Id }, bank);
    }

    [HttpGet("{id:guid}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetById(Guid id)
    {
        var bank = await _db.BloodBanks.FindAsync(id);
        return bank is null ? NotFound() : Ok(bank);
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> GetAll([FromQuery] string? district) =>
        Ok(await _db.BloodBanks
            .Where(b => district == null || b.District == district)
            .OrderBy(b => b.Name)
            .ToListAsync());

    [HttpPut("{id:guid}")]
    [Authorize(Roles = $"{Roles.BloodBank},{Roles.Admin}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] CreateBloodBankDto dto)
    {
        var bank = await _db.BloodBanks.FindAsync(id);
        if (bank is null) return NotFound("Blood bank not found.");

        bank.Name = dto.Name;
        bank.District = dto.District;
        bank.ContactPhone = dto.ContactPhone;

        await _db.SaveChangesAsync();
        return Ok(bank);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> Delete(Guid id)
    {
        var bank = await _db.BloodBanks.FindAsync(id);
        if (bank is null) return NotFound("Blood bank not found.");

        _db.BloodBanks.Remove(bank);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpGet("find-donor")]
    [Authorize(Roles = $"{Roles.BloodBank},{Roles.Admin}")]
    public async Task<IActionResult> FindDonor([FromQuery] string query)
    {
        var user = await _db.Users
            .Include(u => u.DonorProfile)
            .FirstOrDefaultAsync(u => u.NicNumber == query || u.Email == query);

        if (user is null || user.DonorProfile is null)
            return NotFound("No registered donor profile found with this NIC or Email.");

        return Ok(new
        {
            UserId = user.Id,
            user.FullName,
            user.Email,
            user.NicNumber,
            user.District,
            user.PhoneNumber,
            user.DonorProfile.BloodType,
            user.DonorProfile.IsEligibleToDonate,
            user.DonorProfile.DonorCardNumber,
            user.DonorProfile.DonationsCompletedCount
        });
    }

    /// <summary>Records a completed donation and updates the donor's cooldown timer.</summary>
    [HttpPost("{bankId:guid}/record-donation")]
    [Authorize(Roles = $"{Roles.BloodBank},{Roles.Admin}")]
    public async Task<IActionResult> RecordDonation(Guid bankId, [FromQuery] Guid donorUserId, [FromQuery] double volumeMl = 450)
    {
        var profile = await _db.DonorProfiles.FirstOrDefaultAsync(p => p.UserId == donorUserId);
        if (profile is null) return NotFound("Donor profile not found.");

        var record = new DonationRecord
        {
            DonorProfileId = profile.Id,
            BloodBankId = bankId,
            DonationDateUtc = DateTime.UtcNow,
            VolumeMl = volumeMl
        };
        _db.DonationRecords.Add(record);

        profile.DonationsCompletedCount += 1;
        profile.TotalVolumeMl += volumeMl;
        profile.LastDonationDateUtc = DateTime.UtcNow;
        profile.IsEligibleToDonate = false; // resets on the 120-day job / manual recalculation

        await _db.SaveChangesAsync();
        return Ok(record);
    }

    [HttpDelete("donations/{id:guid}")]
    [Authorize(Roles = $"{Roles.BloodBank},{Roles.Admin}")]
    public async Task<IActionResult> DeleteDonationRecord(Guid id)
    {
        var record = await _db.DonationRecords.Include(r => r.DonorProfile).FirstOrDefaultAsync(r => r.Id == id);
        if (record is null) return NotFound("Donation record not found.");

        if (record.DonorProfile != null)
        {
            record.DonorProfile.DonationsCompletedCount = Math.Max(0, record.DonorProfile.DonationsCompletedCount - 1);
            record.DonorProfile.TotalVolumeMl = Math.Max(0, record.DonorProfile.TotalVolumeMl - record.VolumeMl);
        }

        _db.DonationRecords.Remove(record);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}