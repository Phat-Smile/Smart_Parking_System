using MassTransit;
using MassTransit.Testing;
using Microsoft.Extensions.Logging.Abstractions;
using ParkingSystem.Contracts.Events.Booking;
using ParkingSystem.Notification.Application.Common.Email;
using ParkingSystem.Notification.Application.Common.Identity;
using ParkingSystem.Notification.Application.CustomerNotifications;
using ParkingSystem.Notification.Infrastructure.Consumers;
using Xunit;

namespace ParkingSystem.Tests.Notification.UnitTests;

public sealed class ReservationArrivalReminderNotificationConsumerTests
{
    private static readonly DateTime ScheduledArrival = new(2026, 11, 20, 10, 30, 0, DateTimeKind.Utc);
    private static readonly DateTime CutoffTime = new(2026, 11, 20, 10, 46, 0, DateTimeKind.Utc);

    [Fact]
    public async Task ThirtyMinutesBeforeArrival_CreatesInAppNotice()
    {
        var store = new FakeNotificationStore();
        var emailSender = new FakeEmailSender();
        var emailLookup = new FakeEmailLookup();

        var consumer = new ReservationArrivalReminderNotificationConsumer(
            store,
            emailSender,
            emailLookup,
            NullLogger<ReservationArrivalReminderNotificationConsumer>.Instance);

        var accountId = Guid.NewGuid();
        var reservationId = Guid.NewGuid();
        var lotId = Guid.NewGuid();

        var message = new ReservationArrivalReminderIntegrationEvent(
            accountId,
            reservationId,
            lotId,
            ReservationArrivalReminderTypes.ThirtyMinutesBeforeArrival,
            ScheduledArrival,
            CutoffTime,
            "51A***45");

        var harness = new InMemoryTestHarness();
        var consumerHarness = harness.Consumer(() => consumer);

        await harness.Start(TestContext.Current.CancellationToken);
        try
        {
            await harness.Bus.Publish(message, TestContext.Current.CancellationToken);
            Assert.True(await consumerHarness.Consumed.Any<ReservationArrivalReminderIntegrationEvent>(TestContext.Current.CancellationToken));

            // Verify In-App Notification
            var notice = Assert.Single(store.Stored);
            Assert.Equal(accountId, notice.UserId);
            Assert.Equal($"reservation-arrival-reminder:{reservationId}:{ReservationArrivalReminderTypes.ThirtyMinutesBeforeArrival}", notice.SourceEventKey);
            Assert.Equal("booking", notice.Type);
            Assert.Equal("Nhắc nhở: Sắp đến giờ nhận chỗ", notice.Title);
            Assert.Equal("/customer/bookings", notice.Href);
            Assert.Contains("51A***45", notice.Description);
        }
        finally
        {
            await harness.Stop();
        }
    }

    [Fact]
    public async Task FiveMinutesBeforeNoShow_CreatesWarningInAppNotice()
    {
        var store = new FakeNotificationStore();
        var emailSender = new FakeEmailSender();
        var emailLookup = new FakeEmailLookup();

        var consumer = new ReservationArrivalReminderNotificationConsumer(
            store,
            emailSender,
            emailLookup,
            NullLogger<ReservationArrivalReminderNotificationConsumer>.Instance);

        var accountId = Guid.NewGuid();
        var reservationId = Guid.NewGuid();
        var lotId = Guid.NewGuid();

        var message = new ReservationArrivalReminderIntegrationEvent(
            accountId,
            reservationId,
            lotId,
            ReservationArrivalReminderTypes.FiveMinutesBeforeNoShow,
            ScheduledArrival,
            CutoffTime,
            "29A***99");

        var harness = new InMemoryTestHarness();
        var consumerHarness = harness.Consumer(() => consumer);

        await harness.Start(TestContext.Current.CancellationToken);
        try
        {
            await harness.Bus.Publish(message, TestContext.Current.CancellationToken);
            Assert.True(await consumerHarness.Consumed.Any<ReservationArrivalReminderIntegrationEvent>(TestContext.Current.CancellationToken));

            // Verify In-App Notification
            var notice = Assert.Single(store.Stored);
            Assert.Equal(accountId, notice.UserId);
            Assert.Equal($"reservation-arrival-reminder:{reservationId}:{ReservationArrivalReminderTypes.FiveMinutesBeforeNoShow}", notice.SourceEventKey);
            Assert.Equal("booking", notice.Type);
            Assert.Equal("Cảnh báo: Sắp hết hạn nhận chỗ", notice.Title);
            Assert.Contains("tránh bị hủy đơn", notice.Description);
            Assert.Contains("29A***99", notice.Description);
            Assert.Equal("/customer/bookings", notice.Href);
        }
        finally
        {
            await harness.Stop();
        }
    }

