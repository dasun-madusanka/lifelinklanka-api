using LifeLinkLanka.Domain.Enums;

namespace LifeLinkLanka.Application.DTOs.Donor;

public record UpsertDonorProfileDto(BloodType BloodType, double WeightKg, bool ConsentToBeContacted, string? MedicalNotes);