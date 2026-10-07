namespace ParkingSystem.Parking.Application.Common.Auditing;

// NFR-16: sensitive staff actions record who, when and why. Each service keeps its own audit log.
public interface IAuditLog
{
    /// <summary>Adds an entry that is saved in the same transaction as the business change it describes.</summary>
    void Record(AuditEntry entry);
}

/// <param name="ActorUserId">Account id of the staff member (token "sub", identity.users.id).</param>
/// <param name="Details">Extra values such as the status change. Never put a full plate number here (NFR-15).</param>
public sealed record AuditEntry(
    string Action,
    Guid ActorUserId,
    Guid? LotId,
    string TargetType,
    string TargetId,
    string? Reason,
    DateTimeOffset OccurredAt,
    IReadOnlyDictionary<string, string>? Details = null);

public static class AuditActions
{
    public const string SessionCancelled = "session.cancelled";
}
