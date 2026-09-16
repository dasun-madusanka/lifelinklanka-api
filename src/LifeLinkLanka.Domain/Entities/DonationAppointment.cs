using LifeLinkLanka.Domain.Common;
using LifeLinkLanka.Domain.Enums;

namespace LifeLinkLanka.Domain.Entities;

public class DonationAppointment : BaseEntity
{
    public Guid DonorUserId { get; set; }
    public ApplicationUser User { get; set; } = default!;

    public Guid? BloodBankId { get; set; }
    public BloodBank? BloodBank { get; set; }

    public Guid? BloodCampId { get; set; }
    public BloodCamp? BloodCamp { get; set; }

    public DateTime ScheduledSlotUtc { get; set; }
    public AppointmentStatus Status { get; set; } = AppointmentStatus.Scheduled;
    public bool PreScreeningPassed { get; set; } = true;
    public string? PreScreeningAnswersJson { get; set; }
    public string? Notes { get; set; }
}
