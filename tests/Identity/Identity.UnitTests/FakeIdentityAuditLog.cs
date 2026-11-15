using ParkingSystem.Identity.Application.Common.Auditing;

namespace ParkingSystem.Tests.Identity.UnitTests;

public sealed record AuditLogTestEntry(
    string ActionType,
    Guid? ActorId,
    string? ActorEmail,
    string ActorRole,
    string TargetType,
    string TargetId,
    string Reason,
    string? IpAddress,
    string? Details);

public sealed class FakeIdentityAuditLog : IIdentityAuditLog
{
    public List<AuditLogTestEntry> Entries { get; } = [];

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
        Entries.Add(new(actionType, actorId, actorEmail, actorRole, targetType, targetId, reason, ipAddress, details));
    }
}
