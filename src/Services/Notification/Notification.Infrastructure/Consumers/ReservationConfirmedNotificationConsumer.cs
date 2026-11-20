using MassTransit;
using Microsoft.Extensions.Logging;
using ParkingSystem.Contracts.Events.Booking;
using ParkingSystem.Notification.Application.Common.Email;
using ParkingSystem.Notification.Application.Common.Identity;
using ParkingSystem.Notification.Application.CustomerNotifications;

namespace ParkingSystem.Notification.Infrastructure.Consumers;

public sealed class ReservationConfirmedNotificationConsumer(
    ICustomerNotificationStore notifications,
    IEmailSender? emailSender = null,
    IUserAccountEmailLookup? emailLookup = null,
    ILogger<ReservationConfirmedNotificationConsumer>? logger = null)
    : IConsumer<ReservationConfirmedIntegrationEvent>
{
    public async Task Consume(ConsumeContext<ReservationConfirmedIntegrationEvent> context)
    {
        var message = context.Message;

        // 1. In-app customer notification
        await notifications.AddIfAbsentAsync(message.UserId, $"reservation-confirmed:{message.ReservationId}",
            "booking", "Booking confirmed", "Your parking booking is confirmed.",
            "/customer/bookings", message.ConfirmedAtUtc, context.CancellationToken);

        // 2. Email confirmation: Only send to accounts with verified email
        if (emailSender is not null && emailLookup is not null)
        {
            var account = await emailLookup.LookupAccountEmailAsync(message.UserId, context.CancellationToken);
            if (account is null || !account.IsEmailVerified)
            {
                logger?.LogInformation(
                    "Skipped booking confirmation email for reservation {ReservationId}: account {UserId} email not verified or account not found.",
                    message.ReservationId,
                    message.UserId);
                return;
            }

            await emailSender.SendReservationConfirmedEmailAsync(
                account.Email,
                message.ReservationId,
                message.AppointmentTime,
                message.PrepaidAmount,
                context.CancellationToken);

            logger?.LogInformation(
                "Dispatched booking confirmation email for reservation {ReservationId} to account {UserId}.",
                message.ReservationId,
                message.UserId);
        }
    }
}
