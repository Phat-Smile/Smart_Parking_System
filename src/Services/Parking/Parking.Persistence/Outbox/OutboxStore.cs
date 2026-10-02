using Microsoft.EntityFrameworkCore;
using ParkingSystem.Parking.Application.Common.Messaging.Outbox;

namespace ParkingSystem.Parking.Persistence.Outbox;

internal sealed class OutboxStore(ParkingDbContext dbContext) : IOutboxStore
{
    public async Task<IReadOnlyList<OutboxDispatchRecord>> ClaimPendingAsync(
        Guid lockId, DateTimeOffset nowUtc, DateTimeOffset lockedUntilUtc, int batchSize, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(lockId, Guid.Empty);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(batchSize);
        if (lockedUntilUtc <= nowUtc)
            throw new ArgumentException("The lease expiry must be later than the claim time.", nameof(lockedUntilUtc));

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var messages = await dbContext.OutboxMessages
            .FromSqlRaw(
                """
                SELECT *
                FROM "parking"."outbox_messages"
                WHERE processed_at_utc IS NULL
                  AND (next_attempt_at_utc IS NULL OR next_attempt_at_utc <= {0})
                  AND (locked_until_utc IS NULL OR locked_until_utc <= {0})
                ORDER BY occurred_at_utc ASC, id ASC
                LIMIT {1}
                FOR UPDATE SKIP LOCKED
                """,
                nowUtc, batchSize)
            .ToListAsync(cancellationToken);

        foreach (var message in messages)
        {
            message.LockId = lockId;
            message.LockedUntilUtc = lockedUntilUtc;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return messages.Select(message => new OutboxDispatchRecord(
            message.Id, message.EventType, message.EventVersion, message.Payload, message.OccurredAtUtc,
            message.RetryCount, message.CorrelationId, message.CausationId, lockId)).ToArray();
    }

    public async Task<bool> MarkProcessedAsync(Guid id, Guid lockId, DateTimeOffset processedAtUtc, CancellationToken cancellationToken)
    {
        var updated = await dbContext.OutboxMessages
            .Where(message => message.Id == id && message.LockId == lockId && message.ProcessedAtUtc == null)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(message => message.ProcessedAtUtc, processedAtUtc)
                .SetProperty(message => message.LockId, (Guid?)null)
                .SetProperty(message => message.LockedUntilUtc, (DateTimeOffset?)null)
                .SetProperty(message => message.NextAttemptAtUtc, (DateTimeOffset?)null)
                .SetProperty(message => message.Error, (string?)null), cancellationToken);
        return updated == 1;
    }

    public async Task<bool> MarkFailedAsync(Guid id, Guid lockId, DateTimeOffset nextAttemptAtUtc, string error, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(error);
        var safeError = error.Length <= 512 ? error : error[..512];
        var updated = await dbContext.OutboxMessages
            .Where(message => message.Id == id && message.LockId == lockId && message.ProcessedAtUtc == null)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(message => message.RetryCount, message => message.RetryCount + 1)
                .SetProperty(message => message.Error, safeError)
                .SetProperty(message => message.NextAttemptAtUtc, nextAttemptAtUtc)
                .SetProperty(message => message.LockId, (Guid?)null)
                .SetProperty(message => message.LockedUntilUtc, (DateTimeOffset?)null), cancellationToken);
        return updated == 1;
    }
}

