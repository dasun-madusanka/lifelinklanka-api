using LifeLinkLanka.Domain.Common;

namespace LifeLinkLanka.Domain.Entities;

public class AuditLog : BaseEntity
{
    public Guid? ActorUserId { get; set; }
    public string Action { get; set; } = default!;   // "USER_LOGIN", "MFA_ENABLED", "REQUEST_CREATED"
    public string? Details { get; set; }
    public string? IpAddress { get; set; }
}