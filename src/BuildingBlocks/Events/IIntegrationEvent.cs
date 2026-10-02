namespace ParkingSystem.BuildingBlocks.Events;

public interface IIntegrationEvent
{
    Guid EventId { get; }
    string EventType { get; }
    int EventVersion { get; }
    DateTimeOffset OccurredAt { get; }
    Guid CorrelationId { get; }
    Guid? CausationId { get; }
    string Source { get; }
}

public sealed record IntegrationEventEnvelope<TPayload>(
    Guid EventId,
    string EventType,
    int EventVersion,
    DateTimeOffset OccurredAt,
    Guid CorrelationId,
    Guid? CausationId,
    string Source,
    TPayload Payload);
