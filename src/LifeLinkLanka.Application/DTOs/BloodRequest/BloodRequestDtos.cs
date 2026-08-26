using LifeLinkLanka.Domain.Enums;

namespace LifeLinkLanka.Application.DTOs.BloodRequest;

public record CreateBloodRequestDto(Guid HospitalId, BloodType BloodTypeNeeded, int UnitsNeeded,
    UrgencyLevel Urgency, string PatientContext, DateTime NeededByUtc);