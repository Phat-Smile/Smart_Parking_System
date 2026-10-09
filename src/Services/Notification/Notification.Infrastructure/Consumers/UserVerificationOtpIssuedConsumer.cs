using MassTransit;
using Microsoft.Extensions.Logging;
using ParkingSystem.BuildingBlocks.Time;
using ParkingSystem.Contracts.Events.Identity;
using ParkingSystem.Notification.Application.Common.Email;

namespace ParkingSystem.Notification.Infrastructure.Consumers;

// Logs carry the account id only: the email address is personal data and the code is a secret.
public sealed class UserVerificationOtpIssuedConsumer(
    IEmailSender emailSender,
    IClock clock,
    ILogger<UserVerificationOtpIssuedConsumer> logger) : IConsumer<UserVerificationOtpIssuedIntegrationEvent>
{
    public async Task Consume(ConsumeContext<UserVerificationOtpIssuedIntegrationEvent> context)
    {
        var message = context.Message;

        // A delayed or redelivered event must not mail a code the user can no longer use.
        if (message.ExpiresAtUtc <= clock.UtcNow)
        {
            logger.LogWarning("Skipped an expired OTP verification email. AccountId={AccountId}", message.AccountId);
            return;
        }

        await emailSender.SendVerificationOtpEmailAsync(
            message.Email,
            message.OtpCode,
            message.ExpiresAtUtc,
            context.CancellationToken);

        logger.LogInformation("Dispatched OTP verification email. AccountId={AccountId}", message.AccountId);
    }
}
