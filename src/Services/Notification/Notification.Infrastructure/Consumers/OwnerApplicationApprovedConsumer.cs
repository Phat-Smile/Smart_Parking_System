using MassTransit;
using Microsoft.Extensions.Logging;
using ParkingSystem.Contracts.Events.Identity;
using ParkingSystem.Notification.Application.Common.Email;

namespace ParkingSystem.Notification.Infrastructure.Consumers;

public sealed class OwnerApplicationApprovedConsumer(
    IEmailSender emailSender,
    ILogger<OwnerApplicationApprovedConsumer> logger) : IConsumer<OwnerApplicationApprovedIntegrationEvent>
{
    public async Task Consume(ConsumeContext<OwnerApplicationApprovedIntegrationEvent> context)
    {
        var message = context.Message;

        await emailSender.SendOwnerApplicationApprovedEmailAsync(
            message.Email,
            message.ApprovedAtUtc,
            context.CancellationToken);

        logger.LogInformation("Dispatched owner application approved email. AccountId={AccountId}", message.AccountId);
    }
}
