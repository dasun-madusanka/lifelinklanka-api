using LifeLinkLanka.Application.Interfaces;
using LifeLinkLanka.Domain.Entities;
using LifeLinkLanka.Domain.Enums;
using LifeLinkLanka.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LifeLinkLanka.Infrastructure.Services;

public class BloodMatchingService : IBloodMatchingService
{
    private readonly ApplicationDbContext _db;
    private readonly ILogger<BloodMatchingService> _logger;

    public BloodMatchingService(ApplicationDbContext db, ILogger<BloodMatchingService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public IReadOnlyList<BloodType> GetCompatibleDonorBloodTypes(BloodType recipientType, BloodComponentType component)
    {
        if (component == BloodComponentType.FreshFrozenPlasma || component == BloodComponentType.Cryoprecipitate)
        {
            // Plasma compatibility (reverse of RBC)
            return recipientType switch
            {
                BloodType.OPositive or BloodType.ONegative =>
                    new[] { BloodType.OPositive, BloodType.ONegative, BloodType.APositive, BloodType.ANegative, BloodType.BPositive, BloodType.BNegative, BloodType.ABPositive, BloodType.ABNegative },
                BloodType.APositive or BloodType.ANegative =>
                    new[] { BloodType.APositive, BloodType.ANegative, BloodType.ABPositive, BloodType.ABNegative },
                BloodType.BPositive or BloodType.BNegative =>
                    new[] { BloodType.BPositive, BloodType.BNegative, BloodType.ABPositive, BloodType.ABNegative },
                BloodType.ABPositive or BloodType.ABNegative =>
                    new[] { BloodType.ABPositive, BloodType.ABNegative },
                _ => new[] { recipientType }
            };
        }

        // Standard RBC / Whole Blood compatibility
        return recipientType switch
        {
            BloodType.ONegative => new[] { BloodType.ONegative },
            BloodType.OPositive => new[] { BloodType.OPositive, BloodType.ONegative },
            BloodType.ANegative => new[] { BloodType.ANegative, BloodType.ONegative },
            BloodType.APositive => new[] { BloodType.APositive, BloodType.ANegative, BloodType.OPositive, BloodType.ONegative },
            BloodType.BNegative => new[] { BloodType.BNegative, BloodType.ONegative },
            BloodType.BPositive => new[] { BloodType.BPositive, BloodType.BNegative, BloodType.OPositive, BloodType.ONegative },
            BloodType.ABNegative => new[] { BloodType.ABNegative, BloodType.ANegative, BloodType.BNegative, BloodType.ONegative },
            BloodType.ABPositive => new[] { BloodType.ABPositive, BloodType.ABNegative, BloodType.APositive, BloodType.ANegative, BloodType.BPositive, BloodType.BNegative, BloodType.OPositive, BloodType.ONegative },
            _ => new[] { recipientType }
        };
    }

    public bool IsCompatible(BloodType donorType, BloodType recipientType, BloodComponentType component)
    {
        var compatibleTypes = GetCompatibleDonorBloodTypes(recipientType, component);
        return compatibleTypes.Contains(donorType);
    }

    public async Task<List<DonorMatchCandidate>> FindMatchingDonorsAsync(Guid bloodRequestId)
    {
        var request = await _db.BloodRequests
            .Include(r => r.Hospital)
            .FirstOrDefaultAsync(r => r.Id == bloodRequestId);

        if (request is null) return new List<DonorMatchCandidate>();

        var compatibleTypes = GetCompatibleDonorBloodTypes(request.BloodTypeNeeded, request.ComponentNeeded);

        var eligibleDonors = await _db.DonorProfiles
            .Include(d => d.User)
            .Where(d => compatibleTypes.Contains(d.BloodType) &&
                        d.IsEligibleToDonate &&
                        d.ConsentToBeContacted &&
                        d.User.IsActive)
            .ToListAsync();

        var hospitalDistrict = request.Hospital?.District ?? string.Empty;

        var candidates = eligibleDonors.Select(donor =>
        {
            var isExactMatch = donor.BloodType == request.BloodTypeNeeded;
            var isSameDistrict = string.Equals(donor.User.District, hospitalDistrict, StringComparison.OrdinalIgnoreCase);

            // Scoring algorithm: Exact blood match (+50), Same District (+40), Past donations experience (+10)
            double score = 0;
            if (isExactMatch) score += 50; else score += 25;
            if (isSameDistrict) score += 40;
            if (donor.DonationsCompletedCount > 0) score += Math.Min(10, donor.DonationsCompletedCount * 2);

            return new DonorMatchCandidate(
                donor.UserId,
                donor.User.FullName,
                donor.User.District,
                donor.BloodType,
                isExactMatch,
                isSameDistrict,
                score
            );
        })
        .OrderByDescending(c => c.MatchScore)
        .ToList();

        _logger.LogInformation("Found {Count} matching donor candidates for blood request {RequestId} ({BloodType})",
            candidates.Count, bloodRequestId, request.BloodTypeNeeded);

        return candidates;
    }
}
