using LifeLinkLanka.API.Hubs;
using LifeLinkLanka.Application.DTOs.BloodRequest;
using LifeLinkLanka.Application.Interfaces;
using LifeLinkLanka.Domain.Constants;
using LifeLinkLanka.Domain.Entities;
using LifeLinkLanka.Domain.Enums;
using LifeLinkLanka.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using LifeLinkLanka.API.Extensions;

namespace LifeLinkLanka.API.Controllers;

[ApiController]
[Route("api/v1/blood-requests")]
public class BloodRequestController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly IHubContext<EmergencyHub> _hub;
    private readonly IBloodMatchingService _matchingService;

    public BloodRequestController(ApplicationDbContext db, IHubContext<EmergencyHub> hub, IBloodMatchingService matchingService)
    {
        _db = db;
        _hub = hub;
        _matchingService = matchingService;
    }

    [HttpPost]
    [Authorize(Roles = $"{Roles.HospitalStaff},{Roles.Admin}")]
    public async Task<IActionResult> Create(CreateBloodRequestDto dto)
    {
        var hospital = await _db.Hospitals.FindAsync(dto.HospitalId);
        if (hospital is null) return NotFound("Hospital not found.");

        var request = new BloodRequest
        {
            HospitalId = dto.HospitalId,
            BloodTypeNeeded = dto.BloodTypeNeeded,
            ComponentNeeded = dto.ComponentNeeded,
            UnitsNeeded = dto.UnitsNeeded,
            UnitsFulfilled = 0,
            Urgency = dto.Urgency,
            PatientContext = dto.PatientContext,
            ClinicalIndication = dto.ClinicalIndication ?? "Transfusion Requirement",
            NeededByUtc = dto.NeededByUtc,
            Status = RequestStatus.Open
        };

        _db.BloodRequests.Add(request);
        await _db.SaveChangesAsync();

        // Intelligent clinical blood matching (ABO/Rh compatibility & district proximity)
        var matchCandidates = await _matchingService.FindMatchingDonorsAsync(request.Id);

        foreach (var candidate in matchCandidates)
        {
            _db.DonorMatches.Add(new DonorMatch
            {
                BloodRequestId = request.Id,
                DonorUserId = candidate.DonorUserId,
                NotifiedViaRealtime = true
            });
        }
        await _db.SaveChangesAsync();

        // Broadcast real-time emergency alert
        await _hub.Clients.All.SendAsync("BloodRequestAlert", new
        {
            request.Id,
            HospitalName = hospital.Name,
            District = hospital.District,
            request.BloodTypeNeeded,
            request.ComponentNeeded,
            request.UnitsNeeded,
            request.Urgency,
            request.PatientContext,
            request.NeededByUtc,
            IsCritical = request.Urgency == UrgencyLevel.Critical
        });

        var responseDto = new BloodRequestResponseDto(
            request.Id,
            request.HospitalId,
            hospital.Name,
            hospital.District,
            request.BloodTypeNeeded,
            request.ComponentNeeded,
            request.UnitsNeeded,
            request.UnitsFulfilled,
            request.Urgency,
            request.Status,
            request.PatientContext,
            request.ClinicalIndication,
            request.NeededByUtc,
            request.CreatedAtUtc,
            matchCandidates.Count
        );

        return CreatedAtAction(nameof(GetById), new { id = request.Id }, responseDto);
    }

    [HttpGet("{id:guid}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetById(Guid id)
    {
        var req = await _db.BloodRequests
            .Include(r => r.Hospital)
            .Include(r => r.Matches)
            .FirstOrDefaultAsync(r => r.Id == id);

        if (req is null) return NotFound();

        var dto = new BloodRequestResponseDto(
            req.Id,
            req.HospitalId,
            req.Hospital.Name,
            req.Hospital.District,
            req.BloodTypeNeeded,
            req.ComponentNeeded,
            req.UnitsNeeded,
            req.UnitsFulfilled,
            req.Urgency,
            req.Status,
            req.PatientContext,
            req.ClinicalIndication,
            req.NeededByUtc,
            req.CreatedAtUtc,
            req.Matches.Count
        );

        return Ok(dto);
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> GetOpenRequests(
        [FromQuery] string? district,
        [FromQuery] UrgencyLevel? urgency,
        [FromQuery] BloodType? bloodType)
    {
        var query = _db.BloodRequests
            .Include(r => r.Hospital)
            .Include(r => r.Matches)
            .Where(r => r.Status == RequestStatus.Open);

        if (!string.IsNullOrWhiteSpace(district))
            query = query.Where(r => r.Hospital.District == district);

        if (urgency.HasValue)
            query = query.Where(r => r.Urgency == urgency.Value);

        if (bloodType.HasValue)
            query = query.Where(r => r.BloodTypeNeeded == bloodType.Value);

        var list = await query
            .OrderByDescending(r => r.Urgency)
            .ThenBy(r => r.NeededByUtc)
            .Select(r => new BloodRequestResponseDto(
                r.Id,
                r.HospitalId,
                r.Hospital.Name,
                r.Hospital.District,
                r.BloodTypeNeeded,
                r.ComponentNeeded,
                r.UnitsNeeded,
                r.UnitsFulfilled,
                r.Urgency,
                r.Status,
                r.PatientContext,
                r.ClinicalIndication,
                r.NeededByUtc,
                r.CreatedAtUtc,
                r.Matches.Count
            ))
            .ToListAsync();

        return Ok(list);
    }

    [HttpPost("{id:guid}/respond")]
    [Authorize(Roles = Roles.Donor)]
    public async Task<IActionResult> RespondToRequest(Guid id, [FromBody] bool accept)
    {
        var userId = User.GetUserId();
        var match = await _db.DonorMatches.FirstOrDefaultAsync(m => m.BloodRequestId == id && m.DonorUserId == userId);

        if (match is null)
        {
            // If donor wants to volunteer even if not pre-matched
            match = new DonorMatch
            {
                BloodRequestId = id,
                DonorUserId = userId,
                NotifiedViaRealtime = false
            };
            _db.DonorMatches.Add(match);
        }

        match.DonorResponded = true;
        match.DonorAccepted = accept;
        match.RespondedAtUtc = DateTime.UtcNow;

        if (accept)
        {
            var req = await _db.BloodRequests.FindAsync(id);
            if (req != null && req.UnitsFulfilled < req.UnitsNeeded)
            {
                req.UnitsFulfilled += 1;
                if (req.UnitsFulfilled >= req.UnitsNeeded)
                    req.Status = RequestStatus.Fulfilled;
                else
                    req.Status = RequestStatus.PartiallyFulfilled;
            }
        }

        await _db.SaveChangesAsync();

        return Ok(new { message = accept ? "Thank you! Your pledge to donate has been recorded." : "Response recorded." });
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = $"{Roles.HospitalStaff},{Roles.Admin}")]
    public async Task<IActionResult> UpdateRequest(Guid id, [FromBody] UpdateBloodRequestDto dto)
    {
        var req = await _db.BloodRequests.Include(r => r.Hospital).FirstOrDefaultAsync(r => r.Id == id);
        if (req is null) return NotFound("Blood request not found.");

        if (dto.UnitsNeeded.HasValue && dto.UnitsNeeded.Value > 0)
            req.UnitsNeeded = dto.UnitsNeeded.Value;

        if (dto.Urgency.HasValue)
            req.Urgency = dto.Urgency.Value;

        if (dto.Status.HasValue)
            req.Status = dto.Status.Value;

        if (!string.IsNullOrWhiteSpace(dto.ClinicalIndication))
            req.ClinicalIndication = dto.ClinicalIndication;

        if (dto.PatientContext != null)
            req.PatientContext = dto.PatientContext;

        if (dto.NeededByUtc.HasValue)
            req.NeededByUtc = dto.NeededByUtc.Value;

        await _db.SaveChangesAsync();
        return Ok(req);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = $"{Roles.HospitalStaff},{Roles.Admin}")]
    public async Task<IActionResult> DeleteRequest(Guid id)
    {
        var req = await _db.BloodRequests.Include(r => r.Matches).FirstOrDefaultAsync(r => r.Id == id);
        if (req is null) return NotFound("Blood request not found.");

        _db.DonorMatches.RemoveRange(req.Matches);
        _db.BloodRequests.Remove(req);
        await _db.SaveChangesAsync();

        return NoContent();
    }
}