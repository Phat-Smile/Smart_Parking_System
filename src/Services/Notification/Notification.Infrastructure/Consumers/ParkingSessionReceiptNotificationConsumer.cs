using MassTransit;
using Microsoft.Extensions.Logging;
using ParkingSystem.Contracts.Events.Parking;
using ParkingSystem.Notification.Application.Common.Email;
using ParkingSystem.Notification.Application.Common.Identity;
using ParkingSystem.Notification.Application.CustomerNotifications;

namespace ParkingSystem.Notification.Infrastructure.Consumers;

/// <summary>
/// Consumes ParkingSessionReceiptIssuedIntegrationEvent to dispatch electronic invoice / receipt emails.
/// Enforces Tin 200 Condition 6: Dispatches emails ONLY to authenticated accounts with verified email addresses.
/// Guests or unverified accounts receive in-app notifications only (or no-op).
/// </summary>
public sealed class ParkingSessionReceiptNotificationConsumer(
    ICustomerNotificationStore notifications,
    IEmailSender? emailSender = null,
    IUserAccountEmailLookup? emailLookup = null,
    ILogger<ParkingSessionReceiptNotificationConsumer>? logger = null)
    : IConsumer<ParkingSessionReceiptIssuedIntegrationEvent>
{
    public async Task Consume(ConsumeContext<ParkingSessionReceiptIssuedIntegrationEvent> context)
    {
        var message = context.Message;

        // 1. Unregistered walk-in / guest without account: skip email without failing
        if (message.AccountId is not { } accountId || accountId == Guid.Empty)
        {
            logger?.LogInformation(
                "Skipping receipt email for session {SessionId}: session has no authenticated account id.",
                message.SessionId);
            return;
        }

        // 2. In-app customer notification (idempotent)
        await notifications.AddIfAbsentAsync(
            accountId,
            $"parking-session-receipt:{message.SessionId}",
            "parking",
            "Parking receipt issued",
            $"Your parking fee receipt for session {message.SessionId} has been issued.",
            "/customer/sessions",
            message.ExitTimeUtc,
            context.CancellationToken);

        // 3. Email dispatch: Query Identity and only send if email is verified
        if (emailSender is not null && emailLookup is not null)
        {
            var account = await emailLookup.LookupAccountEmailAsync(accountId, context.CancellationToken);
            if (account is null || !account.IsEmailVerified)
            {
                logger?.LogInformation(
                    "Skipping electronic receipt email for session {SessionId}: account {AccountId} email not verified or account not found.",
                    message.SessionId,
                    accountId);
                return;
            }

            await emailSender.SendElectronicReceiptEmailAsync(
                account.Email,
                message.SessionId,
                message.MaskedPlateNumber,
                message.TotalFee,
                message.PaidAmount,
                message.AmountDue,
                message.EntryTimeUtc,
                message.ExitTimeUtc,
                context.CancellationToken);

            logger?.LogInformation(
                "Dispatched electronic receipt email for session {SessionId} to account {AccountId}.",
                message.SessionId,
                accountId);
        }
    }
}
