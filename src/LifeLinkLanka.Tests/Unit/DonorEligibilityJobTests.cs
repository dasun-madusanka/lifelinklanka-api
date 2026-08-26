using FluentAssertions;
using LifeLinkLanka.Domain.Entities;
using LifeLinkLanka.Domain.Enums;
using LifeLinkLanka.Infrastructure.Persistence;
using LifeLinkLanka.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace LifeLinkLanka.Tests.Unit;

public class DonorEligibilityJobTests
{
    private static ApplicationDbContext CreateInMemoryDb()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    [Fact]
    public async Task RecalculateAllDonorsAsync_ReactivatesDonorPastCooldown()
    {
        await using var db = CreateInMemoryDb();
        var donor = new DonorProfile
        {
            UserId = Guid.NewGuid(),
            BloodType = BloodType.OPositive,
            WeightKg = 65,
            IsEligibleToDonate = false,
            LastDonationDateUtc = DateTime.UtcNow.AddDays(-121)
        };
        db.DonorProfiles.Add(donor);
        await db.SaveChangesAsync();

        var job = new DonorEligibilityJob(db, NullLogger<DonorEligibilityJob>.Instance);
        await job.RecalculateAllDonorsAsync();

        var updated = await db.DonorProfiles.FirstAsync(d => d.Id == donor.Id);
        updated.IsEligibleToDonate.Should().BeTrue();
    }

    [Fact]
    public async Task RecalculateAllDonorsAsync_DoesNotReactivateDonorStillInCooldown()
    {
        await using var db = CreateInMemoryDb();
        var donor = new DonorProfile
        {
            UserId = Guid.NewGuid(),
            BloodType = BloodType.APositive,
            WeightKg = 70,
            IsEligibleToDonate = false,
            LastDonationDateUtc = DateTime.UtcNow.AddDays(-30)
        };
        db.DonorProfiles.Add(donor);
        await db.SaveChangesAsync();

        var job = new DonorEligibilityJob(db, NullLogger<DonorEligibilityJob>.Instance);
        await job.RecalculateAllDonorsAsync();

        var updated = await db.DonorProfiles.FirstAsync(d => d.Id == donor.Id);
        updated.IsEligibleToDonate.Should().BeFalse();
    }
}