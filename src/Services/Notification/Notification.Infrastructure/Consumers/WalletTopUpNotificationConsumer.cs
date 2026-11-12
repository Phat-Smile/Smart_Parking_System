using MassTransit;
using ParkingSystem.Contracts.Events.Payment;
using ParkingSystem.Notification.Application.CustomerNotifications;

namespace ParkingSystem.Notification.Infrastructure.Consumers;

public sealed class WalletTopUpNotificationConsumer(ICustomerNotificationStore notifications)
    : IConsumer<WalletTopUpCompletedIntegrationEvent>
{
    public Task Consume(ConsumeContext<WalletTopUpCompletedIntegrationEvent> context)
    {
        var message = context.Message;
        return notifications.AddIfAbsentAsync(message.UserId, $"wallet-top-up:{message.TxnRef}",
            "wallet", "Wallet top-up completed", "Your wallet balance was updated.",
            "/customer/wallet", message.CompletedAtUtc, context.CancellationToken);
    }
}
