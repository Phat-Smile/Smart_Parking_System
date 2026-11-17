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
            $"Đặt chỗ của bạn đã được hủy. Lý do: {message.CancelReason}. Số tiền hoàn: {message.RefundAmount:N0} đ.",
            "/customer/bookings",
            message.CancelledAtUtc,
            context.CancellationToken);
    }
}
