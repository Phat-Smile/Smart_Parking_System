using MassTransit;
using Microsoft.Extensions.Logging;
using ParkingSystem.Contracts.Events.Booking;
using ParkingSystem.Notification.Application.CustomerNotifications;

namespace ParkingSystem.Notification.Infrastructure.Consumers;

public sealed class ReservationArrivalReminderNotificationConsumer(
    ICustomerNotificationStore notifications,
    ILogger<ReservationArrivalReminderNotificationConsumer> logger)
    : IConsumer<ReservationArrivalReminderIntegrationEvent>
{
    public async Task Consume(ConsumeContext<ReservationArrivalReminderIntegrationEvent> context)
    {
        var message = context.Message;

        var isNoShowWarning = string.Equals(message.ReminderType, ReservationArrivalReminderTypes.FiveMinutesBeforeNoShow, StringComparison.OrdinalIgnoreCase);

        var title = isNoShowWarning
            ? "Cảnh báo: Sắp hết hạn nhận chỗ"
            : "Nhắc nhở: Sắp đến giờ nhận chỗ";

        var description = isNoShowWarning
            ? $"Đơn đặt chỗ xe {message.MaskedPlateNumber ?? "của bạn"} sẽ hết hạn nhận chỗ lúc {message.CutoffTimeUtc.ToLocalTime():HH:mm}. Vui lòng đến bãi trước thời điểm này để tránh bị hủy đơn và mất phí cọc."
            : $"Đơn đặt chỗ xe {message.MaskedPlateNumber ?? "của bạn"} dự kiến đến lúc {message.ScheduledArrivalTimeUtc.ToLocalTime():HH:mm}. Vui lòng chuẩn bị di chuyển đến bãi đúng giờ.";

        // In-app notification only
        await notifications.AddIfAbsentAsync(
            message.AccountId,
            $"reservation-arrival-reminder:{message.ReservationId}:{message.ReminderType}",
            "booking",
            title,
            description,
            "/customer/bookings",
            DateTimeOffset.UtcNow,
            context.CancellationToken);

        logger.LogInformation(
            "Created in-app {ReminderType} notification for account {AccountId}, reservation {ReservationId}.",
            message.ReminderType, message.AccountId, message.ReservationId);
    }
}
