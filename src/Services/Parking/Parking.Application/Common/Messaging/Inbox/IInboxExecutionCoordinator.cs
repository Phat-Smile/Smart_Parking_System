namespace ParkingSystem.Parking.Application.Common.Messaging.Inbox;

public sealed record InboxMessageDescriptor(
    Guid MessageId,
    string ConsumerKey,
    DateTimeOffset ReceivedAtUtc,
    Guid? CorrelationId = null,
    Guid? CausationId = null);

public enum InboxExecutionResult
{
    Processed,
    Duplicate
}

public interface IInboxExecutionCoordinator
{
    Task<InboxExecutionResult> ExecuteAsync(
        InboxMessageDescriptor message,
        Func<CancellationToken, Task> handler,
        CancellationToken cancellationToken = default);
}
