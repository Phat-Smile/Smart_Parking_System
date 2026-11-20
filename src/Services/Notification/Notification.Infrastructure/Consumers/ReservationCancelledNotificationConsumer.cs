using MassTransit;
using ParkingSystem.Contracts.Events.Booking;
using ParkingSystem.Notification.Application.CustomerNotifications;

namespace ParkingSystem.Notification.Infrastructure.Consumers;

public sealed class ReservationCancelledNotificationConsumer(ICustomerNotificationStore notifications)
    : IConsumer<ReservationCancelledIntegrationEvent>
{
    public Task Consume(ConsumeContext<ReservationCancelledIntegrationEvent> context)
    {
        var message = context.Message;
        return notifications.AddIfAbsentAsync(
            message.UserId,
            $"reservation-cancelled:{message.ReservationId}",
            "booking",
            "Đặt chỗ đã bị hủy",
            ReservationCancelledNoticeText.Build(message),
            "/customer/bookings",
            message.CancelledAtUtc,
            context.CancellationToken);
    }
}
