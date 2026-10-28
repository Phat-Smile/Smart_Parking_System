using MassTransit;
using Microsoft.Extensions.Logging;
using ParkingSystem.Contracts.Events.Identity;
using ParkingSystem.Notification.Application.Common.Email;

namespace ParkingSystem.Notification.Infrastructure.Consumers;

public sealed class OwnerApplicationRejectedConsumer(
    IEmailSender emailSender,
    ILogger<OwnerApplicationRejectedConsumer> logger) : IConsumer<OwnerApplicationRejectedIntegrationEvent>
{
    public async Task Consume(ConsumeContext<OwnerApplicationRejectedIntegrationEvent> context)
    {
        var message = context.Message;

        await emailSender.SendOwnerApplicationRejectedEmailAsync(
            message.Email,
            message.Reason,
            message.RejectedAtUtc,
            context.CancellationToken);

        logger.LogInformation("Dispatched owner application rejected email. AccountId={AccountId}", message.AccountId);
    }
}