    [Fact]
    public async Task ThirtyMinutesBeforeArrival_WhenAccountVerified_SendsReminderEmail()
    {
        var store = new FakeNotificationStore();
        var emailSender = new FakeEmailSender();
        var emailLookup = new FakeEmailLookup();

        var accountId = Guid.NewGuid();
        var reservationId = Guid.NewGuid();
        var lotId = Guid.NewGuid();
        const string customerEmail = "customer.arrival@parkwise.vn";

        emailLookup.Accounts[accountId] = new UserAccountEmailInfo(accountId, customerEmail, IsEmailVerified: true);

        var consumer = new ReservationArrivalReminderNotificationConsumer(
            store,
            emailSender,
            emailLookup,
            NullLogger<ReservationArrivalReminderNotificationConsumer>.Instance);

        var message = new ReservationArrivalReminderIntegrationEvent(
            accountId,
            reservationId,
            lotId,
            ReservationArrivalReminderTypes.ThirtyMinutesBeforeArrival,
            ScheduledArrival,
            CutoffTime,
            "51A***45");

        var harness = new InMemoryTestHarness();
        var consumerHarness = harness.Consumer(() => consumer);

        await harness.Start(TestContext.Current.CancellationToken);
        try
        {
            await harness.Bus.Publish(message, TestContext.Current.CancellationToken);
            Assert.True(await consumerHarness.Consumed.Any<ReservationArrivalReminderIntegrationEvent>(TestContext.Current.CancellationToken));

            // In-app notice still created
            Assert.Single(store.Stored);

            // Email sent with expected payload
            var email = Assert.Single(emailSender.SentArrivalReminderEmails);
            Assert.Equal(customerEmail, email.Email);
            Assert.Equal(ReservationArrivalReminderTypes.ThirtyMinutesBeforeArrival, email.ReminderType);
            Assert.Equal(ScheduledArrival, email.ScheduledArrivalTimeUtc);
            Assert.Equal(CutoffTime, email.CutoffTimeUtc);
            Assert.Equal("51A***45", email.MaskedPlateNumber);
        }
        finally
        {
            await harness.Stop();
        }
    }

