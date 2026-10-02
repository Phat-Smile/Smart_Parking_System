namespace ParkingSystem.Identity.Persistence.Outbox;

public sealed class OutboxMessage
{
    public Guid Id { get; set; }
    public string EventType { get; set; } = null!;
    public int EventVersion { get; set; } = 1;
    public string Payload { get; set; } = null!;
    public DateTimeOffset OccurredAtUtc { get; set; }
    public DateTimeOffset? ProcessedAtUtc { get; set; }
    public int RetryCount { get; set; }
    public string? Error { get; set; }
    public Guid? CorrelationId { get; set; }
    public Guid? CausationId { get; set; }
    public Guid? LockId { get; set; }
    public DateTimeOffset? LockedUntilUtc { get; set; }
    public DateTimeOffset? NextAttemptAtUtc { get; set; }
}
