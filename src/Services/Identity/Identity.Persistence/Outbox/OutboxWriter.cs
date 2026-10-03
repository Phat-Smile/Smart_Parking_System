using System.Text.Json;
using ParkingSystem.BuildingBlocks.Time;
using ParkingSystem.Identity.Application.Common.Messaging.Outbox;

namespace ParkingSystem.Identity.Persistence.Outbox;

internal sealed class OutboxWriter(IdentityDbContext dbContext, IClock clock) : IOutboxWriter
{
    public void Enqueue<TEvent>(
        TEvent integrationEvent,
        string eventType,
        int eventVersion,
        Guid? correlationId = null,
        Guid? causationId = null)
        where TEvent : class
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);
        ArgumentException.ThrowIfNullOrWhiteSpace(eventType);
        ArgumentOutOfRangeException.ThrowIfLessThan(eventVersion, 1);

        // Serialized with default options, matching the dispatcher's deserialization.
        dbContext.OutboxMessages.Add(new OutboxMessage
        {
            Id = Guid.NewGuid(),
            EventType = eventType,
            EventVersion = eventVersion,
            Payload = JsonSerializer.Serialize(integrationEvent, integrationEvent.GetType()),
            OccurredAtUtc = clock.UtcNow,
            CorrelationId = correlationId,
            CausationId = causationId
        });
    }
}
