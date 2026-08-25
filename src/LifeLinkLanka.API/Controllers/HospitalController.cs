using LifeLinkLanka.Domain.Constants;
using LifeLinkLanka.Domain.Entities;
using LifeLinkLanka.Domain.Enums;
using LifeLinkLanka.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LifeLinkLanka.API.Controllers;

public record CreateHospitalDto(string Name, string RegistrationNumber, string District, string Address, string ContactPhone);

[ApiController]
[Route("api/v1/hospitals")]
[Authorize]
public class HospitalController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    public HospitalController(ApplicationDbContext db) => _db = db;

    /// <summary>
    /// Any authenticated user can submit a hospital for verification (typically done by
    /// HospitalStaff during onboarding). It stays in "Pending" status until an Admin approves it
    /// via POST /api/v1/admin/hospitals/{id}/verify — see AdminController.
    /// </summary>
    [HttpPost]
    [Authorize(Roles = $"{Roles.HospitalStaff},{Roles.Admin}")]
    public async Task<IActionResult> Create(CreateHospitalDto dto)
    {
        var userId = Guid.Parse(User.FindFirst("sub")!.Value);

        var exists = await _db.Hospitals.AnyAsync(h => h.RegistrationNumber == dto.RegistrationNumber);
        if (exists) return Conflict("A hospital with this registration number already exists.");

        var hospital = new Hospital
        {
            Name = dto.Name,
            RegistrationNumber = dto.RegistrationNumber,
            District = dto.District,
            Address = dto.Address,
            ContactPhone = dto.ContactPhone,
            CreatedByUserId = userId,
            VerificationStatus = VerificationStatus.Pending
        };

        _db.Hospitals.Add(hospital);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = hospital.Id }, hospital);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var hospital = await _db.Hospitals.FindAsync(id);
        return hospital is null ? NotFound() : Ok(hospital);
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] string? district) =>
        Ok(await _db.Hospitals
            .Where(h => h.VerificationStatus == VerificationStatus.Verified &&
                        (district == null || h.District == district))
            .ToListAsync());
}