using Microsoft.EntityFrameworkCore;
using ParkingSystem.Parking.Application.Common.Messaging.Inbox;
using ParkingSystem.Parking.Persistence.Inbox;

namespace ParkingSystem.Parking.Persistence.Inbox;

internal sealed class ParkingInboxExecutionCoordinator(ParkingDbContext dbContext) : IInboxExecutionCoordinator
{
    public async Task<InboxExecutionResult> ExecuteAsync(
        InboxMessageDescriptor message,
        Func<CancellationToken, Task> handler,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(handler);
        if (message.MessageId == Guid.Empty)
            throw new ArgumentException("An Inbox message requires a non-empty MessageId.", nameof(message));
        if (string.IsNullOrWhiteSpace(message.ConsumerKey) || message.ConsumerKey.Length > 512)
            throw new ArgumentException("ConsumerKey must contain between 1 and 512 characters.", nameof(message));
        if (dbContext.Database.CurrentTransaction is not null)
            throw new InvalidOperationException("Inbox execution must own the service-local database transaction.");

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var inserted = await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "parking"."inbox_messages"
                ("message_id", "consumer_key", "received_at_utc", "processed_at_utc")
            VALUES
                ({message.MessageId}, {message.ConsumerKey}, {message.ReceivedAtUtc}, {message.ReceivedAtUtc})
            ON CONFLICT ("message_id", "consumer_key") DO NOTHING
            """, cancellationToken);

        if (inserted == 0)
        {
            await transaction.RollbackAsync(cancellationToken);
            return InboxExecutionResult.Duplicate;
        }

        try
        {
            await handler(cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
            var processedAtUtc = DateTimeOffset.UtcNow;
            var updated = await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
                UPDATE "parking"."inbox_messages"
                SET "processed_at_utc" = {processedAtUtc}
                WHERE "message_id" = {message.MessageId} AND "consumer_key" = {message.ConsumerKey}
                """, cancellationToken);
            if (updated != 1)
                throw new InvalidOperationException("The Inbox claim disappeared before it could be marked processed.");

            await transaction.CommitAsync(cancellationToken);
            return InboxExecutionResult.Processed;
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }
}