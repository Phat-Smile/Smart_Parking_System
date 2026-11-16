namespace ParkingSystem.Identity.Domain.AuditLogs;

/// <summary>
/// Immutable append-only audit log entry for Identity service (NFR-16).
/// Records who did what, when, why, and sanitized non-PII details.
/// </summary>
public sealed class AuditLog
{
    public Guid Id { get; private set; }
    public long Seq { get; private set; }
    public DateTime OccurredAtUtc { get; private set; }
    public DateTime RecordedAtUtc { get; private set; }
    public string SourceService { get; private set; } = "identity";
    public Guid SourceEntryId { get; private set; }
    public string ActionType { get; private set; } = string.Empty;
    public Guid? ActorId { get; private set; }
    public string? ActorEmail { get; private set; }
    public string ActorRole { get; private set; } = string.Empty;
    public Guid? ParkingLotId { get; private set; }
    public string TargetType { get; private set; } = string.Empty;
    public string TargetId { get; private set; } = string.Empty;
    public string Reason { get; private set; } = string.Empty;
    public string? IpAddress { get; private set; }
    public string? Details { get; private set; }

    private AuditLog() { }

    public static AuditLog Create(
        Guid id,
        DateTime occurredAtUtc,
        string actionType,
        Guid? actorId,
        string? actorEmail,
        string actorRole,
        Guid? parkingLotId,
        string targetType,
        string targetId,
        string reason,
        string? ipAddress,
        string? details)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("Audit log id cannot be empty.", nameof(id));

        if (string.IsNullOrWhiteSpace(actionType))
            throw new ArgumentException("Action type cannot be empty.", nameof(actionType));

        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("Reason is mandatory for audit log.", nameof(reason));

        if (reason.Length > 500)
            throw new ArgumentException("Reason cannot exceed 500 characters.", nameof(reason));

        if (string.IsNullOrWhiteSpace(targetType) || string.IsNullOrWhiteSpace(targetId))
            throw new ArgumentException("Target type and Target id are required.", nameof(targetType));

        return new AuditLog
        {
            Id = id,
            OccurredAtUtc = occurredAtUtc,
            RecordedAtUtc = DateTime.UtcNow,
            SourceService = "identity",
            SourceEntryId = id,
            ActionType = actionType,
            ActorId = actorId,
            ActorEmail = actorEmail,
            ActorRole = string.IsNullOrWhiteSpace(actorRole) ? "System" : actorRole,
            ParkingLotId = parkingLotId,
            TargetType = targetType,
            TargetId = targetId,
            Reason = reason.Trim(),
            IpAddress = ipAddress,
            Details = details
        };
    }
}
