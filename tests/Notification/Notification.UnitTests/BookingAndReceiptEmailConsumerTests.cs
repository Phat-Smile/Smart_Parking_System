using MassTransit;
using MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;
using ParkingSystem.Contracts.Events.Booking;
using ParkingSystem.Contracts.Events.Parking;
using ParkingSystem.Notification.Application.Common.Email;
using ParkingSystem.Notification.Application.Common.Identity;
using ParkingSystem.Notification.Application.CustomerNotifications;
using ParkingSystem.Notification.Infrastructure.Consumers;
using Xunit;

namespace ParkingSystem.Tests.Notification.UnitTests;

public sealed class BookingAndReceiptEmailConsumerTests
{
    [Fact]
    public async Task ReservationConfirmedConsumer_sends_email_when_account_email_is_verified()
    {
        var store = new FakeCustomerNotificationStore();
        var emailSender = new FakeEmailSender();
        var emailLookup = new FakeEmailLookup();
        var userId = Guid.NewGuid();
        var reservationId = Guid.NewGuid();
        var appointmentTime = DateTimeOffset.UtcNow.AddHours(2);
        const decimal prepaidAmount = 20000m;

        emailLookup.Accounts[userId] = new UserAccountEmailInfo(userId, "verified.customer@parkwise.vn", IsEmailVerified: true);

        await using var provider = BuildHarness<ReservationConfirmedNotificationConsumer>(store, emailSender, emailLookup);
        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        var evt = new ReservationConfirmedIntegrationEvent(
            reservationId,
            userId,
            Guid.NewGuid(),
            "HoldWhileDriving",
            prepaidAmount,
            appointmentTime,
            DateTimeOffset.UtcNow);

        await harness.Bus.Publish(evt, TestContext.Current.CancellationToken);

        Assert.True(await harness.GetConsumerHarness<ReservationConfirmedNotificationConsumer>()
            .Consumed.Any<ReservationConfirmedIntegrationEvent>(TestContext.Current.CancellationToken));

        // In-app notification created
        Assert.Equal(userId, store.LastUserId);
        Assert.Equal("booking", store.LastType);

        // Email dispatched
        var email = Assert.Single(emailSender.SentReservationEmails);
        Assert.Equal("verified.customer@parkwise.vn", email.Email);
        Assert.Equal(reservationId, email.ReservationId);
        Assert.Equal(prepaidAmount, email.PrepaidAmount);
    }

    [Fact]
    public async Task ReservationConfirmedConsumer_skips_email_when_email_is_not_verified()
    {
        var store = new FakeCustomerNotificationStore();
        var emailSender = new FakeEmailSender();
        var emailLookup = new FakeEmailLookup();
        var userId = Guid.NewGuid();

        // Account exists but PendingEmailVerification (IsEmailVerified = false)
        emailLookup.Accounts[userId] = new UserAccountEmailInfo(userId, "pending.customer@parkwise.vn", IsEmailVerified: false);

        await using var provider = BuildHarness<ReservationConfirmedNotificationConsumer>(store, emailSender, emailLookup);
        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        var evt = new ReservationConfirmedIntegrationEvent(
            Guid.NewGuid(),
            userId,
            Guid.NewGuid(),
            "HoldWhileDriving",
            0m,
            DateTimeOffset.UtcNow.AddHours(1),
            DateTimeOffset.UtcNow);

        await harness.Bus.Publish(evt, TestContext.Current.CancellationToken);

        Assert.True(await harness.GetConsumerHarness<ReservationConfirmedNotificationConsumer>()
            .Consumed.Any<ReservationConfirmedIntegrationEvent>(TestContext.Current.CancellationToken));

        // In-app notification still created
        Assert.Equal(userId, store.LastUserId);

        // Email NOT sent
        Assert.Empty(emailSender.SentReservationEmails);
    }

    [Fact]
    public async Task ParkingSessionReceiptConsumer_sends_receipt_email_when_account_is_verified()
    {
        var store = new FakeCustomerNotificationStore();
        var emailSender = new FakeEmailSender();
        var emailLookup = new FakeEmailLookup();
        var accountId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var entryTime = DateTimeOffset.UtcNow.AddHours(-3);
        var exitTime = DateTimeOffset.UtcNow;
        const string maskedPlate = "51F-***.89";

        emailLookup.Accounts[accountId] = new UserAccountEmailInfo(accountId, "customer@parkwise.vn", IsEmailVerified: true);

        await using var provider = BuildHarness<ParkingSessionReceiptNotificationConsumer>(store, emailSender, emailLookup);
        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        var evt = new ParkingSessionReceiptIssuedIntegrationEvent(
            sessionId,
            Guid.NewGuid(),
            maskedPlate,
            30000,
            30000,
            0,
            entryTime,
            exitTime,
            accountId);

        await harness.Bus.Publish(evt, TestContext.Current.CancellationToken);

        Assert.True(await harness.GetConsumerHarness<ParkingSessionReceiptNotificationConsumer>()
            .Consumed.Any<ParkingSessionReceiptIssuedIntegrationEvent>(TestContext.Current.CancellationToken));

        // In-app notification created
        Assert.Equal(accountId, store.LastUserId);
        Assert.Equal("parking", store.LastType);

        // Email dispatched
        var receipt = Assert.Single(emailSender.SentReceiptEmails);
        Assert.Equal("customer@parkwise.vn", receipt.Email);
        Assert.Equal(sessionId, receipt.SessionId);
        Assert.Equal(maskedPlate, receipt.MaskedPlate);
        Assert.Equal(30000, receipt.TotalFee);
        Assert.Equal(30000, receipt.PaidAmount);
        Assert.Equal(0, receipt.AmountDue);
    }

