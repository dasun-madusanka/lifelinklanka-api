using FluentAssertions;
using LifeLinkLanka.Domain.Entities;
using LifeLinkLanka.Domain.Enums;
using LifeLinkLanka.Infrastructure.Persistence;
using LifeLinkLanka.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace LifeLinkLanka.Tests;

public class BloodMatchingServiceTests
{
    private static ApplicationDbContext CreateInMemoryDb()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    [Fact]
    public void GetCompatibleDonorBloodTypes_WholeBlood_OPositiveRecipient_ReturnsOPositiveAndONegative()
    {
        using var db = CreateInMemoryDb();
        var service = new BloodMatchingService(db, NullLogger<BloodMatchingService>.Instance);

        var compatible = service.GetCompatibleDonorBloodTypes(BloodType.OPositive, BloodComponentType.WholeBlood);

        compatible.Should().Contain(new[] { BloodType.OPositive, BloodType.ONegative });
        compatible.Should().NotContain(BloodType.APositive);
        compatible.Should().NotContain(BloodType.BPositive);
    }

    [Fact]
    public void GetCompatibleDonorBloodTypes_WholeBlood_ABPositiveRecipient_IsUniversalRecipient()
    {
        using var db = CreateInMemoryDb();
        var service = new BloodMatchingService(db, NullLogger<BloodMatchingService>.Instance);

        var compatible = service.GetCompatibleDonorBloodTypes(BloodType.ABPositive, BloodComponentType.PackedRedBloodCells);

        compatible.Should().HaveCount(8);
        compatible.Should().Contain(BloodType.ONegative);
        compatible.Should().Contain(BloodType.ABPositive);
    }

    [Fact]
    public void GetCompatibleDonorBloodTypes_Plasma_OPositiveRecipient_CanReceiveFromAllGroups()
    {
        using var db = CreateInMemoryDb();
        var service = new BloodMatchingService(db, NullLogger<BloodMatchingService>.Instance);

        var compatible = service.GetCompatibleDonorBloodTypes(BloodType.OPositive, BloodComponentType.FreshFrozenPlasma);

        compatible.Should().HaveCount(8);
    }

    [Fact]
    public void IsCompatible_RedBloodCells_ValidatesCorrectly()
    {
        using var db = CreateInMemoryDb();
        var service = new BloodMatchingService(db, NullLogger<BloodMatchingService>.Instance);

        // O- can donate to A+
        service.IsCompatible(BloodType.ONegative, BloodType.APositive, BloodComponentType.PackedRedBloodCells).Should().BeTrue();

        // A+ CANNOT donate to B+
        service.IsCompatible(BloodType.APositive, BloodType.BPositive, BloodComponentType.PackedRedBloodCells).Should().BeFalse();
    }

    [Fact]
    public async Task FindMatchingDonorsAsync_PrioritizesSameDistrictDonors()
    {
        await using var db = CreateInMemoryDb();

        var hospital = new Hospital
        {
            Id = Guid.NewGuid(),
            Name = "Colombo General",
            RegistrationNumber = "MOH/H/001",
            District = "Colombo",
            ContactPhone = "0112345678",
            Address = "Colombo",
            VerificationStatus = VerificationStatus.Verified
        };
        db.Hospitals.Add(hospital);

        var request = new BloodRequest
        {
            Id = Guid.NewGuid(),
            HospitalId = hospital.Id,
            Hospital = hospital,
            BloodTypeNeeded = BloodType.BPositive,
            ComponentNeeded = BloodComponentType.WholeBlood,
            UnitsNeeded = 2,
            PatientContext = "Emergency ICU Trauma Case",
            NeededByUtc = DateTime.UtcNow.AddDays(1)
        };
        db.BloodRequests.Add(request);

        var localUser = new ApplicationUser { Id = Guid.NewGuid(), District = "Colombo", FullName = "Colombo Hero", NicNumber = "199012345678", IsActive = true, Email = "colombo@test.com" };
        var distantUser = new ApplicationUser { Id = Guid.NewGuid(), District = "Jaffna", FullName = "Jaffna Hero", NicNumber = "199212345678", IsActive = true, Email = "jaffna@test.com" };
        db.Users.AddRange(localUser, distantUser);

        var localDonor = new DonorProfile
        {
            Id = Guid.NewGuid(),
            UserId = localUser.Id,
            User = localUser,
            BloodType = BloodType.BPositive,
            IsEligibleToDonate = true,
            ConsentToBeContacted = true,
            WeightKg = 70
        };

        var distantDonor = new DonorProfile
        {
            Id = Guid.NewGuid(),
            UserId = distantUser.Id,
            User = distantUser,
            BloodType = BloodType.BPositive,
            IsEligibleToDonate = true,
            ConsentToBeContacted = true,
            WeightKg = 68
        };

        db.DonorProfiles.AddRange(localDonor, distantDonor);
        await db.SaveChangesAsync();

        var service = new BloodMatchingService(db, NullLogger<BloodMatchingService>.Instance);
        var matches = await service.FindMatchingDonorsAsync(request.Id);

        matches.Should().NotBeEmpty();
        matches.First().District.Should().Be("Colombo");
        matches.First().IsSameDistrict.Should().BeTrue();
    }
}