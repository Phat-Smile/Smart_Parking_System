using MassTransit;
using Microsoft.Extensions.Logging;
using ParkingSystem.BuildingBlocks.Time;
using ParkingSystem.Contracts.Events.Identity;
using ParkingSystem.Notification.Application.Common.Email;

namespace ParkingSystem.Notification.Infrastructure.Consumers;

public sealed class PasswordResetOtpIssuedConsumer(
    IEmailSender emailSender,
    IClock clock,
    ILogger<PasswordResetOtpIssuedConsumer> logger) : IConsumer<PasswordResetOtpIssuedIntegrationEvent>
{
    public async Task Consume(ConsumeContext<PasswordResetOtpIssuedIntegrationEvent> context)
    {
        var message = context.Message;

        // A delayed or redelivered event must not mail a code the user can no longer use.
        if (message.ExpiresAtUtc <= clock.UtcNow)
        {
            logger.LogWarning("Skipped an expired password reset email. AccountId={AccountId}", message.AccountId);
            return;
        }

        await emailSender.SendPasswordResetOtpEmailAsync(
            message.Email,
            message.OtpCode,
            message.ExpiresAtUtc,
            context.CancellationToken);

        logger.LogInformation("Dispatched password reset email. AccountId={AccountId}", message.AccountId);
    }
}
