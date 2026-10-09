using MassTransit;
using Microsoft.Extensions.Logging;
using ParkingSystem.Contracts.Events.Identity;
using ParkingSystem.Notification.Application.Common.Email;

namespace ParkingSystem.Notification.Infrastructure.Consumers;

public sealed class PasswordResetOtpIssuedConsumer(
    IEmailSender emailSender,
    ILogger<PasswordResetOtpIssuedConsumer> logger) : IConsumer<PasswordResetOtpIssuedIntegrationEvent>
{
    public async Task Consume(ConsumeContext<PasswordResetOtpIssuedIntegrationEvent> context)
    {
        var message = context.Message;

        await emailSender.SendPasswordResetOtpEmailAsync(
            message.Email,
            message.OtpCode,
            message.ExpiresAtUtc,
            context.CancellationToken);

        logger.LogInformation("Dispatched password reset email. AccountId={AccountId}", message.AccountId);
    }
}
