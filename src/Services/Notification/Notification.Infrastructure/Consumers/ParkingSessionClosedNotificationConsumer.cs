using MassTransit;
using ParkingSystem.Contracts.Events.Parking;
using ParkingSystem.Notification.Application.CustomerNotifications;

namespace ParkingSystem.Notification.Infrastructure.Consumers;

public sealed class ParkingSessionClosedNotificationConsumer(ICustomerNotificationStore notifications)
    : IConsumer<ParkingSessionClosedIntegrationEvent>
{
    public Task Consume(ConsumeContext<ParkingSessionClosedIntegrationEvent> context)
    {
        var message = context.Message;
        if (message.UserId is not { } userId || userId == Guid.Empty) return Task.CompletedTask;
        return notifications.AddIfAbsentAsync(userId, $"parking-session-closed:{message.SessionId}",
            "parking", "Parking session completed", "Your parking session has ended.",
            "/customer/sessions", message.CompletedAtUtc, context.CancellationToken);
    }
}
