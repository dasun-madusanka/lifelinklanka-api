using LifeLinkLanka.Domain.Enums;

namespace LifeLinkLanka.Application.DTOs.BloodCamp;

public record BloodCampDto(
    Guid Id,
    string Title,
    string OrganizerName,
    string District,
    string VenueAddress,
    double? Latitude,
    double? Longitude,
    DateTime StartDateUtc,
    DateTime EndDateUtc,
    int TargetUnits,
    int UnitsCollected,
    string ContactPhone,
    string? SpecialInstructions,
    CampStatus Status,
    int RegisteredDonorsCount);

public record CreateBloodCampDto(
    string Title,
    string OrganizerName,
    string District,
    string VenueAddress,
    double? Latitude,
    double? Longitude,
    DateTime StartDateUtc,
    DateTime EndDateUtc,
    int TargetUnits,
    string ContactPhone,
    string? SpecialInstructions);

public record RegisterCampDto(
    string DonorName,
    string ContactPhone,
    BloodType BloodTypePledged);

public record CampRegistrationDto(
    Guid Id,
    Guid BloodCampId,
    string CampTitle,
    string DonorName,
    string ContactPhone,
    BloodType BloodTypePledged,
    DateTime RegisteredAtUtc,
    bool Attended);
