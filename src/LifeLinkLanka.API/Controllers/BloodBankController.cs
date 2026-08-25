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
[Authorize]
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
            VerificationStatus = VerificationStatus.Pending
        };
        _db.BloodBanks.Add(bank);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetById), new { id = bank.Id }, bank);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var bank = await _db.BloodBanks.FindAsync(id);
        return bank is null ? NotFound() : Ok(bank);
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] string? district) =>
        Ok(await _db.BloodBanks
            .Where(b => b.VerificationStatus == VerificationStatus.Verified &&
                        (district == null || b.District == district))
            .ToListAsync());

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

        profile.LastDonationDateUtc = DateTime.UtcNow;
        profile.IsEligibleToDonate = false; // resets on the 120-day job / manual recalculation

        await _db.SaveChangesAsync();
        return Ok(record);
    }
}