using LifeLinkLanka.Domain.Common;
using LifeLinkLanka.Domain.Enums;

namespace LifeLinkLanka.Domain.Entities;

public class Hospital : BaseEntity
{
    public string Name { get; set; } = default!;
    public string RegistrationNumber { get; set; } = default!; // Ministry of Health reg no.
    public string District { get; set; } = default!;
    public string Address { get; set; } = default!;
    public string ContactPhone { get; set; } = default!;
    public VerificationStatus VerificationStatus { get; set; } = VerificationStatus.Pending;
    public Guid CreatedByUserId { get; set; }

    public ICollection<BloodRequest> BloodRequests { get; set; } = new List<BloodRequest>();
}