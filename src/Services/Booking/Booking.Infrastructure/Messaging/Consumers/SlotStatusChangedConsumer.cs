using MassTransit;
using MediatR;
using Microsoft.Extensions.Logging;
using ParkingSystem.Booking.Application.Reservations.Commands.HandleSlotStatusChanged;
using ParkingSystem.Contracts.Events.Parking;

namespace ParkingSystem.Booking.Infrastructure.Messaging.Consumers;

public sealed class SlotStatusChangedConsumer(
    ISender sender,
    ILogger<SlotStatusChangedConsumer> logger)
    : IConsumer<SlotStatusChangedIntegrationEvent>
{
    public async Task Consume(ConsumeContext<SlotStatusChangedIntegrationEvent> context)
    {
        var message = context.Message;
        logger.LogInformation(
            "Consuming SlotStatusChangedIntegrationEvent: EventId={EventId}, LotId={LotId}, SlotCode={SlotCode}, Status={Status}, Version={Version}.",
            message.EventId,
            message.LotId,
            message.SlotCode,
            message.EffectiveStatus,
            message.Version);

        var command = new HandleSlotStatusChangedCommand(
            message.LotId,
            message.ZoneId,
            message.SlotId,
            message.SlotCode,
            message.EffectiveStatus,
            message.PreviousStatus,
            message.Source,
            message.OccurredAtUtc,
            message.Version);

        var result = await sender.Send(command, context.CancellationToken);
        if (result.IsSuccess)
        {
            logger.LogInformation(
                "SlotStatusChangedConsumer handled successfully for slot {SlotCode}: {Message}",
                message.SlotCode,
                result.Value!.Message);
        }
        else
        {
            logger.LogWarning(
                "SlotStatusChangedConsumer encountered warning for slot {SlotCode}: {Error}",
                message.SlotCode,
                result.Error!.Message);
        }
    }
}
