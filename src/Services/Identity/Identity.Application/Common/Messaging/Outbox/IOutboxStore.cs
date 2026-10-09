namespace ParkingSystem.Identity.Application.Common.Messaging.Outbox;

public sealed record OutboxDispatchRecord(
    Guid Id,
    string EventType,
    int EventVersion,
    string Payload,
    DateTimeOffset OccurredAtUtc,
    int RetryCount,
    Guid? CorrelationId,
    Guid? CausationId,
    Guid LockId);

public interface IOutboxStore
{
    Task<IReadOnlyList<OutboxDispatchRecord>> ClaimPendingAsync(
        Guid lockId, DateTimeOffset nowUtc, DateTimeOffset lockedUntilUtc, int batchSize, CancellationToken cancellationToken);
    // redactedPayloadKeys: top-level payload fields removed once the event is published (e.g. one-time codes).
    Task<bool> MarkProcessedAsync(
        Guid id, Guid lockId, DateTimeOffset processedAtUtc, CancellationToken cancellationToken,
        IReadOnlyCollection<string>? redactedPayloadKeys = null);
    Task<bool> MarkFailedAsync(Guid id, Guid lockId, DateTimeOffset nextAttemptAtUtc, string error, CancellationToken cancellationToken);
}

