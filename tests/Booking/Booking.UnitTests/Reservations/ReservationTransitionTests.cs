using ParkingSystem.Booking.Domain.Reservations;
using Xunit;

namespace ParkingSystem.Tests.Booking.UnitTests.Reservations;

public sealed class ReservationTransitionTests
{
    private static readonly Guid ReservationId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid UserId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid VehicleId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid LotId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private static readonly DateTimeOffset Now = new(2026, 10, 12, 10, 0, 0, TimeSpan.Zero);

    private static Reservation CreateSampleReservation() =>
        Reservation.CreateTemporaryHold(
            id: ReservationId,
            userId: UserId,
            vehicleId: VehicleId,
            plateNumber: "51F-123.45",
            lotId: LotId,
            vehicleGroup: VehicleGroup.Car,
            reservationType: ReservationType.HoldWhileDriving,
            appointmentTime: Now.AddMinutes(30),
            prepaidAmount: 20000m,
            createdAt: Now,
            tempHoldExpiresAt: Now.AddMinutes(10));

    [Fact]
    public void Initial_reservation_creation_starts_in_TemporaryHold_status()
    {
        var reservation = CreateSampleReservation();

        Assert.Equal(ReservationStatus.TemporaryHold, reservation.Status);
        Assert.False(reservation.IsNoShow);
        Assert.Equal(0, reservation.RowVersion);
        Assert.Equal("51F-123.45", reservation.PlateNumber);
        Assert.Null(reservation.ConfirmedAt);
        Assert.Null(reservation.UsedAt);
        Assert.Null(reservation.CancelledAt);
        Assert.Null(reservation.CancelReason);
        Assert.Null(reservation.SessionId);
    }

    [Theory]
    [InlineData(ReservationStatus.TemporaryHold, ReservationStatus.Confirmed)]
    [InlineData(ReservationStatus.TemporaryHold, ReservationStatus.Cancelled)]
    [InlineData(ReservationStatus.TemporaryHold, ReservationStatus.Expired)]
    public void Allowed_transitions_from_TemporaryHold_succeed(ReservationStatus initial, ReservationStatus target)
    {
        Assert.True(Reservation.CanTransition(initial, target));

        var reservation = CreateSampleReservation();
        reservation.TransitionTo(target);

        Assert.Equal(target, reservation.Status);
    }

    [Theory]
    [InlineData(ReservationStatus.Confirmed, ReservationStatus.Used)]
    [InlineData(ReservationStatus.Confirmed, ReservationStatus.Cancelled)]
    public void Allowed_transitions_from_Confirmed_succeed(ReservationStatus initial, ReservationStatus target)
    {
        Assert.True(Reservation.CanTransition(initial, target));

        var reservation = CreateSampleReservation();
        reservation.TransitionTo(ReservationStatus.Confirmed);

        reservation.TransitionTo(target);
        Assert.Equal(target, reservation.Status);
    }

    [Theory]
    [InlineData(ReservationStatus.TemporaryHold, ReservationStatus.Used)]
    [InlineData(ReservationStatus.TemporaryHold, ReservationStatus.TemporaryHold)]
    [InlineData(ReservationStatus.Confirmed, ReservationStatus.Confirmed)]
    [InlineData(ReservationStatus.Confirmed, ReservationStatus.TemporaryHold)]
    [InlineData(ReservationStatus.Confirmed, ReservationStatus.Expired)]
    public void Disallowed_transitions_from_active_states_throw_domain_exception(ReservationStatus initial, ReservationStatus target)
    {
        Assert.False(Reservation.CanTransition(initial, target));

        var reservation = CreateSampleReservation();
        if (initial == ReservationStatus.Confirmed)
        {
            reservation.TransitionTo(ReservationStatus.Confirmed);
        }

        var ex = Assert.Throws<ReservationDomainException>(() => reservation.TransitionTo(target));
        Assert.Contains(initial.ToString(), ex.Message);
        Assert.Contains(target.ToString(), ex.Message);
    }

