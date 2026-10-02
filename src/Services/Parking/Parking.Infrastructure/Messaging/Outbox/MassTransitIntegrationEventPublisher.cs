using MassTransit;
using ParkingSystem.Parking.Application.Common.Messaging.Outbox;

namespace ParkingSystem.Parking.Infrastructure.Messaging.Outbox;

public interface IIntegrationEventPublisher
{
    Task PublishAsync(object message, Type messageType, OutboxDispatchRecord record, CancellationToken cancellationToken);
}

internal sealed class MassTransitIntegrationEventPublisher(IPublishEndpoint publishEndpoint) : IIntegrationEventPublisher
{
    public Task PublishAsync(object message, Type messageType, OutboxDispatchRecord record, CancellationToken cancellationToken) =>
        publishEndpoint.Publish(message, messageType, Pipe.Execute<PublishContext>(context =>
        {
            context.MessageId = record.Id;
            if (record.CorrelationId.HasValue)
                context.CorrelationId = record.CorrelationId;
            if (record.CausationId.HasValue)
                context.InitiatorId = record.CausationId;
            context.Headers.Set("event-type", record.EventType);
            context.Headers.Set("event-version", record.EventVersion);
        }), cancellationToken);
}

