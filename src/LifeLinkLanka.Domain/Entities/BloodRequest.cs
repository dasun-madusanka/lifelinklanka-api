using LifeLinkLanka.Domain.Common;
using LifeLinkLanka.Domain.Enums;

namespace LifeLinkLanka.Domain.Entities;

public class BloodRequest : BaseEntity
{
    public Guid HospitalId { get; set; }
    public Hospital Hospital { get; set; } = default!;

    public BloodType BloodTypeNeeded { get; set; }
    public int UnitsNeeded { get; set; }
    public int UnitsFulfilled { get; set; } = 0;
    public UrgencyLevel Urgency { get; set; }
    public RequestStatus Status { get; set; } = RequestStatus.Open;
    public string PatientContext { get; set; } = default!; // e.g. "Accident trauma, Colombo General"
    public DateTime NeededByUtc { get; set; }

    public ICollection<DonorMatch> Matches { get; set; } = new List<DonorMatch>();
}