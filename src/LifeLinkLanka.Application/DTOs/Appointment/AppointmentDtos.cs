using LifeLinkLanka.Domain.Enums;

namespace LifeLinkLanka.Application.DTOs.Appointment;

public record BookAppointmentDto(
    Guid? BloodBankId,
    Guid? BloodCampId,
    DateTime ScheduledSlotUtc,
    bool PreScreeningPassed,
    string? PreScreeningAnswersJson,
    string? Notes);

public record AppointmentDto(
    Guid Id,
    Guid DonorUserId,
    string DonorName,
    string DonorEmail,
    string DonorPhone,
    BloodType BloodType,
    Guid? BloodBankId,
    string? BloodBankName,
    Guid? BloodCampId,
    string? BloodCampTitle,
    DateTime ScheduledSlotUtc,
    AppointmentStatus Status,
    bool PreScreeningPassed,
    string? Notes,
    DateTime CreatedAtUtc);

public record PreScreeningQuestionDto(
    int Id,
    string Question,
    string Category,
    bool DisqualifyingIfYes,
    string Hint);
