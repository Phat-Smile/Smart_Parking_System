using ParkingSystem.Identity.Application.Common.Auditing;
using ParkingSystem.Identity.Domain.AuditLogs;

namespace ParkingSystem.Identity.Persistence.Auditing;

public sealed class IdentityAuditLog(IdentityDbContext dbContext) : IIdentityAuditLog
{
    public void Record(
        string actionType,
        Guid? actorId,
        string? actorEmail,
        string actorRole,
        string targetType,
        string targetId,
        string reason,
        string? ipAddress = null,
        string? details = null)
    {
        var log = AuditLog.Create(
            Guid.NewGuid(),
            DateTime.UtcNow,
            actionType,
            actorId,
            actorEmail,
            actorRole,
            null,
            targetType,
            targetId,
            reason,
            ipAddress,
            details);

        dbContext.AuditLogs.Add(log);
    }
}
