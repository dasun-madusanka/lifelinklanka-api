using LifeLinkLanka.Domain.Common;
using LifeLinkLanka.Domain.Enums;

namespace LifeLinkLanka.Domain.Entities;

public class BloodBank : BaseEntity
{
    public string Name { get; set; } = default!;
    public string District { get; set; } = default!;
    public string ContactPhone { get; set; } = default!;
    public VerificationStatus VerificationStatus { get; set; } = VerificationStatus.Pending;
}