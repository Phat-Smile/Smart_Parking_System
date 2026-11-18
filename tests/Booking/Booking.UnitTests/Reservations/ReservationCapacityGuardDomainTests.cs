using ParkingSystem.Booking.Domain.Reservations;
using Xunit;

namespace ParkingSystem.Tests.Booking.UnitTests.Reservations;

public sealed class ReservationCapacityGuardDomainTests
{
    private static readonly DateTimeOffset BaseTime = new(2026, 10, 15, 10, 0, 0, TimeSpan.Zero);

    // ==========================================
    // U1: Khung giữ chỗ & Khung kiểm trần
    // ==========================================
    [Fact]
    public void U1_HoldWhileDriving_calculates_correct_windows_with_default_and_custom_lead_grace()
    {
        // Default lead (30m) & grace (16m): 10:00 -> [09:30, 10:16)
        var defaultWindows = ReservationTimeWindow.CalculateWindows(
            ReservationType.HoldWhileDriving,
            BaseTime,
            durationMinutes: null);

        Assert.Equal(BaseTime.AddMinutes(-30), defaultWindows.WindowStartAt);
        Assert.Equal(BaseTime.AddMinutes(16), defaultWindows.WindowEndAt);
        Assert.Equal(BaseTime.AddMinutes(16), defaultWindows.HoldEndAt);

        // Custom lead (20m) & grace (10m): 10:00 -> [09:40, 10:10)
        var customWindows = ReservationTimeWindow.CalculateWindows(
            ReservationType.HoldWhileDriving,
            BaseTime,
            durationMinutes: null,
            leadMinutes: 20,
            graceMinutes: 10);

        Assert.Equal(BaseTime.AddMinutes(-20), customWindows.WindowStartAt);
        Assert.Equal(BaseTime.AddMinutes(10), customWindows.WindowEndAt);
        Assert.Equal(BaseTime.AddMinutes(10), customWindows.HoldEndAt);
    }

    [Fact]
    public void U1_Scheduled_calculates_distinct_hold_and_ceiling_windows()
    {
        // 10:00 with duration 120m:
        // Hold window: [09:30, 10:16)
        // Ceiling check window: [09:30, 12:00)
        var windows = ReservationTimeWindow.CalculateWindows(
            ReservationType.Scheduled,
            BaseTime,
            durationMinutes: 120);

        Assert.Equal(BaseTime.AddMinutes(-30), windows.WindowStartAt);
        Assert.Equal(BaseTime.AddMinutes(120), windows.WindowEndAt);
        Assert.Equal(BaseTime.AddMinutes(16), windows.HoldEndAt);

        // Custom config: lead 45m, grace 20m, duration 180m
        var customWindows = ReservationTimeWindow.CalculateWindows(
            ReservationType.Scheduled,
            BaseTime,
            durationMinutes: 180,
            leadMinutes: 45,
            graceMinutes: 20);

        Assert.Equal(BaseTime.AddMinutes(-45), customWindows.WindowStartAt);
        Assert.Equal(BaseTime.AddMinutes(180), customWindows.WindowEndAt);
        Assert.Equal(BaseTime.AddMinutes(20), customWindows.HoldEndAt);
    }

    // ==========================================
    // U2: Giao khung nửa mở [start, end)
    // ==========================================
    [Fact]
    public void U2_HalfOpenInterval_overlap_behavior_verifies_touching_boundary_is_not_overlapping()
    {
        var t09 = BaseTime.AddHours(-1);
        var t10 = BaseTime;
        var t11 = BaseTime.AddHours(1);
        var t12 = BaseTime.AddHours(2);

        // [09:00, 10:00) and [10:00, 11:00) do NOT overlap
        Assert.False(ReservationTimeWindow.IsOverlapping(t09, t10, t10, t11));
        Assert.False(ReservationTimeWindow.IsOverlapping(t10, t11, t09, t10));

        // Overlapping by 1 minute: [09:00, 10:01) and [10:00, 11:00)
        Assert.True(ReservationTimeWindow.IsOverlapping(t09, t10.AddMinutes(1), t10, t11));

        // Containing: [09:00, 12:00) contains [10:00, 11:00)
        Assert.True(ReservationTimeWindow.IsOverlapping(t09, t12, t10, t11));

        // Identical: [09:00, 10:00) and [09:00, 10:00)
        Assert.True(ReservationTimeWindow.IsOverlapping(t09, t10, t09, t10));
    }

