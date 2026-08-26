using LifeLinkLanka.Application.Interfaces;
using LifeLinkLanka.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LifeLinkLanka.Infrastructure.Services;

/// <summary>
/// Runs daily via Hangfire. Sri Lanka's National Blood Transfusion Service requires a minimum
/// 120-day (~4 month) gap between whole-blood donations. This job flips IsEligibleToDonate
/// back to true once a donor clears that window, so they start appearing in match results again.
/// </summary>
public class DonorEligibilityJob : IDonorEligibilityJob
{
    private readonly ApplicationDbContext _db;
    private readonly ILogger<DonorEligibilityJob> _logger;
    private const int MinimumDonationIntervalDays = 120;

    public DonorEligibilityJob(ApplicationDbContext db, ILogger<DonorEligibilityJob> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task RecalculateAllDonorsAsync()
    {
        var cutoff = DateTime.UtcNow.AddDays(-MinimumDonationIntervalDays);

        var donorsToReactivate = await _db.DonorProfiles
            .Where(d => !d.IsEligibleToDonate &&
                        d.LastDonationDateUtc != null &&
                        d.LastDonationDateUtc <= cutoff)
            .ToListAsync();

        foreach (var donor in donorsToReactivate)
            donor.IsEligibleToDonate = true;

        // Also catch donors who somehow have no LastDonationDateUtc but are marked ineligible
        // (defensive — shouldn't normally happen, but keeps data self-healing)
        var orphanedIneligible = await _db.DonorProfiles
            .Where(d => !d.IsEligibleToDonate && d.LastDonationDateUtc == null)
            .ToListAsync();

        foreach (var donor in orphanedIneligible)
            donor.IsEligibleToDonate = true;

        var totalUpdated = donorsToReactivate.Count + orphanedIneligible.Count;

        if (totalUpdated > 0)
        {
            await _db.SaveChangesAsync();
            _logger.LogInformation("DonorEligibilityJob: reactivated {Count} donors.", totalUpdated);
        }
        else
        {
            _logger.LogInformation("DonorEligibilityJob: no donors required reactivation.");
        }
    }
}