    [Fact]
    public async Task FiveMinutesBeforeNoShow_WhenAccountVerified_SendsWarningEmail()
    {
        var store = new FakeNotificationStore();
        var emailSender = new FakeEmailSender();
        var emailLookup = new FakeEmailLookup();

        var accountId = Guid.NewGuid();
        var reservationId = Guid.NewGuid();
        var lotId = Guid.NewGuid();
        const string customerEmail = "warned.customer@parkwise.vn";

        emailLookup.Accounts[accountId] = new UserAccountEmailInfo(accountId, customerEmail, IsEmailVerified: true);

        var consumer = new ReservationArrivalReminderNotificationConsumer(
            store,
            emailSender,
            emailLookup,
            NullLogger<ReservationArrivalReminderNotificationConsumer>.Instance);

        var message = new ReservationArrivalReminderIntegrationEvent(
            accountId,
            reservationId,
            lotId,
            ReservationArrivalReminderTypes.FiveMinutesBeforeNoShow,
            ScheduledArrival,
            CutoffTime,
            "29A***99");

        var harness = new InMemoryTestHarness();
        var consumerHarness = harness.Consumer(() => consumer);

        await harness.Start(TestContext.Current.CancellationToken);
        try
        {
            await harness.Bus.Publish(message, TestContext.Current.CancellationToken);
            Assert.True(await consumerHarness.Consumed.Any<ReservationArrivalReminderIntegrationEvent>(TestContext.Current.CancellationToken));

            // In-app notice created
            Assert.Single(store.Stored);

            // Warning email sent with expected payload
            var email = Assert.Single(emailSender.SentArrivalReminderEmails);
            Assert.Equal(customerEmail, email.Email);
            Assert.Equal(ReservationArrivalReminderTypes.FiveMinutesBeforeNoShow, email.ReminderType);
            Assert.Equal(ScheduledArrival, email.ScheduledArrivalTimeUtc);
            Assert.Equal(CutoffTime, email.CutoffTimeUtc);
            Assert.Equal("29A***99", email.MaskedPlateNumber);
        }
        finally
        {
            await harness.Stop();
        }
    }

    [Fact]
    public async Task ArrivalReminder_WhenAccountNotFound_SkipsEmail()
    {
        var store = new FakeNotificationStore();
        var emailSender = new FakeEmailSender();
        var emailLookup = new FakeEmailLookup();

        var consumer = new ReservationArrivalReminderNotificationConsumer(
            store,
            emailSender,
            emailLookup,
            NullLogger<ReservationArrivalReminderNotificationConsumer>.Instance);

        var accountId = Guid.NewGuid();
        var message = new ReservationArrivalReminderIntegrationEvent(
            accountId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            ReservationArrivalReminderTypes.ThirtyMinutesBeforeArrival,
            ScheduledArrival,
            CutoffTime,
            "51A***45");

        var harness = new InMemoryTestHarness();
        var consumerHarness = harness.Consumer(() => consumer);

        await harness.Start(TestContext.Current.CancellationToken);
        try
        {
            await harness.Bus.Publish(message, TestContext.Current.CancellationToken);
            Assert.True(await consumerHarness.Consumed.Any<ReservationArrivalReminderIntegrationEvent>(TestContext.Current.CancellationToken));

            // In-app notice still created
            Assert.Single(store.Stored);

            // Email NOT sent
            Assert.Empty(emailSender.SentArrivalReminderEmails);
        }
        finally
        {
            await harness.Stop();
        }
    }

    [Fact]
    public async Task ArrivalReminder_WhenEmailNotVerified_SkipsEmail()
    {
        var store = new FakeNotificationStore();
        var emailSender = new FakeEmailSender();
        var emailLookup = new FakeEmailLookup();

        var accountId = Guid.NewGuid();
        emailLookup.Accounts[accountId] = new UserAccountEmailInfo(accountId, "unverified@parkwise.vn", IsEmailVerified: false);

        var consumer = new ReservationArrivalReminderNotificationConsumer(
            store,
            emailSender,
            emailLookup,
            NullLogger<ReservationArrivalReminderNotificationConsumer>.Instance);

        var message = new ReservationArrivalReminderIntegrationEvent(
            accountId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            ReservationArrivalReminderTypes.ThirtyMinutesBeforeArrival,
            ScheduledArrival,
            CutoffTime,
            "51A***45");

        var harness = new InMemoryTestHarness();
        var consumerHarness = harness.Consumer(() => consumer);

        await harness.Start(TestContext.Current.CancellationToken);
        try
        {
            await harness.Bus.Publish(message, TestContext.Current.CancellationToken);
            Assert.True(await consumerHarness.Consumed.Any<ReservationArrivalReminderIntegrationEvent>(TestContext.Current.CancellationToken));

            // In-app notice still created
            Assert.Single(store.Stored);

            // Email NOT sent
            Assert.Empty(emailSender.SentArrivalReminderEmails);
        }
        finally
        {
            await harness.Stop();
        }
    }

