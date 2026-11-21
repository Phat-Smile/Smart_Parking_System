using MassTransit;
using Microsoft.Extensions.Logging;
using ParkingSystem.Contracts.Events.Booking;
using ParkingSystem.Notification.Application.Common.Email;
using ParkingSystem.Notification.Application.Common.Identity;
using ParkingSystem.Notification.Application.CustomerNotifications;

namespace ParkingSystem.Notification.Infrastructure.Consumers;

public sealed class ReservationArrivalReminderNotificationConsumer(
    ICustomerNotificationStore notifications,
    IIdentityAccountClient identityClient,
    IEmailSender emailSender,
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

        // 2. Tra cứu email và trạng thái xác thực từ Identity gRPC client
        var accountInfo = await identityClient.GetAccountContactInfoAsync(message.AccountId, context.CancellationToken);

        // Chỉ gửi email nhắc nhở nếu IsEmailVerified == true
        if (accountInfo is not null && accountInfo.IsEmailVerified && !string.IsNullOrWhiteSpace(accountInfo.Email))
        {
            await emailSender.SendReservationArrivalReminderEmailAsync(
                accountInfo.Email,
                message.ReminderType,
                message.ScheduledArrivalTimeUtc,
                message.CutoffTimeUtc,
                message.MaskedPlateNumber,
                context.CancellationToken);

            logger.LogInformation(
                "Dispatched {ReminderType} reminder email to verified account {AccountId} for reservation {ReservationId}.",
                message.ReminderType, message.AccountId, message.ReservationId);
        }
        else
        {
            logger.LogInformation(
                "Skipped {ReminderType} email for reservation {ReservationId}: account {AccountId} has unverified or missing email.",
                message.ReminderType, message.ReservationId, message.AccountId);
        }
    }
}
