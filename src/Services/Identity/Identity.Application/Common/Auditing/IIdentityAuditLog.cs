namespace ParkingSystem.Identity.Application.Common.Auditing;

public interface IIdentityAuditLog
{
    void Record(
        string actionType,
        Guid? actorId,
        string? actorEmail,
        string actorRole,
        string targetType,
        string targetId,
        string reason,
        string? ipAddress = null,
        string? details = null);
}
