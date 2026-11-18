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
    public void Initial_reservation_creation_starts_in_TemporaryHold_status_with_version_zero()
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
    [InlineData(ReservationStatus.TemporaryHold, ReservationStatus.Confirmed, true)]
    [InlineData(ReservationStatus.TemporaryHold, ReservationStatus.Cancelled, true)]
    [InlineData(ReservationStatus.TemporaryHold, ReservationStatus.Expired, true)]
    [InlineData(ReservationStatus.TemporaryHold, ReservationStatus.Used, false)]
    [InlineData(ReservationStatus.TemporaryHold, ReservationStatus.TemporaryHold, false)]
    [InlineData(ReservationStatus.Confirmed, ReservationStatus.Used, true)]
    [InlineData(ReservationStatus.Confirmed, ReservationStatus.Cancelled, true)]
    [InlineData(ReservationStatus.Confirmed, ReservationStatus.Confirmed, false)]
    [InlineData(ReservationStatus.Confirmed, ReservationStatus.TemporaryHold, false)]
    [InlineData(ReservationStatus.Confirmed, ReservationStatus.Expired, false)]
    [InlineData(ReservationStatus.Used, ReservationStatus.TemporaryHold, false)]
    [InlineData(ReservationStatus.Used, ReservationStatus.Confirmed, false)]
    [InlineData(ReservationStatus.Used, ReservationStatus.Used, false)]
    [InlineData(ReservationStatus.Used, ReservationStatus.Cancelled, false)]
    [InlineData(ReservationStatus.Used, ReservationStatus.Expired, false)]
    [InlineData(ReservationStatus.Cancelled, ReservationStatus.TemporaryHold, false)]
    [InlineData(ReservationStatus.Cancelled, ReservationStatus.Confirmed, false)]
    [InlineData(ReservationStatus.Cancelled, ReservationStatus.Used, false)]
    [InlineData(ReservationStatus.Cancelled, ReservationStatus.Cancelled, false)]
    [InlineData(ReservationStatus.Cancelled, ReservationStatus.Expired, false)]
    [InlineData(ReservationStatus.Expired, ReservationStatus.TemporaryHold, false)]
    [InlineData(ReservationStatus.Expired, ReservationStatus.Confirmed, false)]
    [InlineData(ReservationStatus.Expired, ReservationStatus.Used, false)]
    [InlineData(ReservationStatus.Expired, ReservationStatus.Cancelled, false)]
    [InlineData(ReservationStatus.Expired, ReservationStatus.Expired, false)]
    public void CanTransition_matrix_evaluates_correctly_for_all_state_pairs(
        ReservationStatus initial, ReservationStatus target, bool expectedAllowed)
    {
        Assert.Equal(expectedAllowed, Reservation.CanTransition(initial, target));
    }

    [Fact]
    public void Confirm_advances_status_sets_timestamp_and_increments_RowVersion_once()
    {
        var reservation = CreateSampleReservation();
        var confirmedAt = Now.AddMinutes(2);

        reservation.Confirm(confirmedAt);

        Assert.Equal(ReservationStatus.Confirmed, reservation.Status);
        Assert.Equal(confirmedAt, reservation.ConfirmedAt);
        Assert.Equal(1, reservation.RowVersion);
    }

    [Fact]
    public void Use_from_Confirmed_advances_status_sets_metadata_and_increments_RowVersion()
    {
        var reservation = CreateSampleReservation();
        var confirmedAt = Now.AddMinutes(2);
        var usedAt = Now.AddMinutes(15);
        var sessionId = Guid.NewGuid();

        reservation.Confirm(confirmedAt);
        Assert.Equal(1, reservation.RowVersion);

        reservation.Use(usedAt, sessionId);

        Assert.Equal(ReservationStatus.Used, reservation.Status);
        Assert.Equal(usedAt, reservation.UsedAt);
        Assert.Equal(sessionId, reservation.SessionId);
        Assert.Equal(2, reservation.RowVersion);
    }

    [Fact]
    public void Expire_from_TemporaryHold_advances_status_and_increments_RowVersion()
    {
        var reservation = CreateSampleReservation();

        reservation.Expire();

        Assert.Equal(ReservationStatus.Expired, reservation.Status);
        Assert.Equal(1, reservation.RowVersion);
    }

    [Theory]
    [InlineData(CancelReason.CustomerCancelled, false)]
    [InlineData(CancelReason.PaymentFailed, false)]
    [InlineData(CancelReason.NoSuitableSlot, false)]
    [InlineData(CancelReason.NoShow, true)]
    public void Cancel_derives_IsNoShow_consistently_from_CancelReason(CancelReason reason, bool expectedNoShow)
    {
        var reservation = CreateSampleReservation();
        reservation.Confirm(Now.AddMinutes(2));
        var cancelledAt = reservation.AppointmentTime.AddMinutes(16);

        reservation.Cancel(cancelledAt, reason);

        Assert.Equal(ReservationStatus.Cancelled, reservation.Status);
        Assert.Equal(cancelledAt, reservation.CancelledAt);
        Assert.Equal(reason, reservation.CancelReason);
        Assert.Equal(expectedNoShow, reservation.IsNoShow);
        Assert.Equal(2, reservation.RowVersion);
    }

    [Fact]
    public void Use_with_Guid_Empty_sessionId_throws_ArgumentException_and_preserves_state()
    {
        var reservation = CreateSampleReservation();
        reservation.Confirm(Now.AddMinutes(2));
        var versionBefore = reservation.RowVersion;
        var statusBefore = reservation.Status;

        var ex = Assert.Throws<ArgumentException>(() => reservation.Use(Now.AddMinutes(15), Guid.Empty));

        Assert.Equal("sessionId", ex.ParamName);
        Assert.Equal(statusBefore, reservation.Status);
        Assert.Equal(versionBefore, reservation.RowVersion);
        Assert.Null(reservation.UsedAt);
        Assert.Null(reservation.SessionId);
    }

    [Fact]
    public void Use_without_sessionId_succeeds_and_sets_SessionId_to_null()
    {
        var reservation = CreateSampleReservation();
        reservation.Confirm(Now.AddMinutes(2));

        reservation.Use(Now.AddMinutes(15));

        Assert.Equal(ReservationStatus.Used, reservation.Status);
        Assert.Equal(Now.AddMinutes(15), reservation.UsedAt);
        Assert.Null(reservation.SessionId);
        Assert.Equal(2, reservation.RowVersion);
    }

    [Fact]
    public void Cancel_with_NoShow_when_status_is_not_Confirmed_throws_and_preserves_state()
    {
        var reservation = CreateSampleReservation(); // TemporaryHold
        var initialVersion = reservation.RowVersion;
        var initialStatus = reservation.Status;
        var after15Min = reservation.AppointmentTime.AddMinutes(20);

        var ex = Assert.Throws<ReservationDomainException>(() =>
            reservation.Cancel(after15Min, CancelReason.NoShow, PrepaidOutcome.Forfeited));

        Assert.Contains("Only confirmed reservations", ex.Message);
        Assert.Equal(initialStatus, reservation.Status);
        Assert.Equal(initialVersion, reservation.RowVersion);
        Assert.Null(reservation.CancelledAt);
        Assert.Null(reservation.CancelReason);
        Assert.False(reservation.IsNoShow);
        Assert.Null(reservation.PrepaidOutcome);
    }

    [Fact]
    public void Cancel_with_NoShow_before_grace_period_elapsed_throws_and_preserves_state()
    {
        var reservation = CreateSampleReservation();
        reservation.Confirm(Now.AddMinutes(2));
        var versionAfterConfirm = reservation.RowVersion;

        // 15 minutes 59 seconds after AppointmentTime (1 second before 16 min cutoff)
        var beforeCutoff = reservation.AppointmentTime.AddMinutes(16).AddSeconds(-1);

        var ex = Assert.Throws<ReservationDomainException>(() =>
            reservation.Cancel(beforeCutoff, CancelReason.NoShow, PrepaidOutcome.Forfeited));

        Assert.Contains("after 15 minutes past its appointment time", ex.Message);
        Assert.Equal(ReservationStatus.Confirmed, reservation.Status);
        Assert.Equal(versionAfterConfirm, reservation.RowVersion);
        Assert.Null(reservation.CancelledAt);
        Assert.Null(reservation.CancelReason);
        Assert.False(reservation.IsNoShow);
        Assert.Null(reservation.PrepaidOutcome);
    }

    [Fact]
    public void Cancel_with_NoShow_exactly_at_cutoff_boundary_succeeds()
    {
        var reservation = CreateSampleReservation();
        reservation.Confirm(Now.AddMinutes(2));
        var exactlyCutoff = reservation.AppointmentTime.AddMinutes(16);

        reservation.Cancel(exactlyCutoff, CancelReason.NoShow, PrepaidOutcome.Forfeited);

        Assert.Equal(ReservationStatus.Cancelled, reservation.Status);
        Assert.Equal(CancelReason.NoShow, reservation.CancelReason);
        Assert.True(reservation.IsNoShow);
        Assert.Equal(PrepaidOutcome.Forfeited, reservation.PrepaidOutcome);
        Assert.Equal(exactlyCutoff.ToUniversalTime(), reservation.CancelledAt);
        Assert.Equal(TimeSpan.Zero, reservation.CancelledAt!.Value.Offset);
        Assert.Equal(2, reservation.RowVersion);
    }

    [Fact]
    public void Cancel_with_NoShow_after_cutoff_succeeds()
    {
        var reservation = CreateSampleReservation();
        reservation.Confirm(Now.AddMinutes(2));
        var afterCutoff = reservation.AppointmentTime.AddMinutes(17);

        reservation.Cancel(afterCutoff, CancelReason.NoShow, PrepaidOutcome.Forfeited);

        Assert.Equal(ReservationStatus.Cancelled, reservation.Status);
        Assert.Equal(CancelReason.NoShow, reservation.CancelReason);
        Assert.True(reservation.IsNoShow);
        Assert.Equal(2, reservation.RowVersion);
    }

    [Fact]
    public void Cancel_with_NoShow_evaluates_exact_same_instant_across_different_offsets()
    {
        var vnOffset = TimeSpan.FromHours(7);
        var vnAppointmentTime = new DateTimeOffset(2026, 10, 20, 10, 0, 0, vnOffset); // UTC 03:00:00
        var reservation = Reservation.CreateTemporaryHold(
            id: Guid.NewGuid(),
            userId: Guid.NewGuid(),
            vehicleId: Guid.NewGuid(),
            plateNumber: "51F-123.45",
            lotId: Guid.NewGuid(),
            vehicleGroup: VehicleGroup.Car,
            reservationType: ReservationType.Scheduled,
            appointmentTime: vnAppointmentTime,
            prepaidAmount: 20000m,
            createdAt: vnAppointmentTime.AddHours(-1));

        reservation.Confirm(vnAppointmentTime.AddMinutes(-30));

        // 15 min 59 sec in UTC (03:15:59 UTC) -> rejects
        var beforeBoundaryUtc = new DateTimeOffset(2026, 10, 20, 3, 15, 59, TimeSpan.Zero);
        Assert.Throws<ReservationDomainException>(() =>
            reservation.Cancel(beforeBoundaryUtc, CancelReason.NoShow));

        // Exactly 16 min in VN time (+07:00: 10:16:00) -> equals 03:16:00 UTC -> succeeds
        var exactBoundaryVn = new DateTimeOffset(2026, 10, 20, 10, 16, 0, vnOffset);
        reservation.Cancel(exactBoundaryVn, CancelReason.NoShow);

        Assert.Equal(ReservationStatus.Cancelled, reservation.Status);
        Assert.True(reservation.IsNoShow);
        Assert.Equal(new DateTimeOffset(2026, 10, 20, 3, 16, 0, TimeSpan.Zero), reservation.CancelledAt);
    }

    [Theory]
    [InlineData(CancelReason.CustomerCancelled)]
    [InlineData(CancelReason.PaymentFailed)]
    [InlineData(CancelReason.NoSuitableSlot)]
    public void Standard_cancel_reasons_are_not_restricted_by_NoShow_15_minute_grace_period(CancelReason reason)
    {
        var reservation = CreateSampleReservation();
        var cancelledAt = Now.AddMinutes(1);

        reservation.Cancel(cancelledAt, reason);

        Assert.Equal(ReservationStatus.Cancelled, reservation.Status);
        Assert.Equal(reason, reservation.CancelReason);
        Assert.False(reservation.IsNoShow);
        Assert.Equal(1, reservation.RowVersion);
    }

    [Theory]
    [InlineData(CancelReason.CustomerCancelled)]
    [InlineData(CancelReason.PaymentFailed)]
    [InlineData(CancelReason.NoSuitableSlot)]
    [InlineData(CancelReason.NoShow)]
    public void Cancel_when_reservation_is_Used_throws_and_preserves_state(CancelReason reason)
    {
        var reservation = CreateSampleReservation();
        reservation.Confirm(Now.AddMinutes(2));
        reservation.Use(Now.AddMinutes(15), Guid.NewGuid());
        var usedVersion = reservation.RowVersion;
        var usedAt = reservation.UsedAt;
        var sessionId = reservation.SessionId;

        var ex = Assert.Throws<ReservationDomainException>(() =>
            reservation.Cancel(reservation.AppointmentTime.AddMinutes(20), reason));

        Assert.Equal(ReservationStatus.Used, reservation.Status);
        Assert.Equal(usedVersion, reservation.RowVersion);
        Assert.Equal(usedAt, reservation.UsedAt);
        Assert.Equal(sessionId, reservation.SessionId);
        Assert.Null(reservation.CancelledAt);
        Assert.Null(reservation.CancelReason);
        Assert.False(reservation.IsNoShow);
    }


    [Fact]
    public void Cancel_with_undefined_CancelReason_throws_ArgumentOutOfRangeException_and_preserves_state()
    {
        var reservation = CreateSampleReservation();
        var initialVersion = reservation.RowVersion;
        var initialStatus = reservation.Status;

        var ex = Assert.Throws<ArgumentOutOfRangeException>(() =>
            reservation.Cancel(Now.AddMinutes(5), (CancelReason)999));

        Assert.Equal(initialStatus, reservation.Status);
        Assert.Equal(initialVersion, reservation.RowVersion);
        Assert.Null(reservation.CancelledAt);
        Assert.Null(reservation.CancelReason);
        Assert.False(reservation.IsNoShow);
        Assert.Null(reservation.PrepaidOutcome);
    }

    [Fact]
    public void Cancel_with_undefined_PrepaidOutcome_throws_ArgumentOutOfRangeException_and_preserves_state()
    {
        var reservation = CreateSampleReservation();
        var initialVersion = reservation.RowVersion;
        var initialStatus = reservation.Status;

        var ex = Assert.Throws<ArgumentOutOfRangeException>(() =>
            reservation.Cancel(Now.AddMinutes(5), CancelReason.CustomerCancelled, (PrepaidOutcome)999));

        Assert.Equal(initialStatus, reservation.Status);
        Assert.Equal(initialVersion, reservation.RowVersion);
        Assert.Null(reservation.CancelledAt);
        Assert.Null(reservation.CancelReason);
        Assert.False(reservation.IsNoShow);
        Assert.Null(reservation.PrepaidOutcome);
    }

    [Fact]
    public void Cancel_with_null_PrepaidOutcome_succeeds_as_optional()
    {
        var reservation = CreateSampleReservation();
        reservation.Cancel(Now.AddMinutes(5), CancelReason.CustomerCancelled, prepaidOutcome: null);

        Assert.Equal(ReservationStatus.Cancelled, reservation.Status);
        Assert.Null(reservation.PrepaidOutcome);
        Assert.Equal(1, reservation.RowVersion);
    }

    [Fact]
    public void Disallowed_transition_from_TemporaryHold_to_Used_throws_and_preserves_RowVersion()
    {
        var reservation = CreateSampleReservation();
        var initialVersion = reservation.RowVersion;

        var ex = Assert.Throws<ReservationDomainException>(() => reservation.Use(Now.AddMinutes(5), Guid.NewGuid()));
        Assert.Contains(ReservationStatus.TemporaryHold.ToString(), ex.Message);
        Assert.Contains(ReservationStatus.Used.ToString(), ex.Message);
        Assert.Equal(initialVersion, reservation.RowVersion);
        Assert.Equal(ReservationStatus.TemporaryHold, reservation.Status);
    }

    [Fact]
    public void Disallowed_transition_from_Confirmed_to_Expire_throws_and_preserves_RowVersion()
    {
        var reservation = CreateSampleReservation();
        reservation.Confirm(Now.AddMinutes(2));
        var versionAfterConfirm = reservation.RowVersion;

        var ex = Assert.Throws<ReservationDomainException>(() => reservation.Expire());
        Assert.Contains(ReservationStatus.Confirmed.ToString(), ex.Message);
        Assert.Contains(ReservationStatus.Expired.ToString(), ex.Message);
        Assert.Equal(versionAfterConfirm, reservation.RowVersion);
        Assert.Equal(ReservationStatus.Confirmed, reservation.Status);
    }

    [Fact]
    public void Repeated_Confirm_throws_and_preserves_RowVersion()
    {
        var reservation = CreateSampleReservation();
        reservation.Confirm(Now.AddMinutes(2));
        var versionAfterConfirm = reservation.RowVersion;

        var ex = Assert.Throws<ReservationDomainException>(() => reservation.Confirm(Now.AddMinutes(3)));
        Assert.Equal(versionAfterConfirm, reservation.RowVersion);
        Assert.Equal(ReservationStatus.Confirmed, reservation.Status);
    }

    [Fact]
    public void Used_is_terminal_and_rejects_further_transitions_without_altering_version()
    {
        var reservation = CreateSampleReservation();
        reservation.Confirm(Now.AddMinutes(2));
        reservation.Use(Now.AddMinutes(15), Guid.NewGuid());
        var terminalVersion = reservation.RowVersion;

        Assert.Throws<ReservationDomainException>(() => reservation.Confirm(Now.AddMinutes(20)));
        Assert.Throws<ReservationDomainException>(() => reservation.Use(Now.AddMinutes(20), Guid.NewGuid()));
        Assert.Throws<ReservationDomainException>(() => reservation.Cancel(Now.AddMinutes(20), CancelReason.CustomerCancelled));
        Assert.Throws<ReservationDomainException>(() => reservation.Expire());

        Assert.Equal(terminalVersion, reservation.RowVersion);
        Assert.Equal(ReservationStatus.Used, reservation.Status);
    }

    [Fact]
    public void Cancelled_is_terminal_and_rejects_further_transitions_without_altering_version()
    {
        var reservation = CreateSampleReservation();
        reservation.Cancel(Now.AddMinutes(5), CancelReason.CustomerCancelled);
        var terminalVersion = reservation.RowVersion;

        Assert.Throws<ReservationDomainException>(() => reservation.Confirm(Now.AddMinutes(10)));
        Assert.Throws<ReservationDomainException>(() => reservation.Use(Now.AddMinutes(10), Guid.NewGuid()));
        Assert.Throws<ReservationDomainException>(() => reservation.Cancel(Now.AddMinutes(10), CancelReason.NoShow));
        Assert.Throws<ReservationDomainException>(() => reservation.Expire());

        Assert.Equal(terminalVersion, reservation.RowVersion);
        Assert.Equal(ReservationStatus.Cancelled, reservation.Status);
    }

    [Fact]
    public void Expired_is_terminal_and_rejects_further_transitions_without_altering_version()
    {
        var reservation = CreateSampleReservation();
        reservation.Expire();
        var terminalVersion = reservation.RowVersion;

        Assert.Throws<ReservationDomainException>(() => reservation.Confirm(Now.AddMinutes(10)));
        Assert.Throws<ReservationDomainException>(() => reservation.Use(Now.AddMinutes(10), Guid.NewGuid()));
        Assert.Throws<ReservationDomainException>(() => reservation.Cancel(Now.AddMinutes(10), CancelReason.CustomerCancelled));
        Assert.Throws<ReservationDomainException>(() => reservation.Expire());

        Assert.Equal(terminalVersion, reservation.RowVersion);
        Assert.Equal(ReservationStatus.Expired, reservation.Status);
    }

    [Fact]
    public void CreateTemporaryHold_with_Car_and_HoldWhileDriving_succeeds()
    {
        var reservation = Reservation.CreateTemporaryHold(
            id: Guid.NewGuid(),
            userId: Guid.NewGuid(),
            vehicleId: Guid.NewGuid(),
            plateNumber: "51F-123.45",
            lotId: Guid.NewGuid(),
            vehicleGroup: VehicleGroup.Car,
            reservationType: ReservationType.HoldWhileDriving,
            appointmentTime: Now.AddMinutes(30),
            prepaidAmount: 20000m,
            createdAt: Now);

        Assert.NotNull(reservation);
        Assert.Equal(VehicleGroup.Car, reservation.VehicleGroup);
        Assert.Equal(ReservationType.HoldWhileDriving, reservation.ReservationType);
        Assert.Null(reservation.UsedAt);
    }

    [Fact]
    public void CreateTemporaryHold_with_Car_and_Scheduled_succeeds()
    {
        var reservation = Reservation.CreateTemporaryHold(
            id: Guid.NewGuid(),
            userId: Guid.NewGuid(),
            vehicleId: Guid.NewGuid(),
            plateNumber: "51F-123.45",
            lotId: Guid.NewGuid(),
            vehicleGroup: VehicleGroup.Car,
            reservationType: ReservationType.Scheduled,
            appointmentTime: Now.AddHours(2),
            prepaidAmount: 30000m,
            createdAt: Now);

        Assert.NotNull(reservation);
        Assert.Equal(VehicleGroup.Car, reservation.VehicleGroup);
        Assert.Equal(ReservationType.Scheduled, reservation.ReservationType);
        Assert.Null(reservation.UsedAt);
    }

    [Fact]
    public void CreateTemporaryHold_with_Motorbike_throws_ReservationDomainException()
    {
        var ex = Assert.Throws<ReservationDomainException>(() =>
            Reservation.CreateTemporaryHold(
                id: Guid.NewGuid(),
                userId: Guid.NewGuid(),
                vehicleId: Guid.NewGuid(),
                plateNumber: "59A-123.45",
                lotId: Guid.NewGuid(),
                vehicleGroup: VehicleGroup.Motorbike,
                reservationType: ReservationType.HoldWhileDriving,
                appointmentTime: Now.AddMinutes(30),
                prepaidAmount: 10000m,
                createdAt: Now));

        Assert.Contains("Only cars are allowed", ex.Message);
    }

    [Fact]
    public void CreateTemporaryHold_with_undefined_VehicleGroup_throws_ArgumentOutOfRangeException()
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() =>
            Reservation.CreateTemporaryHold(
                id: Guid.NewGuid(),
                userId: Guid.NewGuid(),
                vehicleId: Guid.NewGuid(),
                plateNumber: "51F-123.45",
                lotId: Guid.NewGuid(),
                vehicleGroup: (VehicleGroup)999,
                reservationType: ReservationType.HoldWhileDriving,
                appointmentTime: Now.AddMinutes(30),
                prepaidAmount: 20000m,
                createdAt: Now));

        Assert.Equal("vehicleGroup", ex.ParamName);
    }

    [Fact]
    public void CreateTemporaryHold_with_undefined_ReservationType_throws_ArgumentOutOfRangeException()
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() =>
            Reservation.CreateTemporaryHold(
                id: Guid.NewGuid(),
                userId: Guid.NewGuid(),
                vehicleId: Guid.NewGuid(),
                plateNumber: "51F-123.45",
                lotId: Guid.NewGuid(),
                vehicleGroup: VehicleGroup.Car,
                reservationType: (ReservationType)999,
                appointmentTime: Now.AddMinutes(30),
                prepaidAmount: 20000m,
                createdAt: Now));

        Assert.Equal("reservationType", ex.ParamName);
    }

    [Fact]
    public void PrepaidOutcome_enum_contains_only_approved_ERD_values()
    {
        var names = Enum.GetNames<PrepaidOutcome>();

        Assert.Equal(3, names.Length);
        Assert.Contains(nameof(PrepaidOutcome.Applied), names);
        Assert.Contains(nameof(PrepaidOutcome.Refunded), names);
        Assert.Contains(nameof(PrepaidOutcome.Forfeited), names);
        Assert.DoesNotContain("Held", names);
    }

    [Fact]
    public void UsedAt_is_null_for_new_and_non_used_reservations()
    {
        var reservation = CreateSampleReservation();
        Assert.Null(reservation.UsedAt);

        reservation.Confirm(Now.AddMinutes(5));
        Assert.Null(reservation.UsedAt);

        reservation.Cancel(Now.AddMinutes(10), CancelReason.CustomerCancelled);
        Assert.Null(reservation.UsedAt);
    }

    [Fact]
    public void DateTimeOffset_with_plus_seven_offset_normalizes_to_utc_instant()
    {
        // Specific requirement: Input 2026-10-20 10:00:00 +07:00 must normalize to 2026-10-20 03:00:00 +00:00
        var vnOffset = TimeSpan.FromHours(7);
        var vnAppointmentTime = new DateTimeOffset(2026, 10, 20, 10, 0, 0, vnOffset);
        var vnCreatedAt = new DateTimeOffset(2026, 10, 20, 9, 30, 0, vnOffset);
        var vnTempHoldExpiresAt = new DateTimeOffset(2026, 10, 20, 9, 40, 0, vnOffset);

        var reservation = Reservation.CreateTemporaryHold(
            id: Guid.NewGuid(),
            userId: Guid.NewGuid(),
            vehicleId: Guid.NewGuid(),
            plateNumber: "51F-123.45",
            lotId: Guid.NewGuid(),
            vehicleGroup: VehicleGroup.Car,
            reservationType: ReservationType.HoldWhileDriving,
            appointmentTime: vnAppointmentTime,
            prepaidAmount: 20000m,
            createdAt: vnCreatedAt,
            tempHoldExpiresAt: vnTempHoldExpiresAt);

        // Verify UTC offset (+00:00)
        Assert.Equal(TimeSpan.Zero, reservation.AppointmentTime.Offset);
        Assert.Equal(TimeSpan.Zero, reservation.CreatedAt.Offset);
        Assert.Equal(TimeSpan.Zero, reservation.TempHoldExpiresAt!.Value.Offset);

        // Verify normalized instant: 2026-10-20 03:00:00 +00:00
        var expectedUtcAppointment = new DateTimeOffset(2026, 10, 20, 3, 0, 0, TimeSpan.Zero);
        Assert.Equal(expectedUtcAppointment, reservation.AppointmentTime);

        // Verify instant round-trip to Vietnam timezone (+07:00)
        var convertedBackToVn = reservation.AppointmentTime.ToOffset(vnOffset);
        Assert.Equal(vnAppointmentTime, convertedBackToVn);
        Assert.Equal(10, convertedBackToVn.Hour);

        // Test lifecycle transitions with +07:00 inputs
        var vnConfirmedAt = new DateTimeOffset(2026, 10, 20, 9, 32, 0, vnOffset);
        reservation.Confirm(vnConfirmedAt);
        Assert.Equal(TimeSpan.Zero, reservation.ConfirmedAt!.Value.Offset);
        Assert.Equal(new DateTimeOffset(2026, 10, 20, 2, 32, 0, TimeSpan.Zero), reservation.ConfirmedAt.Value);
        Assert.Equal(vnConfirmedAt, reservation.ConfirmedAt.Value.ToOffset(vnOffset));

        var vnUsedAt = new DateTimeOffset(2026, 10, 20, 9, 50, 0, vnOffset);
        reservation.Use(vnUsedAt);
        Assert.Equal(TimeSpan.Zero, reservation.UsedAt!.Value.Offset);
        Assert.Equal(new DateTimeOffset(2026, 10, 20, 2, 50, 0, TimeSpan.Zero), reservation.UsedAt.Value);
        Assert.Equal(vnUsedAt, reservation.UsedAt.Value.ToOffset(vnOffset));
    }

    [Fact]
    public void DateTimeOffset_with_zero_offset_remains_utc_without_shifts()
    {
        var utcTime = new DateTimeOffset(2026, 10, 20, 3, 0, 0, TimeSpan.Zero);

        var reservation = Reservation.CreateTemporaryHold(
            id: Guid.NewGuid(),
            userId: Guid.NewGuid(),
            vehicleId: Guid.NewGuid(),
            plateNumber: "51F-123.45",
            lotId: Guid.NewGuid(),
            vehicleGroup: VehicleGroup.Car,
            reservationType: ReservationType.HoldWhileDriving,
            appointmentTime: utcTime,
            prepaidAmount: 20000m,
            createdAt: utcTime);

        Assert.Equal(TimeSpan.Zero, reservation.AppointmentTime.Offset);
        Assert.Equal(utcTime, reservation.AppointmentTime);
    }
}