    [Theory]
    [InlineData(ReservationStatus.Used, ReservationStatus.TemporaryHold)]
    [InlineData(ReservationStatus.Used, ReservationStatus.Confirmed)]
    [InlineData(ReservationStatus.Used, ReservationStatus.Used)]
    [InlineData(ReservationStatus.Used, ReservationStatus.Cancelled)]
    [InlineData(ReservationStatus.Used, ReservationStatus.Expired)]
    public void Used_is_terminal_and_rejects_all_further_transitions(ReservationStatus initial, ReservationStatus target)
    {
        Assert.False(Reservation.CanTransition(initial, target));

        var reservation = CreateSampleReservation();
        reservation.TransitionTo(ReservationStatus.Confirmed);
        reservation.TransitionTo(ReservationStatus.Used);

        var ex = Assert.Throws<ReservationDomainException>(() => reservation.TransitionTo(target));
        Assert.Contains("Used", ex.Message);
    }

    [Theory]
    [InlineData(ReservationStatus.Cancelled, ReservationStatus.TemporaryHold)]
    [InlineData(ReservationStatus.Cancelled, ReservationStatus.Confirmed)]
    [InlineData(ReservationStatus.Cancelled, ReservationStatus.Used)]
    [InlineData(ReservationStatus.Cancelled, ReservationStatus.Cancelled)]
    [InlineData(ReservationStatus.Cancelled, ReservationStatus.Expired)]
    public void Cancelled_is_terminal_and_rejects_all_further_transitions(ReservationStatus initial, ReservationStatus target)
    {
        Assert.False(Reservation.CanTransition(initial, target));

        var reservation = CreateSampleReservation();
        reservation.TransitionTo(ReservationStatus.Cancelled);

        var ex = Assert.Throws<ReservationDomainException>(() => reservation.TransitionTo(target));
        Assert.Contains("Cancelled", ex.Message);
    }

    [Theory]
    [InlineData(ReservationStatus.Expired, ReservationStatus.TemporaryHold)]
    [InlineData(ReservationStatus.Expired, ReservationStatus.Confirmed)]
    [InlineData(ReservationStatus.Expired, ReservationStatus.Used)]
    [InlineData(ReservationStatus.Expired, ReservationStatus.Cancelled)]
    [InlineData(ReservationStatus.Expired, ReservationStatus.Expired)]
    public void Expired_is_terminal_and_rejects_all_further_transitions(ReservationStatus initial, ReservationStatus target)
    {
        Assert.False(Reservation.CanTransition(initial, target));

        var reservation = CreateSampleReservation();
        reservation.TransitionTo(ReservationStatus.Expired);

        var ex = Assert.Throws<ReservationDomainException>(() => reservation.TransitionTo(target));
        Assert.Contains("Expired", ex.Message);
    }

    [Fact]
    public void Confirm_method_transitions_to_Confirmed_and_sets_confirmed_timestamp()
    {
        var reservation = CreateSampleReservation();
        var confirmedAt = Now.AddMinutes(2);

        reservation.Confirm(confirmedAt);

        Assert.Equal(ReservationStatus.Confirmed, reservation.Status);
        Assert.Equal(confirmedAt, reservation.ConfirmedAt);
    }

    [Fact]
    public void Use_method_transitions_to_Used_and_sets_used_timestamp_and_session()
    {
        var reservation = CreateSampleReservation();
        var confirmedAt = Now.AddMinutes(2);
        var usedAt = Now.AddMinutes(15);
        var sessionId = Guid.NewGuid();

        reservation.Confirm(confirmedAt);
        reservation.Use(usedAt, sessionId);

        Assert.Equal(ReservationStatus.Used, reservation.Status);
        Assert.Equal(usedAt, reservation.UsedAt);
        Assert.Equal(sessionId, reservation.SessionId);
    }

    [Fact]
    public void Cancel_method_transitions_to_Cancelled_and_records_reason_and_noshow()
    {
        var reservation = CreateSampleReservation();
        reservation.Confirm(Now.AddMinutes(2));

        var cancelledAt = Now.AddMinutes(45);
        reservation.Cancel(cancelledAt, CancelReason.NoShow, isNoShow: true, prepaidOutcome: PrepaidOutcome.Forfeited);

        Assert.Equal(ReservationStatus.Cancelled, reservation.Status);
        Assert.Equal(cancelledAt, reservation.CancelledAt);
        Assert.Equal(CancelReason.NoShow, reservation.CancelReason);
        Assert.True(reservation.IsNoShow);
        Assert.Equal(PrepaidOutcome.Forfeited, reservation.PrepaidOutcome);
    }

    [Fact]
    public void Expire_method_transitions_to_Expired()
    {
        var reservation = CreateSampleReservation();

        reservation.Expire();

        Assert.Equal(ReservationStatus.Expired, reservation.Status);
    }
}
