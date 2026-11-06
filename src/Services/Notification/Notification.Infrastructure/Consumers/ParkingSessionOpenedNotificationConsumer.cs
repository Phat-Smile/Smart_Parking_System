using MassTransit;
using ParkingSystem.Contracts.Events.Parking;
using ParkingSystem.Notification.Application.CustomerNotifications;

namespace ParkingSystem.Notification.Infrastructure.Consumers;

public sealed class ParkingSessionOpenedNotificationConsumer(ICustomerNotificationStore notifications)
    : IConsumer<ParkingSessionOpenedIntegrationEvent>
{
    public Task Consume(ConsumeContext<ParkingSessionOpenedIntegrationEvent> context)
    {
        var message = context.Message;
        return notifications.AddIfAbsentAsync(message.UserId, $"parking-session-opened:{message.SessionId}",
            "parking", "Parking session active", "Use your session QR code when leaving.",
            "/customer/sessions", message.EntryTimeUtc, context.CancellationToken);
    }
}
