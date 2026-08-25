using LifeLinkLanka.Domain.Common;
using LifeLinkLanka.Domain.Enums;

namespace LifeLinkLanka.Domain.Entities;

public class DonorProfile : BaseEntity
{
    public Guid UserId { get; set; }
    public ApplicationUser User { get; set; } = default!;

    public BloodType BloodType { get; set; }
    public double WeightKg { get; set; }
    public DateTime? LastDonationDateUtc { get; set; }
    public bool IsEligibleToDonate { get; set; } = true; // recalculated by background job
    public string? MedicalNotes { get; set; }
    public bool ConsentToBeContacted { get; set; } = true;

    public ICollection<DonationRecord> DonationHistory { get; set; } = new List<DonationRecord>();
}