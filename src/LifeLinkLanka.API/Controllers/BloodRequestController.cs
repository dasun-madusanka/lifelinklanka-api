using LifeLinkLanka.API.Hubs;
using LifeLinkLanka.Domain.Constants;
using LifeLinkLanka.Domain.Entities;
using LifeLinkLanka.Domain.Enums;
using LifeLinkLanka.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace LifeLinkLanka.API.Controllers;

public record CreateBloodRequestDto(Guid HospitalId, BloodType BloodTypeNeeded, int UnitsNeeded,
    UrgencyLevel Urgency, string PatientContext, DateTime NeededByUtc);

[ApiController]
[Route("api/v1/blood-requests")]
[Authorize]
public class BloodRequestController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly IHubContext<EmergencyHub> _hub;

    public BloodRequestController(ApplicationDbContext db, IHubContext<EmergencyHub> hub)
    {
        _db = db;
        _hub = hub;
    }

    [HttpPost]
    [Authorize(Roles = $"{Roles.HospitalStaff},{Roles.Admin}")]
    public async Task<IActionResult> Create(CreateBloodRequestDto dto)
    {
        var request = new BloodRequest
        {
            HospitalId = dto.HospitalId,
            BloodTypeNeeded = dto.BloodTypeNeeded,
            UnitsNeeded = dto.UnitsNeeded,
            Urgency = dto.Urgency,
            PatientContext = dto.PatientContext,
            NeededByUtc = dto.NeededByUtc
        };
        _db.BloodRequests.Add(request);
        await _db.SaveChangesAsync();

        // Find eligible, matching donors and notify in real time
        var matchingDonorIds = await _db.DonorProfiles
            .Where(d => d.BloodType == dto.BloodTypeNeeded && d.IsEligibleToDonate && d.ConsentToBeContacted)
            .Select(d => d.UserId)
            .ToListAsync();

        foreach (var donorId in matchingDonorIds)
        {
            _db.DonorMatches.Add(new DonorMatch
            {
                BloodRequestId = request.Id,
                DonorUserId = donorId,
                NotifiedViaRealtime = true
            });
        }
        await _db.SaveChangesAsync();

        if (dto.Urgency == UrgencyLevel.Critical)
        {
            await _hub.Clients.Group("Donors").SendAsync("CriticalBloodAlert", new
            {
                request.Id, request.BloodTypeNeeded, request.UnitsNeeded, request.PatientContext
            });
        }

        return CreatedAtAction(nameof(GetById), new { id = request.Id }, request);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var req = await _db.BloodRequests.Include(r => r.Hospital).FirstOrDefaultAsync(r => r.Id == id);
        return req is null ? NotFound() : Ok(req);
    }

    [HttpGet]
    public async Task<IActionResult> GetOpenRequests([FromQuery] string? district) =>
        Ok(await _db.BloodRequests
            .Include(r => r.Hospital)
            .Where(r => r.Status == RequestStatus.Open && (district == null || r.Hospital.District == district))
            .OrderByDescending(r => r.Urgency)
            .ToListAsync());

    [HttpPost("{id:guid}/respond")]
    [Authorize(Roles = Roles.Donor)]
    public async Task<IActionResult> RespondToRequest(Guid id, [FromBody] bool accept)
    {
        var userId = Guid.Parse(User.FindFirst("sub")!.Value);
        var match = await _db.DonorMatches.FirstOrDefaultAsync(m => m.BloodRequestId == id && m.DonorUserId == userId);
        if (match is null) return NotFound("No matching donor record found.");

        match.DonorResponded = true;
        match.DonorAccepted = accept;
        match.RespondedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return Ok(new { message = "Response recorded." });
    }
}