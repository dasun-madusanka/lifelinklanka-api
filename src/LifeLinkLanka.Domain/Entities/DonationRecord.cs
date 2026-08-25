using LifeLinkLanka.Domain.Common;

namespace LifeLinkLanka.Domain.Entities;

public class DonationRecord : BaseEntity
{
    public Guid DonorProfileId { get; set; }
    public DonorProfile DonorProfile { get; set; } = default!;
    public Guid? BloodBankId { get; set; }
    public DateTime DonationDateUtc { get; set; }
    public double VolumeMl { get; set; } = 450;
}