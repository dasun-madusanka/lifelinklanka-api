namespace LifeLinkLanka.Application.Interfaces;

public interface IAuditService
{
    Task LogAsync(Guid? actorUserId, string action, string? details = null, string? ipAddress = null);
}