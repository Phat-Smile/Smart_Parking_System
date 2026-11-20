using MassTransit;
using MassTransit.Testing;
using Microsoft.Extensions.Logging.Abstractions;
using ParkingSystem.Contracts.Events.Booking;
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

        var consumer = new ReservationArrivalReminderNotificationConsumer(
            store,
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

        await harness.Start();
        try
        {
            await harness.Bus.Publish(message);
            Assert.True(await consumerHarness.Consumed.Any<ReservationArrivalReminderIntegrationEvent>());

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

        var consumer = new ReservationArrivalReminderNotificationConsumer(
            store,
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

        await harness.Start();
        try
        {
            await harness.Bus.Publish(message);
            Assert.True(await consumerHarness.Consumed.Any<ReservationArrivalReminderIntegrationEvent>());

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
}
