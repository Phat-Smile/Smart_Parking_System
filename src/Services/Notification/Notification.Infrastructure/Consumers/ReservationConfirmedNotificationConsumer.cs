using MassTransit;
using ParkingSystem.Contracts.Events.Booking;
using ParkingSystem.Notification.Application.CustomerNotifications;

namespace ParkingSystem.Notification.Infrastructure.Consumers;

public sealed class ReservationConfirmedNotificationConsumer(ICustomerNotificationStore notifications)
    : IConsumer<ReservationConfirmedIntegrationEvent>
{
    public Task Consume(ConsumeContext<ReservationConfirmedIntegrationEvent> context)
    {
        var message = context.Message;
        return notifications.AddIfAbsentAsync(message.UserId, $"reservation-confirmed:{message.ReservationId}",
            "booking", "Booking confirmed", "Your parking booking is confirmed.",
            "/customer/bookings", message.ConfirmedAtUtc, context.CancellationToken);
    }
}
