using LifeLinkLanka.Application.Interfaces;
using LifeLinkLanka.Domain.Entities;
using LifeLinkLanka.Infrastructure.Persistence;

namespace LifeLinkLanka.Infrastructure.Services;

public class AuditService : IAuditService
{
    private readonly ApplicationDbContext _db;
    public AuditService(ApplicationDbContext db) => _db = db;

    public async Task LogAsync(Guid? actorUserId, string action, string? details = null, string? ipAddress = null)
    {
        _db.AuditLogs.Add(new AuditLog
        {
            ActorUserId = actorUserId,
            Action = action,
            Details = details,
            IpAddress = ipAddress
        });
        await _db.SaveChangesAsync();
    }
}