using MassTransit;
using MediatR;
using Microsoft.Extensions.Logging;
using ParkingSystem.Contracts.Events.Payment;
using ParkingSystem.Parking.Application.ParkingSessions.MarkParkingSessionPaid;

namespace ParkingSystem.Parking.Infrastructure.Messaging.Consumers;

// US-PAY-006. The Inbox filter drops a message delivered twice. ExitWindowExpiresUtc is not read: Parking computes the
// window itself and only uses the event's value for reconciliation (lead decision 2026-11-04).
public sealed class ParkingSessionPaidConsumer(ISender sender, ILogger<ParkingSessionPaidConsumer> logger)
    : IConsumer<ParkingSessionPaidIntegrationEvent>
{
    public async Task Consume(ConsumeContext<ParkingSessionPaidIntegrationEvent> context)
    {
        var message = context.Message;
        var result = await sender.Send(new MarkParkingSessionPaidCommand(message.SessionId, message.PaidAtUtc), context.CancellationToken);

        if (result.IsSuccess)
        {
            logger.LogInformation("Parking session {SessionId} is Paid by payment {PaymentId}.", message.SessionId, message.PaymentId);
            return;
        }

        // Retrying would not change the outcome: the session does not exist or is no longer Active.
        logger.LogWarning(
            "Payment {PaymentId} for parking session {SessionId} was ignored: {ErrorCode}.",
            message.PaymentId,
            message.SessionId,
            result.Error!.Code);
    }
}
