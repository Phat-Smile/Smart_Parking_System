using MassTransit;
using Microsoft.Extensions.Logging;
using ParkingSystem.BuildingBlocks.Events;
using ParkingSystem.Notification.Application.Common.Email;

namespace ParkingSystem.Notification.Infrastructure.Consumers;

public sealed class UserVerificationOtpIssuedConsumer(
    IEmailSender emailSender,
    ILogger<UserVerificationOtpIssuedConsumer> logger) : IConsumer<UserVerificationOtpIssuedIntegrationEvent>
{
    public async Task Consume(ConsumeContext<UserVerificationOtpIssuedIntegrationEvent> context)
    {
        var message = context.Message;
        logger.LogInformation(
            "Processing UserVerificationOtpIssued event. AccountId={AccountId}, Email={Email}",
            message.AccountId,
            message.Email);

        await emailSender.SendVerificationOtpEmailAsync(
            message.Email,
            message.OtpCode,
            message.ExpiresAtUtc,
            context.CancellationToken);

        logger.LogInformation(
            "Successfully dispatched OTP verification email. AccountId={AccountId}, Email={Email}",
            message.AccountId,
            message.Email);
    }
}
