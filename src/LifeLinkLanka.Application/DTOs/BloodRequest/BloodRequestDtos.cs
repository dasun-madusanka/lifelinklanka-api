using LifeLinkLanka.Domain.Enums;

namespace LifeLinkLanka.Application.DTOs.BloodRequest;

public record CreateBloodRequestDto(
    Guid HospitalId,
    BloodType BloodTypeNeeded,
    int UnitsNeeded,
    UrgencyLevel Urgency,
    string PatientContext,
    DateTime NeededByUtc,
    BloodComponentType ComponentNeeded = BloodComponentType.WholeBlood,
    string? ClinicalIndication = null);

public record BloodRequestResponseDto(
    Guid Id,
    Guid HospitalId,
    string HospitalName,
    string District,
    BloodType BloodTypeNeeded,
    BloodComponentType ComponentNeeded,
    int UnitsNeeded,
    int UnitsFulfilled,
    UrgencyLevel Urgency,
    RequestStatus Status,
    string PatientContext,
    string? ClinicalIndication,
    DateTime NeededByUtc,
    DateTime CreatedAtUtc,
    int MatchedDonorsCount);

public record UpdateBloodRequestDto(
    int? UnitsNeeded,
    UrgencyLevel? Urgency,
    RequestStatus? Status,
    string? PatientContext,
    string? ClinicalIndication,
    DateTime? NeededByUtc);