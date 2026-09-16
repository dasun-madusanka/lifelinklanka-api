using LifeLinkLanka.Domain.Common;
using LifeLinkLanka.Domain.Enums;

namespace LifeLinkLanka.Domain.Entities;

public class BloodInventory : BaseEntity
{
    public Guid BloodBankId { get; set; }
    public BloodBank BloodBank { get; set; } = default!;

    public BloodType BloodType { get; set; }
    public BloodComponentType ComponentType { get; set; } = BloodComponentType.WholeBlood;
    public int UnitsAvailable { get; set; }
    public string StorageLocation { get; set; } = "Cold Storage A";
    public string BatchNumber { get; set; } = default!;
    public DateTime ExpiryDateUtc { get; set; }
    public string Status { get; set; } = "Available"; // Available, Reserved, Expired, Quarantine
}
