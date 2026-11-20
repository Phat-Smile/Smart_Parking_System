using System.Globalization;
using MassTransit;
using MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;
using ParkingSystem.Contracts.Events.Booking;
using ParkingSystem.Notification.Application.CustomerNotifications;
using ParkingSystem.Notification.Infrastructure.Consumers;
using Xunit;

namespace ParkingSystem.Tests.Notification.UnitTests;

public sealed class ReservationCancelledNotificationConsumerTests
{
    private static readonly DateTimeOffset SampleCancelledAt = new(2026, 11, 20, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void T_01_NoShow_with_forfeited_amount_explains_15_minutes_and_exact_forfeited_text()
    {
        var evt = new ReservationCancelledIntegrationEvent(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "NoShow", 0m, SampleCancelledAt, 20000m);

        var body = ReservationCancelledNoticeText.Build(evt);

        Assert.Equal(
            "Đặt chỗ đã bị hủy vì xe không vào bãi trong 15 phút sau giờ hẹn. Khoản trả trước 20.000 đ không được hoàn.",
            body);
    }

    [Theory]
    [InlineData("en-US")]
    [InlineData("")]
    public void T_01b_Formatted_amount_uses_fixed_vnd_separator_regardless_of_culture(string cultureName)
    {
        var originalCulture = CultureInfo.CurrentCulture;
        var originalUiCulture = CultureInfo.CurrentUICulture;

        try
        {
            var targetCulture = string.IsNullOrEmpty(cultureName)
                ? CultureInfo.InvariantCulture
                : CultureInfo.GetCultureInfo(cultureName);

            CultureInfo.CurrentCulture = targetCulture;
            CultureInfo.CurrentUICulture = targetCulture;

            var evt = new ReservationCancelledIntegrationEvent(
                Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
                "NoShow", 0m, SampleCancelledAt, 20000m);

            var body = ReservationCancelledNoticeText.Build(evt);

            Assert.Equal(
                "Đặt chỗ đã bị hủy vì xe không vào bãi trong 15 phút sau giờ hẹn. Khoản trả trước 20.000 đ không được hoàn.",
                body);
            Assert.Contains("20.000 đ", body);
            Assert.DoesNotContain("20,000", body);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
            CultureInfo.CurrentUICulture = originalUiCulture;
        }
    }

    [Fact]
    public void T_02_NoShow_with_zero_forfeited_amount_omits_prepaid_sentence()
    {
        var evt = new ReservationCancelledIntegrationEvent(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "NoShow", 0m, SampleCancelledAt, 0m);

        var body = ReservationCancelledNoticeText.Build(evt);

        Assert.Equal("Đặt chỗ đã bị hủy vì xe không vào bãi trong 15 phút sau giờ hẹn.", body);
    }

    [Fact]
    public void T_03_CustomerCancelled_with_forfeited_amount_informs_non_refunded_prepayment()
    {
        var evt = new ReservationCancelledIntegrationEvent(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "CustomerCancelled", 0m, SampleCancelledAt, 20000m);

        var body = ReservationCancelledNoticeText.Build(evt);

        Assert.Equal(
            "Bạn đã hủy đặt chỗ sau mốc được hoàn tiền. Khoản trả trước 20.000 đ không được hoàn.",
            body);
    }

    [Fact]
    public void T_04_CustomerCancelled_with_refund_amount_states_will_be_refunded_to_wallet()
    {
        var evt = new ReservationCancelledIntegrationEvent(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "CustomerCancelled", 50000m, SampleCancelledAt, 0m);

        var body = ReservationCancelledNoticeText.Build(evt);

        Assert.Equal("Bạn đã hủy đặt chỗ. 50.000 đ sẽ được hoàn về ví.", body);
    }

    [Fact]
    public void T_04b_CustomerCancelled_with_both_zero_outputs_plain_cancellation()
    {
        var evt = new ReservationCancelledIntegrationEvent(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "CustomerCancelled", 0m, SampleCancelledAt, 0m);

        var body = ReservationCancelledNoticeText.Build(evt);

        Assert.Equal("Bạn đã hủy đặt chỗ.", body);
    }

    [Theory]
    [InlineData(50000, "Đặt chỗ đã bị hủy vì bãi không còn chỗ phù hợp. 50.000 đ sẽ được hoàn về ví.")]
    [InlineData(0, "Đặt chỗ đã bị hủy vì bãi không còn chỗ phù hợp.")]
    public void T_05_NoSuitableSlot_informs_lot_capacity_and_optional_refund(decimal refundAmount, string expectedBody)
    {
        var evt = new ReservationCancelledIntegrationEvent(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "NoSuitableSlot", refundAmount, SampleCancelledAt, 0m);

        var body = ReservationCancelledNoticeText.Build(evt);

        Assert.Equal(expectedBody, body);
    }

    [Fact]
    public void T_06_PaymentFailed_informs_payment_failure()
    {
        var evt = new ReservationCancelledIntegrationEvent(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "PaymentFailed", 0m, SampleCancelledAt, 0m);

        var body = ReservationCancelledNoticeText.Build(evt);

        Assert.Equal("Đặt chỗ đã bị hủy vì thanh toán không thành công.", body);
    }

    [Theory]
    [InlineData("UnknownTechnicalReason")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void T_07_Unknown_or_empty_reason_uses_generic_fallback_without_leaking_raw_reason(string? rawReason)
    {
        var evt = new ReservationCancelledIntegrationEvent(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            rawReason!, 0m, SampleCancelledAt, 0m);

        var body = ReservationCancelledNoticeText.Build(evt);

        Assert.Equal("Đặt chỗ của bạn đã được hủy.", body);
        if (!string.IsNullOrWhiteSpace(rawReason))
        {
            Assert.DoesNotContain(rawReason, body);
        }
    }

    [Fact]
    public async Task T_08_Consumer_deduplicates_by_source_event_key_and_creates_single_notice()
    {
        var store = new IdempotentFakeNotificationStore();
        await using var provider = new ServiceCollection()
            .AddSingleton<ICustomerNotificationStore>(store)
            .AddMassTransitTestHarness(cfg => cfg.AddConsumer<ReservationCancelledNotificationConsumer>())
            .BuildServiceProvider(true);

        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        var reservationId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var evt = new ReservationCancelledIntegrationEvent(
            reservationId, userId, Guid.NewGuid(),
            "NoShow", 0m, SampleCancelledAt, 20000m);

        var ct = TestContext.Current.CancellationToken;
        await harness.Bus.Publish(evt, ct);
        await harness.Bus.Publish(evt, ct);

        var consumerHarness = harness.GetConsumerHarness<ReservationCancelledNotificationConsumer>();
        Assert.True(await consumerHarness.Consumed.Any<ReservationCancelledIntegrationEvent>(ct));

        Assert.Single(store.StoredNotifications);
        var notice = store.StoredNotifications[0];
        Assert.Equal(userId, notice.UserId);
        Assert.Equal($"reservation-cancelled:{reservationId}", notice.SourceEventKey);
        Assert.Equal("booking", notice.Type);
        Assert.Equal("Đặt chỗ đã bị hủy", notice.Title);
        Assert.Equal(
            "Đặt chỗ đã bị hủy vì xe không vào bãi trong 15 phút sau giờ hẹn. Khoản trả trước 20.000 đ không được hoàn.",
            notice.Description);
        Assert.Equal("/customer/bookings", notice.Href);
        Assert.Equal(SampleCancelledAt, notice.CreatedAtUtc);
    }

    private sealed class IdempotentFakeNotificationStore : ICustomerNotificationStore
    {
        private readonly HashSet<string> _seenKeys = [];
        public List<(Guid UserId, string SourceEventKey, string Type, string Title, string Description, string Href, DateTimeOffset CreatedAtUtc)> StoredNotifications { get; } = [];

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
            lock (_seenKeys)
            {
                if (_seenKeys.Add(sourceEventKey))
                {
                    StoredNotifications.Add((userId, sourceEventKey, type, title, description, href, createdAtUtc));
                }
            }

            return Task.CompletedTask;
        }

        public Task<CustomerNotificationPage> ListAsync(Guid userId, bool unreadOnly, int page, int pageSize, CancellationToken ct) =>
            Task.FromResult(new CustomerNotificationPage([], 0, 0, page, pageSize));

        public Task<bool> MarkReadAsync(Guid userId, Guid notificationId, DateTimeOffset now, CancellationToken ct) =>
            Task.FromResult(false);

        public Task<int> MarkAllReadAsync(Guid userId, DateTimeOffset now, CancellationToken ct) =>
            Task.FromResult(0);
    }
}
