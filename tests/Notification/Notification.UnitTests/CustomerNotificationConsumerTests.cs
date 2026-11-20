using MassTransit;
using MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ParkingSystem.Contracts.Events.Parking;
using ParkingSystem.Contracts.Events.Booking;
using ParkingSystem.Contracts.Events.Payment;
using ParkingSystem.Notification.Application.Common.Email;
using ParkingSystem.Notification.Application.Common.Identity;
using ParkingSystem.Notification.Application.CustomerNotifications;
using ParkingSystem.Notification.Infrastructure.Consumers;
using Xunit;

namespace ParkingSystem.Tests.Notification.UnitTests;

public sealed class CustomerNotificationConsumerTests
{
    [Fact]
    public async Task Wallet_top_up_creates_customer_notice()
    {
        var store = new RecordingStore();
        await using var provider = Build<WalletTopUpNotificationConsumer>(store);
        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();
        var userId = Guid.NewGuid();
        await harness.Bus.Publish(new WalletTopUpCompletedIntegrationEvent(Guid.NewGuid(), userId,
            100000, 200000, "TX-1", DateTimeOffset.UtcNow), TestContext.Current.CancellationToken);
        Assert.True(await harness.GetConsumerHarness<WalletTopUpNotificationConsumer>()
            .Consumed.Any<WalletTopUpCompletedIntegrationEvent>(TestContext.Current.CancellationToken));
        Assert.Equal(userId, store.UserId);
        Assert.Equal("wallet", store.Type);
    }

    [Fact]
    public async Task Anonymous_parking_session_does_not_create_customer_notice()
    {
        var store = new RecordingStore();
        await using var provider = Build<ParkingSessionClosedNotificationConsumer>(store);
        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();
        var now = DateTimeOffset.UtcNow;
        await harness.Bus.Publish(new ParkingSessionClosedIntegrationEvent(Guid.NewGuid(), Guid.NewGuid(),
            "51A12345", "Car", now, now, now), TestContext.Current.CancellationToken);
        Assert.True(await harness.GetConsumerHarness<ParkingSessionClosedNotificationConsumer>()
            .Consumed.Any<ParkingSessionClosedIntegrationEvent>(TestContext.Current.CancellationToken));
        Assert.Null(store.UserId);
    }

    [Fact]
    public async Task Confirmed_reservation_creates_booking_notice_for_customer()
    {
        var store = new RecordingStore();
        await using var provider = Build<ReservationConfirmedNotificationConsumer>(store);
        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();
        var userId = Guid.NewGuid();
        await harness.Bus.Publish(new ReservationConfirmedIntegrationEvent(Guid.NewGuid(), userId,
            Guid.NewGuid(), DateTimeOffset.UtcNow), TestContext.Current.CancellationToken);
        Assert.True(await harness.GetConsumerHarness<ReservationConfirmedNotificationConsumer>()
            .Consumed.Any<ReservationConfirmedIntegrationEvent>(TestContext.Current.CancellationToken));
        Assert.Equal(userId, store.UserId);
        Assert.Equal("booking", store.Type);
    }

    [Fact]
    public async Task Opened_parking_session_creates_active_notice()
    {
        var store = new RecordingStore();
        await using var provider = Build<ParkingSessionOpenedNotificationConsumer>(store);
        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();
        var userId = Guid.NewGuid();
        await harness.Bus.Publish(new ParkingSessionOpenedIntegrationEvent(Guid.NewGuid(), userId,
            Guid.NewGuid(), DateTimeOffset.UtcNow), TestContext.Current.CancellationToken);
        Assert.True(await harness.GetConsumerHarness<ParkingSessionOpenedNotificationConsumer>()
            .Consumed.Any<ParkingSessionOpenedIntegrationEvent>(TestContext.Current.CancellationToken));
        Assert.Equal(userId, store.UserId);
        Assert.Equal("parking", store.Type);
    }

    private static ServiceProvider Build<T>(RecordingStore store) where T : class, IConsumer =>
        new ServiceCollection()
            .AddLogging()
            .AddSingleton<ICustomerNotificationStore>(store)
            .AddSingleton<IEmailSender>(new DummyEmailSender())
            .AddSingleton<IUserAccountEmailLookup>(new DummyEmailLookup())
            .AddMassTransitTestHarness(x => x.AddConsumer<T>())
            .BuildServiceProvider(true);

    private sealed class DummyEmailSender : IEmailSender
    {
        public Task SendReservationConfirmedEmailAsync(string email, Guid reservationId, DateTimeOffset appointmentTime, decimal prepaidAmount, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SendElectronicReceiptEmailAsync(string email, Guid sessionId, string maskedPlateNumber, long totalFee, long paidAmount, long amountDue, DateTimeOffset entryTimeUtc, DateTimeOffset exitTimeUtc, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SendVerificationOtpEmailAsync(string email, string otpCode, DateTimeOffset expiresAtUtc, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SendPasswordResetOtpEmailAsync(string email, string otpCode, DateTimeOffset expiresAtUtc, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SendOwnerApplicationApprovedEmailAsync(string email, DateTimeOffset approvedAtUtc, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SendOwnerApplicationRejectedEmailAsync(string email, string reason, DateTimeOffset rejectedAtUtc, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class DummyEmailLookup : IUserAccountEmailLookup
    {
        public Task<UserAccountEmailInfo?> LookupAccountEmailAsync(Guid accountId, CancellationToken cancellationToken) =>
            Task.FromResult<UserAccountEmailInfo?>(null);
    }

    private sealed class RecordingStore : ICustomerNotificationStore
    {
        public Guid? UserId { get; private set; }
        public string? Type { get; private set; }
        public Task AddIfAbsentAsync(Guid userId, string sourceEventKey, string type, string title,
            string description, string href, DateTimeOffset createdAtUtc, CancellationToken ct)
        {
            UserId = userId;
            Type = type;
            return Task.CompletedTask;
        }
        public Task<CustomerNotificationPage> ListAsync(Guid userId, bool unreadOnly, int page, int pageSize, CancellationToken ct) =>
            Task.FromResult(new CustomerNotificationPage([], 0, 0, page, pageSize));
        public Task<bool> MarkReadAsync(Guid userId, Guid notificationId, DateTimeOffset now, CancellationToken ct) => Task.FromResult(false);
        public Task<int> MarkAllReadAsync(Guid userId, DateTimeOffset now, CancellationToken ct) => Task.FromResult(0);
    }
}
