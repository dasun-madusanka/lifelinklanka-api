using LifeLinkLanka.Application.DTOs.Analytics;
using LifeLinkLanka.Domain.Enums;
using LifeLinkLanka.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LifeLinkLanka.API.Controllers;

[ApiController]
[Route("api/v1/analytics")]
public class AnalyticsController : ControllerBase
{
    private readonly ApplicationDbContext _db;

    public AnalyticsController(ApplicationDbContext db) => _db = db;

    [HttpGet("dashboard")]
    [AllowAnonymous]
    public async Task<IActionResult> GetDashboardStats()
    {
        var totalDonors = await _db.DonorProfiles.CountAsync();
        var totalHospitals = await _db.Hospitals.CountAsync(h => h.VerificationStatus == VerificationStatus.Verified);
        var totalBloodBanks = await _db.BloodBanks.CountAsync(b => b.VerificationStatus == VerificationStatus.Verified);
        var activeCamps = await _db.BloodCamps.CountAsync(c => c.Status == CampStatus.Upcoming || c.Status == CampStatus.Ongoing);
        var totalUnitsAvailable = await _db.BloodInventories.SumAsync(i => i.UnitsAvailable);
        var criticalRequests = await _db.BloodRequests.CountAsync(r => r.Status == RequestStatus.Open && r.Urgency == UrgencyLevel.Critical);
        var totalDonations = await _db.DonationRecords.CountAsync();

        // Each voluntary donation separates into 3 lifesaving components (PRBC, Platelets, Plasma)
        var livesSaved = totalDonations * 3;

        var dto = new NationalDashboardStatsDto(
            totalDonors,
            totalHospitals,
            totalBloodBanks,
            activeCamps,
            totalUnitsAvailable,
            criticalRequests,
            totalDonations,
            livesSaved
        );

        return Ok(dto);
    }

    [HttpGet("blood-distribution")]
    [AllowAnonymous]
    public async Task<IActionResult> GetBloodTypeDistribution()
    {
        var inventories = await _db.BloodInventories.ToListAsync();
        var totalUnits = Math.Max(inventories.Sum(i => i.UnitsAvailable), 1);

        var distribution = Enum.GetValues<BloodType>().Select(type =>
        {
            var units = inventories.Where(i => i.BloodType == type).Sum(i => i.UnitsAvailable);
            var percentage = Math.Round((double)units / totalUnits * 100, 1);
            var name = type switch
            {
                BloodType.APositive => "A+",
                BloodType.ANegative => "A-",
                BloodType.BPositive => "B+",
                BloodType.BNegative => "B-",
                BloodType.ABPositive => "AB+",
                BloodType.ABNegative => "AB-",
                BloodType.OPositive => "O+",
                BloodType.ONegative => "O-",
                _ => type.ToString()
            };

            return new BloodTypeDistributionDto(type, name, units, percentage);
        }).ToList();

        return Ok(distribution);
    }

    [HttpGet("monthly-trends")]
    [AllowAnonymous]
    public async Task<IActionResult> GetMonthlyTrends()
    {
        var now = DateTime.UtcNow;
        var sixMonthsAgo = now.AddMonths(-5).Date;
        sixMonthsAgo = new DateTime(sixMonthsAgo.Year, sixMonthsAgo.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        var donations = await _db.DonationRecords
            .Where(d => d.DonationDateUtc >= sixMonthsAgo)
            .ToListAsync();

        var trends = new List<MonthlyDonationTrendDto>();

        for (int i = 5; i >= 0; i--)
        {
            var targetMonth = now.AddMonths(-i);
            var monthDonations = donations
                .Where(d => d.DonationDateUtc.Year == targetMonth.Year && d.DonationDateUtc.Month == targetMonth.Month)
                .ToList();

            var count = monthDonations.Count;
            var unitsCollected = (int)Math.Round(monthDonations.Sum(d => d.VolumeMl) / 450.0);

            trends.Add(new MonthlyDonationTrendDto(
                targetMonth.ToString("MMM"),
                targetMonth.Year,
                count,
                unitsCollected
            ));
        }

        return Ok(trends);
    }

    [HttpGet("districts")]
    [AllowAnonymous]
    public async Task<IActionResult> GetDistrictSummaries()
    {
        var sriLankaDistricts = new[]
        {
            "Colombo", "Gampaha", "Kalutara", "Kandy", "Matale", "Nuwara Eliya",
            "Galle", "Matara", "Hambantota", "Jaffna", "Kilinochchi", "Mannar",
            "Vavuniya", "Mullaitivu", "Batticaloa", "Ampara", "Trincomalee",
            "Kurunegala", "Puttalam", "Anuradhapura", "Polonnaruwa", "Badulla",
            "Monaragala", "Ratnapura", "Kegalle"
        };

        var requests = await _db.BloodRequests.Include(r => r.Hospital).Where(r => r.Status == RequestStatus.Open).ToListAsync();
        var donors = await _db.DonorProfiles.Include(d => d.User).ToListAsync();
        var inventories = await _db.BloodInventories.Include(i => i.BloodBank).ToListAsync();

        var summaries = sriLankaDistricts.Select(dist =>
        {
            var distReqs = requests.Where(r => string.Equals(r.Hospital?.District, dist, StringComparison.OrdinalIgnoreCase)).ToList();
            var distDonors = donors.Count(d => string.Equals(d.User?.District, dist, StringComparison.OrdinalIgnoreCase));
            var distUnits = inventories.Where(i => string.Equals(i.BloodBank?.District, dist, StringComparison.OrdinalIgnoreCase)).Sum(i => i.UnitsAvailable);

            var activeReqCount = distReqs.Count;
            var unitsNeeded = distReqs.Sum(r => r.UnitsNeeded - r.UnitsFulfilled);

            var urgency = distReqs.Any(r => r.Urgency == UrgencyLevel.Critical) ? "Critical"
                        : (activeReqCount > 0 ? "Urgent" : (distUnits < 10 ? "Moderate" : "Normal"));

            return new DistrictDemandSummaryDto(dist, activeReqCount, unitsNeeded, distDonors, distUnits, urgency);
        }).ToList();

        return Ok(summaries);
    }
}