    [Fact]
    public async Task ArrivalReminder_WhenAccountIdEmpty_SkipsEmail()
    {
        var store = new FakeNotificationStore();
        var emailSender = new FakeEmailSender();
        var emailLookup = new FakeEmailLookup();

        var consumer = new ReservationArrivalReminderNotificationConsumer(
            store,
            emailSender,
            emailLookup,
            NullLogger<ReservationArrivalReminderNotificationConsumer>.Instance);

        var message = new ReservationArrivalReminderIntegrationEvent(
            Guid.Empty,
            Guid.NewGuid(),
            Guid.NewGuid(),
            ReservationArrivalReminderTypes.ThirtyMinutesBeforeArrival,
            ScheduledArrival,
            CutoffTime,
            "51A***45");

        var harness = new InMemoryTestHarness();
        var consumerHarness = harness.Consumer(() => consumer);

        await harness.Start(TestContext.Current.CancellationToken);
        try
        {
            await harness.Bus.Publish(message, TestContext.Current.CancellationToken);
            Assert.True(await consumerHarness.Consumed.Any<ReservationArrivalReminderIntegrationEvent>(TestContext.Current.CancellationToken));

            // In-app notice still created
            Assert.Single(store.Stored);

            // Email NOT sent
            Assert.Empty(emailSender.SentArrivalReminderEmails);
        }
        finally
        {
            await harness.Stop();
        }
    }

    // -------------------------------------------------------------------------
    // Test Doubles
    // -------------------------------------------------------------------------
    private sealed class FakeNotificationStore : ICustomerNotificationStore
    {
        public List<(Guid UserId, string SourceEventKey, string Type, string Title, string Description, string Href, DateTimeOffset CreatedAtUtc)> Stored { get; } = [];

        public Task AddIfAbsentAsync(
            Guid userId,
            string sourceEventKey,
            string type,
            string title,
            string description,
            string href,
            DateTimeOffset createdAtUtc,
            CancellationToken ct)
        {
            Stored.Add((userId, sourceEventKey, type, title, description, href, createdAtUtc));
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
        public List<ArrivalReminderEmailRecord> SentArrivalReminderEmails { get; } = [];

        public Task SendReservationArrivalReminderEmailAsync(
            string email,
            string reminderType,
            DateTime scheduledArrivalTimeUtc,
            DateTime cutoffTimeUtc,
            string? maskedPlateNumber,
            CancellationToken cancellationToken = default)
        {
            SentArrivalReminderEmails.Add(new ArrivalReminderEmailRecord(
                email,
                reminderType,
                scheduledArrivalTimeUtc,
                cutoffTimeUtc,
                maskedPlateNumber));
            return Task.CompletedTask;
        }

        public Task SendVerificationOtpEmailAsync(string email, string otpCode, DateTimeOffset expiresAtUtc, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SendPasswordResetOtpEmailAsync(string email, string otpCode, DateTimeOffset expiresAtUtc, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SendOwnerApplicationApprovedEmailAsync(string email, DateTimeOffset approvedAtUtc, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SendOwnerApplicationRejectedEmailAsync(string email, string reason, DateTimeOffset rejectedAtUtc, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SendReservationConfirmedEmailAsync(string email, Guid reservationId, DateTimeOffset appointmentTime, decimal prepaidAmount, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SendElectronicReceiptEmailAsync(string email, Guid sessionId, string maskedPlateNumber, long totalFee, long paidAmount, long amountDue, DateTimeOffset paymentTimeUtc, DateTimeOffset exitWindowExpiresUtc, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    public sealed record ArrivalReminderEmailRecord(
        string Email,
        string ReminderType,
        DateTime ScheduledArrivalTimeUtc,
        DateTime CutoffTimeUtc,
        string? MaskedPlateNumber);
}
