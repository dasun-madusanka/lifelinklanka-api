using LifeLinkLanka.Domain.Common;
using LifeLinkLanka.Domain.Enums;

namespace LifeLinkLanka.Domain.Entities;

public class CampRegistration : BaseEntity
{
    public Guid BloodCampId { get; set; }
    public BloodCamp BloodCamp { get; set; } = default!;

    public Guid? UserId { get; set; }
    public ApplicationUser? User { get; set; }

    public string DonorName { get; set; } = default!;
    public string ContactPhone { get; set; } = default!;
    public BloodType BloodTypePledged { get; set; }
    public DateTime RegisteredAtUtc { get; set; } = DateTime.UtcNow;
    public bool Attended { get; set; } = false;
}