    // ==========================================
    // U3: Trần suất = floor(validCapacity * ratio / 100)
    // ==========================================
    [Theory]
    [InlineData(10, 50, 5)]
    [InlineData(9, 50, 4)]
    [InlineData(1, 50, 0)]
    [InlineData(0, 50, 0)]
    [InlineData(0, 100, 0)]
    [InlineData(50, 100, 50)]
    [InlineData(50, 0, 0)]
    [InlineData(7, 33, 2)]
    [InlineData(100, 1, 1)]
    [InlineData(13, 50, 6)]
    [InlineData(7, 50, 3)]
    public void U3_CalculateCeiling_matches_expected_floor_rounding(int capacity, int percent, int expectedCeiling)
    {
        var ceiling = ReservationTimeWindow.CalculateCeiling(capacity, percent);
        Assert.Equal(expectedCeiling, ceiling);
    }

    [Theory]
    [InlineData(10, -1)]
    [InlineData(10, 101)]
    [InlineData(-1, 50)]
    public void U3_CalculateCeiling_rejects_out_of_range_arguments(int capacity, int percent)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ReservationTimeWindow.CalculateCeiling(capacity, percent));
    }

    // ==========================================
    // U4: Điều kiện đếm trạng thái còn hiệu lực
    // ==========================================
    [Fact]
    public void U4_ActiveReservation_counting_conditions_distinguish_valid_from_invalid_holds()
    {
        var now = BaseTime;

        // TemporaryHold with future expiry -> active
        Assert.True(IsActiveForCounting(ReservationStatus.TemporaryHold, tempHoldExpiresAt: now.AddMinutes(5), now));

        // TemporaryHold with expiry == now -> expired (not counted)
        Assert.False(IsActiveForCounting(ReservationStatus.TemporaryHold, tempHoldExpiresAt: now, now));

        // TemporaryHold with past expiry -> expired (not counted)
        Assert.False(IsActiveForCounting(ReservationStatus.TemporaryHold, tempHoldExpiresAt: now.AddMinutes(-1), now));

        // Confirmed -> active
        Assert.True(IsActiveForCounting(ReservationStatus.Confirmed, tempHoldExpiresAt: null, now));

        // Cancelled, Expired, Used -> not active
        Assert.False(IsActiveForCounting(ReservationStatus.Cancelled, tempHoldExpiresAt: null, now));
        Assert.False(IsActiveForCounting(ReservationStatus.Expired, tempHoldExpiresAt: null, now));
        Assert.False(IsActiveForCounting(ReservationStatus.Used, tempHoldExpiresAt: null, now));
    }

    private static bool IsActiveForCounting(ReservationStatus status, DateTimeOffset? tempHoldExpiresAt, DateTimeOffset now)
    {
        return status == ReservationStatus.Confirmed ||
               (status == ReservationStatus.TemporaryHold && tempHoldExpiresAt.HasValue && tempHoldExpiresAt.Value > now);
    }

    // ==========================================
    // U5: Xe máy & Khung vô lý bị từ chối
    // ==========================================
    [Fact]
    public void U5_Motorbike_is_rejected_at_domain_level()
    {
        var ex = Assert.Throws<ReservationDomainException>(() =>
            Reservation.CreateTemporaryHold(
                id: Guid.NewGuid(),
                userId: Guid.NewGuid(),
                vehicleId: Guid.NewGuid(),
                plateNumber: "29A-12345",
                lotId: Guid.NewGuid(),
                vehicleGroup: VehicleGroup.Motorbike,
                reservationType: ReservationType.HoldWhileDriving,
                appointmentTime: BaseTime,
                prepaidAmount: 20000m,
                createdAt: BaseTime));

        Assert.Contains("Only cars", ex.Message);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-15)]
    public void U5_Scheduled_with_non_positive_duration_is_rejected(int invalidDuration)
    {
        var ex = Assert.Throws<ReservationDomainException>(() =>
            ReservationTimeWindow.CalculateWindows(
                ReservationType.Scheduled,
                BaseTime,
                durationMinutes: invalidDuration));

        Assert.Contains("greater than zero", ex.Message);
    }

    [Fact]
    public void U5_Invalid_interval_start_end_order_throws_ArgumentException()
    {
        var t10 = BaseTime;
        var t09 = BaseTime.AddHours(-1);

        // Interval where end <= start
        Assert.Throws<ArgumentException>(() =>
            ReservationTimeWindow.IsOverlapping(t10, t09, BaseTime, BaseTime.AddHours(1)));
    }
}

