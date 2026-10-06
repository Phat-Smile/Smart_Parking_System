using ParkingSystem.Booking.Domain.Enums;
using ParkingSystem.Booking.Domain.Reservations;
using Xunit;

namespace ParkingSystem.Tests.Booking.UnitTests;

public sealed class ReservationTimeWindowTests
{
    [Fact]
    public void Test_M_Boundary_touching_windows_do_not_overlap()
    {
        // [10:00, 11:00) and [11:00, 12:00)
        var baseDate = new DateTimeOffset(2026, 10, 10, 0, 0, 0, TimeSpan.Zero);
        var windowA = new ReservationTimeWindow(baseDate.AddHours(10), baseDate.AddHours(11));
        var windowB = new ReservationTimeWindow(baseDate.AddHours(11), baseDate.AddHours(12));

        Assert.False(windowA.Overlaps(windowB));
        Assert.False(windowB.Overlaps(windowA));
    }

    [Fact]
    public void Test_N_Truly_overlapping_windows_are_detected()
    {
        // [10:00, 11:30) and [11:00, 12:00)
        var baseDate = new DateTimeOffset(2026, 10, 10, 0, 0, 0, TimeSpan.Zero);
        var windowA = new ReservationTimeWindow(baseDate.AddHours(10), baseDate.AddHours(11).AddMinutes(30));
        var windowB = new ReservationTimeWindow(baseDate.AddHours(11), baseDate.AddHours(12));

        Assert.True(windowA.Overlaps(windowB));
        Assert.True(windowB.Overlaps(windowA));
    }

    [Fact]
    public void Scheduled_booking_window_spans_from_30_minutes_before_appointment_to_duration_end()
    {
        var appointmentTime = new DateTimeOffset(2026, 10, 10, 14, 0, 0, TimeSpan.Zero);
        var durationMinutes = 120; // 2 hours

        var window = ReservationTimeWindow.ForReservation(
            ReservationType.Scheduled,
            appointmentTime,
            durationMinutes);

        Assert.Equal(appointmentTime.AddMinutes(-30), window.Start);
        Assert.Equal(appointmentTime.AddMinutes(120), window.End);
    }

    [Fact]
    public void Hold_while_driving_window_spans_from_30_minutes_before_to_15_minutes_grace()
    {
        var eta = new DateTimeOffset(2026, 10, 10, 14, 0, 0, TimeSpan.Zero);

        var window = ReservationTimeWindow.ForReservation(
            ReservationType.HoldWhileDriving,
            eta,
            null);

        Assert.Equal(eta.AddMinutes(-30), window.Start);
        Assert.Equal(eta.AddMinutes(15), window.End);
    }

    [Fact]
    public void Window_contains_evaluates_half_open_interval_properly()
    {
        var start = new DateTimeOffset(2026, 10, 10, 10, 0, 0, TimeSpan.Zero);
        var end = new DateTimeOffset(2026, 10, 10, 11, 0, 0, TimeSpan.Zero);
        var window = new ReservationTimeWindow(start, end);

        Assert.True(window.Contains(start));
        Assert.True(window.Contains(start.AddMinutes(30)));
        Assert.False(window.Contains(start.AddMinutes(-1)));
        Assert.False(window.Contains(end)); // End boundary is excluded in [start, end)
    }
}
