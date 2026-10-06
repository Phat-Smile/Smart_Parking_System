namespace ParkingSystem.Parking.Persistence.Auditing;

// Append-only row (ERD parking.audit_logs); there is no code path that updates or deletes audit entries.
public sealed class AuditLogEntry
{
    public Guid Id { get; set; }
    public Guid? LotId { get; set; }
    public Guid ActorUserId { get; set; }
    public string Action { get; set; } = null!;
    public string TargetType { get; set; } = null!;
    public string TargetId { get; set; } = null!;
    public string? Reason { get; set; }
    public string? Details { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
}
