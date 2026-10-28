using Microsoft.Extensions.DependencyInjection;
using ParkingSystem.Payment.Application;
using ParkingSystem.Payment.Application.Common.Messaging.Inbox;
using ParkingSystem.Payment.Application.Common.Messaging.Outbox;
using ParkingSystem.Payment.Application.Features.Wallets.Queries.GetMyWallet;
using Xunit;

namespace ParkingSystem.Tests.Payment.UnitTests.Common;

public class ApplicationDependencyInjectionTests
{
    [Fact]
    public void AddApplicationServices_ShouldRegisterServicesSuccessfully()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddApplicationServices();
        var provider = services.BuildServiceProvider();

        // Assert
        Assert.NotNull(provider);
    }

    [Fact]
    public void DtoAndDescriptors_ShouldHoldPropertiesCorrectly()
    {
        // Arrange & Act
        var now = DateTimeOffset.UtcNow;
        var pendingTopUp = new PendingTopUpDto
        {
            TopupId = "TXN01",
            Amount = 50000m,
            CreatedAtUtc = now
        };

        var msgId = Guid.NewGuid();
        var correlationId = Guid.NewGuid();
        var causationId = Guid.NewGuid();
        var inboxDesc = new InboxMessageDescriptor(msgId, "OrderConsumer", now, correlationId, causationId);

        var recordId = Guid.NewGuid();
        var lockId = Guid.NewGuid();
        var outboxRecord = new OutboxDispatchRecord(
            recordId,
            "PaymentCompleted",
            1,
            "{}",
            now,
            0,
            correlationId,
            causationId,
            lockId);

        // Assert
        Assert.Equal("TXN01", pendingTopUp.TopupId);
        Assert.Equal(50000m, pendingTopUp.Amount);
        Assert.Equal(now, pendingTopUp.CreatedAtUtc);

        Assert.Equal(msgId, inboxDesc.MessageId);
        Assert.Equal("OrderConsumer", inboxDesc.ConsumerKey);
        Assert.Equal(now, inboxDesc.ReceivedAtUtc);
        Assert.Equal(correlationId, inboxDesc.CorrelationId);
        Assert.Equal(causationId, inboxDesc.CausationId);

        Assert.Equal(recordId, outboxRecord.Id);
        Assert.Equal("PaymentCompleted", outboxRecord.EventType);
        Assert.Equal(1, outboxRecord.EventVersion);
        Assert.Equal("{}", outboxRecord.Payload);
        Assert.Equal(now, outboxRecord.OccurredAtUtc);
        Assert.Equal(0, outboxRecord.RetryCount);
        Assert.Equal(correlationId, outboxRecord.CorrelationId);
        Assert.Equal(causationId, outboxRecord.CausationId);
        Assert.Equal(lockId, outboxRecord.LockId);
    }
}
