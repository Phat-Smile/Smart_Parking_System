using System.Collections.Concurrent;
using MassTransit;

namespace ParkingSystem.Testing;

public sealed record InboxRegistrationProbeMessage(string Value);

public sealed class InboxRegistrationProbeCapture
{
    private readonly ConcurrentDictionary<Guid, int> _counts = new();

    public int Count(Guid messageId) => _counts.TryGetValue(messageId, out var count) ? count : 0;

    public void Record(Guid messageId) => _counts.AddOrUpdate(messageId, 1, static (_, count) => count + 1);
}

public sealed class InboxRegistrationProbeConsumer(InboxRegistrationProbeCapture capture) : IConsumer<InboxRegistrationProbeMessage>
{
    public Task Consume(ConsumeContext<InboxRegistrationProbeMessage> context)
    {
        if (context.MessageId is { } messageId)
            capture.Record(messageId);
        return Task.CompletedTask;
    }
}
