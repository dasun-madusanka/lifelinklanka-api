using LifeLinkLanka.Domain.Enums;

namespace LifeLinkLanka.Application.DTOs.Analytics;

public record NationalDashboardStatsDto(
    int TotalDonors,
    int TotalHospitals,
    int TotalBloodBanks,
    int ActiveCamps,
    int TotalUnitsAvailable,
    int CriticalRequestsCount,
    int TotalDonationsCompleted,
    int EstimatedLivesSaved);

public record BloodTypeDistributionDto(
    BloodType BloodType,
    string BloodTypeName,
    int AvailableUnits,
    double PercentageOfTotal);

public record MonthlyDonationTrendDto(
    string MonthName,
    int Year,
    int DonationCount,
    int UnitsCollected);

public record DistrictDemandSummaryDto(
    string District,
    int ActiveRequests,
    int UnitsNeeded,
    int RegisteredDonors,
    int AvailableStockUnits,
    string UrgencyRating);