    [Fact]
    public async Task ParkingSessionReceiptConsumer_skips_email_for_unregistered_guest_session()
    {
        var store = new FakeCustomerNotificationStore();
        var emailSender = new FakeEmailSender();
        var emailLookup = new FakeEmailLookup();

        await using var provider = BuildHarness<ParkingSessionReceiptNotificationConsumer>(store, emailSender, emailLookup);
        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        // Guest / unregistered walk-in (AccountId = null)
        var evt = new ParkingSessionReceiptIssuedIntegrationEvent(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "59X-***.12",
            15000,
            15000,
            0,
            DateTimeOffset.UtcNow.AddHours(-1),
            DateTimeOffset.UtcNow,
            AccountId: null);

        await harness.Bus.Publish(evt, TestContext.Current.CancellationToken);

        Assert.True(await harness.GetConsumerHarness<ParkingSessionReceiptNotificationConsumer>()
            .Consumed.Any<ParkingSessionReceiptIssuedIntegrationEvent>(TestContext.Current.CancellationToken));

        // No in-app notice, no email
        Assert.Null(store.LastUserId);
        Assert.Empty(emailSender.SentReceiptEmails);
    }

    private static ServiceProvider BuildHarness<TConsumer>(
        ICustomerNotificationStore store,
        IEmailSender emailSender,
        IUserAccountEmailLookup emailLookup) where TConsumer : class, IConsumer
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(store);
        services.AddSingleton(emailSender);
        services.AddSingleton(emailLookup);
        services.AddMassTransitTestHarness(x => x.AddConsumer<TConsumer>());
        return services.BuildServiceProvider(true);
    }

    private sealed class FakeCustomerNotificationStore : ICustomerNotificationStore
    {
        public Guid? LastUserId { get; private set; }
        public string? LastType { get; private set; }

        public Task AddIfAbsentAsync(Guid userId, string sourceEventKey, string type, string title,
            string description, string href, DateTimeOffset createdAtUtc, CancellationToken ct)
        {
            LastUserId = userId;
            LastType = type;
            return Task.CompletedTask;
        }

        public Task<CustomerNotificationPage> ListAsync(Guid userId, bool unreadOnly, int page, int pageSize, CancellationToken ct) =>
            Task.FromResult(new CustomerNotificationPage([], 0, 0, page, pageSize));
        public Task<bool> MarkReadAsync(Guid userId, Guid notificationId, DateTimeOffset now, CancellationToken ct) => Task.FromResult(false);
        public Task<int> MarkAllReadAsync(Guid userId, DateTimeOffset now, CancellationToken ct) => Task.FromResult(0);
    }

    private sealed class FakeEmailLookup : IUserAccountEmailLookup
    {
        public Dictionary<Guid, UserAccountEmailInfo> Accounts { get; } = [];

        public Task<UserAccountEmailInfo?> LookupAccountEmailAsync(Guid accountId, CancellationToken cancellationToken)
        {
            Accounts.TryGetValue(accountId, out var info);
            return Task.FromResult(info);
        }
    }

    private sealed class FakeEmailSender : IEmailSender
    {
        public List<(string Email, Guid ReservationId, DateTimeOffset AppointmentTime, decimal PrepaidAmount)> SentReservationEmails { get; } = [];
        public List<(string Email, Guid SessionId, string MaskedPlate, long TotalFee, long PaidAmount, long AmountDue)> SentReceiptEmails { get; } = [];

        public Task SendReservationConfirmedEmailAsync(string email, Guid reservationId, DateTimeOffset appointmentTime, decimal prepaidAmount, CancellationToken cancellationToken = default)
        {
            SentReservationEmails.Add((email, reservationId, appointmentTime, prepaidAmount));
            return Task.CompletedTask;
        }

        public Task SendElectronicReceiptEmailAsync(string email, Guid sessionId, string maskedPlateNumber, long totalFee, long paidAmount, long amountDue, DateTimeOffset entryTimeUtc, DateTimeOffset exitTimeUtc, CancellationToken cancellationToken = default)
        {
            SentReceiptEmails.Add((email, sessionId, maskedPlateNumber, totalFee, paidAmount, amountDue));
            return Task.CompletedTask;
        }

        public Task SendVerificationOtpEmailAsync(string email, string otpCode, DateTimeOffset expiresAtUtc, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SendPasswordResetOtpEmailAsync(string email, string otpCode, DateTimeOffset expiresAtUtc, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SendOwnerApplicationApprovedEmailAsync(string email, DateTimeOffset approvedAtUtc, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SendOwnerApplicationRejectedEmailAsync(string email, string reason, DateTimeOffset rejectedAtUtc, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
