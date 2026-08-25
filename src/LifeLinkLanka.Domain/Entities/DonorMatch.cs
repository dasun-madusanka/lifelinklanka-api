using LifeLinkLanka.Domain.Common;

namespace LifeLinkLanka.Domain.Entities;

public class DonorMatch : BaseEntity
{
    public Guid BloodRequestId { get; set; }
    public BloodRequest BloodRequest { get; set; } = default!;

    public Guid DonorUserId { get; set; }
    public bool NotifiedViaRealtime { get; set; }
    public bool DonorResponded { get; set; }
    public bool DonorAccepted { get; set; }
    public DateTime? RespondedAtUtc { get; set; }
}