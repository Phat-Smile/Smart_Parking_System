using ParkingSystem.Booking.Domain.Enums;
using ParkingSystem.Booking.Domain.Reservations;
using Xunit;

namespace ParkingSystem.Tests.Booking.UnitTests;

public sealed class ReservationStatusTransitionTests
{
    private readonly DateTimeOffset _now = new(2026, 10, 10, 12, 0, 0, TimeSpan.Zero);

    private Reservation CreateTemporaryHold()
    {
        return Reservation.CreateTemporaryHold(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "51A-999.99",
            Guid.NewGuid(),
            VehicleGroup.Car,
            ReservationType.Scheduled,
            _now.AddHours(2),
            60,
            50000,
            _now.AddMinutes(10),
            _now);
    }

    [Fact]
    public void TemporaryHold_to_Confirmed_succeeds()
    {
        var reservation = CreateTemporaryHold();

        reservation.Confirm(_now.AddMinutes(5));

        Assert.Equal(ReservationStatus.Confirmed, reservation.Status);
        Assert.NotNull(reservation.ConfirmedAt);
    }

    [Fact]
    public void TemporaryHold_to_Cancelled_succeeds()
    {
        var reservation = CreateTemporaryHold();

        reservation.Cancel(CancelReason.CustomerCancelled, _now.AddMinutes(2));

        Assert.Equal(ReservationStatus.Cancelled, reservation.Status);
        Assert.NotNull(reservation.CancelledAt);
        Assert.Equal(CancelReason.CustomerCancelled, reservation.CancelReason);
    }

    [Fact]
    public void TemporaryHold_to_Expired_succeeds()
    {
        var reservation = CreateTemporaryHold();

        reservation.Expire(_now.AddMinutes(10));

        Assert.Equal(ReservationStatus.Expired, reservation.Status);
        Assert.NotNull(reservation.CancelledAt);
    }

    [Fact]
    public void Confirmed_to_Used_succeeds()
    {
        var reservation = CreateTemporaryHold();
        reservation.Confirm(_now.AddMinutes(5));
        var sessionId = Guid.NewGuid();

        reservation.MarkUsed(sessionId, _now.AddHours(2));

        Assert.Equal(ReservationStatus.Used, reservation.Status);
        Assert.Equal(sessionId, reservation.SessionId);
        Assert.NotNull(reservation.UsedAt);
    }

    [Fact]
    public void Confirmed_to_Cancelled_succeeds()
    {
        var reservation = CreateTemporaryHold();
        reservation.Confirm(_now.AddMinutes(5));

        reservation.Cancel(CancelReason.CustomerCancelled, _now.AddHours(1), PrepaidOutcome.Refunded);

        Assert.Equal(ReservationStatus.Cancelled, reservation.Status);
        Assert.Equal(PrepaidOutcome.Refunded, reservation.PrepaidOutcome);
    }

    [Fact]
    public void Confirmed_to_Expired_succeeds()
    {
        var reservation = CreateTemporaryHold();
        reservation.Confirm(_now.AddMinutes(5));

        reservation.ChangeStatus(ReservationStatus.Expired);

        Assert.Equal(ReservationStatus.Expired, reservation.Status);
    }

    [Fact]
    public void Used_to_Cancelled_fails()
    {
        var reservation = CreateTemporaryHold();
        reservation.Confirm(_now.AddMinutes(5));
        reservation.MarkUsed(Guid.NewGuid(), _now.AddHours(2));

        var ex = Assert.Throws<InvalidReservationStatusTransitionException>(() =>
            reservation.Cancel(CancelReason.CustomerCancelled, _now.AddHours(3)));

        Assert.Equal(ReservationStatus.Used, ex.FromStatus);
        Assert.Equal(ReservationStatus.Cancelled, ex.ToStatus);
    }

    [Fact]
    public void Used_to_Confirmed_fails()
    {
        var reservation = CreateTemporaryHold();
        reservation.Confirm(_now.AddMinutes(5));
        reservation.MarkUsed(Guid.NewGuid(), _now.AddHours(2));

        var ex = Assert.Throws<InvalidReservationStatusTransitionException>(() =>
            reservation.Confirm(_now.AddHours(3)));

        Assert.Equal(ReservationStatus.Used, ex.FromStatus);
        Assert.Equal(ReservationStatus.Confirmed, ex.ToStatus);
    }

    [Fact]
    public void Cancelled_to_Confirmed_fails()
    {
        var reservation = CreateTemporaryHold();
        reservation.Cancel(CancelReason.CustomerCancelled, _now.AddMinutes(2));

        var ex = Assert.Throws<InvalidReservationStatusTransitionException>(() =>
            reservation.Confirm(_now.AddMinutes(5)));

        Assert.Equal(ReservationStatus.Cancelled, ex.FromStatus);
        Assert.Equal(ReservationStatus.Confirmed, ex.ToStatus);
    }

    [Fact]
    public void Expired_to_Confirmed_fails()
    {
        var reservation = CreateTemporaryHold();
        reservation.Expire(_now.AddMinutes(10));

        var ex = Assert.Throws<InvalidReservationStatusTransitionException>(() =>
            reservation.Confirm(_now.AddMinutes(12)));

        Assert.Equal(ReservationStatus.Expired, ex.FromStatus);
        Assert.Equal(ReservationStatus.Confirmed, ex.ToStatus);
    }

    [Theory]
    [InlineData(ReservationStatus.Used, ReservationStatus.TemporaryHold)]
    [InlineData(ReservationStatus.Used, ReservationStatus.Expired)]
    [InlineData(ReservationStatus.Cancelled, ReservationStatus.TemporaryHold)]
    [InlineData(ReservationStatus.Cancelled, ReservationStatus.Used)]
    [InlineData(ReservationStatus.Cancelled, ReservationStatus.Expired)]
    [InlineData(ReservationStatus.Expired, ReservationStatus.TemporaryHold)]
    [InlineData(ReservationStatus.Expired, ReservationStatus.Used)]
    [InlineData(ReservationStatus.Expired, ReservationStatus.Cancelled)]
    public void Terminal_statuses_reject_any_arbitrary_transitions(ReservationStatus terminalStatus, ReservationStatus targetStatus)
    {
        var reservation = CreateTemporaryHold();
        if (terminalStatus == ReservationStatus.Used)
        {
            reservation.Confirm(_now.AddMinutes(1));
            reservation.MarkUsed(Guid.NewGuid(), _now.AddHours(1));
        }
        else if (terminalStatus == ReservationStatus.Cancelled)
        {
            reservation.Cancel(CancelReason.CustomerCancelled, _now.AddMinutes(1));
        }
        else if (terminalStatus == ReservationStatus.Expired)
        {
            reservation.Expire(_now.AddMinutes(10));
        }

        Assert.False(reservation.CanTransitionTo(targetStatus));
        Assert.Throws<InvalidReservationStatusTransitionException>(() =>
            reservation.ChangeStatus(targetStatus));
    }
}
