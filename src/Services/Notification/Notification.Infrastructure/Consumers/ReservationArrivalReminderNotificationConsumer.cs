using MassTransit;
using Microsoft.Extensions.Logging;
using ParkingSystem.Contracts.Events.Booking;
using ParkingSystem.Notification.Application.Common.Email;
using ParkingSystem.Notification.Application.Common.Identity;
using ParkingSystem.Notification.Application.CustomerNotifications;

namespace ParkingSystem.Notification.Infrastructure.Consumers;

public sealed class ReservationArrivalReminderNotificationConsumer(
    ICustomerNotificationStore notifications,
    IEmailSender emailSender,
    IUserAccountEmailLookup emailLookup,
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

        // 1. In-app notification
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

        // 2. Email notification
        if (message.AccountId == Guid.Empty)
        {
            logger.LogInformation(
                "Skipped arrival reminder email for reservation {ReservationId}: empty AccountId.",
                message.ReservationId);
            return;
        }

        var account = await emailLookup.LookupAccountEmailAsync(message.AccountId, context.CancellationToken);
        if (account is null || !account.IsEmailVerified)
        {
            logger.LogInformation(
                "Skipped arrival reminder email for reservation {ReservationId}: account {AccountId} email not verified or account not found.",
                message.ReservationId,
                message.AccountId);
            return;
        }

        await emailSender.SendReservationArrivalReminderEmailAsync(
            account.Email,
            message.ReminderType,
            message.ScheduledArrivalTimeUtc,
            message.CutoffTimeUtc,
            message.MaskedPlateNumber,
            context.CancellationToken);

        logger.LogInformation(
            "Dispatched {ReminderType} email for reservation {ReservationId} to account {AccountId}.",
            message.ReminderType,
            message.ReservationId,
            message.AccountId);
    }
}
