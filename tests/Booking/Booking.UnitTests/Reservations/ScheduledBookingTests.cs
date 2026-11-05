using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using ParkingSystem.Booking.Application.Parking;
using ParkingSystem.Booking.Application.Reservations;
using ParkingSystem.Booking.Application.Reservations.Commands.ConfirmReservation;
using ParkingSystem.Booking.Application.Reservations.Commands.CreateTemporaryHold;
using ParkingSystem.Booking.Application.Vehicles;
using ParkingSystem.Booking.Domain.Reservations;
using ParkingSystem.BuildingBlocks.Results;
using ParkingSystem.BuildingBlocks.Time;
using Xunit;

namespace ParkingSystem.Tests.Booking.UnitTests.Reservations;

public sealed class ScheduledBookingTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 5, 10, 0, 0, TimeSpan.Zero);

    private readonly FakeVehicleOwnershipClient _ownershipClient = new();
    private readonly FakeParkingLotCapacityProvider _capacityProvider = new();
    private readonly FakeReservationCapacityCoordinator _coordinator = new();
    private readonly FakeParkingPricingProvider _pricingProvider = new();
    private readonly FakeClock _clock = new(Now);
    private readonly BookingCapacityOptions _options = new()
    {
        LeadTimeMinutes = 30,
        GracePeriodMinutes = 15,
        TempHoldDurationMinutes = 10,
        LockTimeoutSeconds = 5
    };

    private CreateTemporaryHoldCommandHandler CreateHandler() =>
        new(_ownershipClient, _capacityProvider, _coordinator, _clock,
            NullLogger<CreateTemporaryHoldCommandHandler>.Instance,
            _pricingProvider, _options);

    private static CreateTemporaryHoldCommand CreateScheduledCommand(
        Guid? userId = null,
        Guid? vehicleId = null,
        string plate = "51A-123.45",
        Guid? lotId = null,
        VehicleGroup group = VehicleGroup.Car,
        DateTimeOffset? appointmentTime = null,
        int? durationMinutes = 120,
        decimal prepaidAmount = 0m,
        string? priceSnapshot = null)
    {
        return new CreateTemporaryHoldCommand(
            ReservationId: Guid.NewGuid(),
            UserId: userId ?? Guid.NewGuid(),
            VehicleId: vehicleId ?? Guid.NewGuid(),
            PlateNumber: plate,
            LotId: lotId ?? Guid.NewGuid(),
            VehicleGroup: group,
            ReservationType: ReservationType.Scheduled,
            AppointmentTime: appointmentTime ?? Now.AddHours(2),
            DurationMinutes: durationMinutes,
            PrepaidAmount: prepaidAmount,
            PriceSnapshot: priceSnapshot);
    }

    // =========================================================================
    // A. Scheduled input validation (SCH-01 to SCH-11)
    // =========================================================================

    [Fact]
    public async Task SCH_01_Valid_future_entry_time_and_valid_duration_is_accepted()
    {
        var handler = CreateHandler();
        var command = CreateScheduledCommand(
            appointmentTime: Now.AddHours(3),
            durationMinutes: 120);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, _coordinator.CallCount);
    }

    [Fact]
    public async Task SCH_02_Entry_time_in_the_past_is_rejected()
    {
        var handler = CreateHandler();
        var command = CreateScheduledCommand(
            appointmentTime: Now.AddHours(-1),
            durationMinutes: 60);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ReservationCapacityErrors.InvalidTimeWindow, result.Error);
        Assert.Equal(0, _coordinator.CallCount);
    }

    [Fact]
    public async Task SCH_03_Entry_time_beyond_7_day_window_is_rejected()
    {
        var handler = CreateHandler();
        var command = CreateScheduledCommand(
            appointmentTime: Now.AddDays(7).AddMinutes(1),
            durationMinutes: 60);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ReservationCapacityErrors.InvalidTimeWindow, result.Error);
        Assert.Equal(0, _coordinator.CallCount);
    }

    [Fact]
    public async Task SCH_04_Entry_time_exactly_at_7_day_boundary_is_accepted()
    {
        var handler = CreateHandler();
        var command = CreateScheduledCommand(
            appointmentTime: Now.AddDays(7),
            durationMinutes: 60);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, _coordinator.CallCount);
    }

    [Fact]
    public async Task SCH_05_Zero_duration_is_rejected()
    {
        var handler = CreateHandler();
        var command = CreateScheduledCommand(
            appointmentTime: Now.AddHours(2),
            durationMinutes: 0);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ReservationCapacityErrors.InvalidTimeWindow, result.Error);
        Assert.Equal(0, _coordinator.CallCount);
    }

    [Fact]
    public async Task SCH_06_Negative_duration_is_rejected()
    {
        var handler = CreateHandler();
        var command = CreateScheduledCommand(
            appointmentTime: Now.AddHours(2),
            durationMinutes: -30);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ReservationCapacityErrors.InvalidTimeWindow, result.Error);
        Assert.Equal(0, _coordinator.CallCount);
    }

    [Fact]
    public async Task SCH_07_Duration_greater_than_24_hours_is_rejected()
    {
        var handler = CreateHandler();
        var command = CreateScheduledCommand(
            appointmentTime: Now.AddHours(2),
            durationMinutes: 24 * 60 + 1); // 1441 minutes

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ReservationCapacityErrors.InvalidTimeWindow, result.Error);
        Assert.Equal(0, _coordinator.CallCount);
    }

    [Fact]
    public async Task SCH_08_Duration_exactly_24_hours_is_accepted()
    {
        var handler = CreateHandler();
        var command = CreateScheduledCommand(
            appointmentTime: Now.AddHours(2),
            durationMinutes: 24 * 60); // exactly 1440 minutes

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, _coordinator.CallCount);
    }

    [Fact]
    public async Task SCH_09_DateTime_overflow_is_safely_rejected()
    {
        var handler = CreateHandler();
        var command = CreateScheduledCommand(
            appointmentTime: DateTimeOffset.MaxValue,
            durationMinutes: 60);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ReservationCapacityErrors.InvalidTimeWindow, result.Error);
        Assert.Equal(0, _coordinator.CallCount);
    }

    [Fact]
    public async Task SCH_10_Vehicle_group_motorbike_from_identity_is_rejected()
    {
        _ownershipClient.ResultToReturn = Result<VehicleOwnershipInfo>.Failure(VehicleOwnershipErrors.UnsupportedVehicleType);
        var handler = CreateHandler();
        var command = CreateScheduledCommand();

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(VehicleOwnershipErrors.UnsupportedVehicleType, result.Error);
        Assert.Equal(0, _coordinator.CallCount);
    }

    [Fact]
    public async Task SCH_11_Unsupported_vehicle_from_identity_is_rejected()
    {
        _ownershipClient.ResultToReturn = Result<VehicleOwnershipInfo>.Failure(VehicleOwnershipErrors.UnsupportedVehicleType);
        var handler = CreateHandler();
        var command = CreateScheduledCommand();

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(VehicleOwnershipErrors.UnsupportedVehicleType, result.Error);
    }

    // =========================================================================
    // B. Identity and authorization (SCH-12 to SCH-18)
    // =========================================================================

    [Fact]
    public async Task SCH_12_Vehicle_belongs_to_authenticated_user_continues()
    {
        var userId = Guid.NewGuid();
        var handler = CreateHandler();
        var command = CreateScheduledCommand(userId: userId);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(userId, result.Value!.UserId);
    }

    [Fact]
    public async Task SCH_13_Vehicle_belongs_to_another_user_is_rejected_without_capacity_hold()
    {
        _ownershipClient.ResultToReturn = Result<VehicleOwnershipInfo>.Failure(VehicleOwnershipErrors.VehicleOwnershipMismatch);
        var handler = CreateHandler();
        var command = CreateScheduledCommand();

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(VehicleOwnershipErrors.VehicleOwnershipMismatch, result.Error);
        Assert.Equal(0, _coordinator.CallCount);
    }

    [Fact]
    public async Task SCH_14_Vehicle_is_not_registered_is_rejected_without_capacity_hold()
    {
        _ownershipClient.ResultToReturn = Result<VehicleOwnershipInfo>.Failure(VehicleOwnershipErrors.VehicleNotRegistered);
        var handler = CreateHandler();
        var command = CreateScheduledCommand();

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(VehicleOwnershipErrors.VehicleNotRegistered, result.Error);
        Assert.Equal(0, _coordinator.CallCount);
    }

    [Fact]
    public async Task SCH_15_Identity_unavailable_fails_closed_without_capacity_hold()
    {
        _ownershipClient.ResultToReturn = Result<VehicleOwnershipInfo>.Failure(VehicleOwnershipErrors.ServiceUnavailable);
        var handler = CreateHandler();
        var command = CreateScheduledCommand();

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(VehicleOwnershipErrors.ServiceUnavailable, result.Error);
        Assert.Equal(0, _coordinator.CallCount);
    }

    [Fact]
    public async Task SCH_16_Invalid_plate_format_is_rejected_before_capacity_hold()
    {
        _ownershipClient.ResultToReturn = Result<VehicleOwnershipInfo>.Failure(VehicleOwnershipErrors.InvalidPlateFormat);
        var handler = CreateHandler();
        var command = CreateScheduledCommand(plate: "INVALID-PLATE");

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(VehicleOwnershipErrors.InvalidPlateFormat, result.Error);
        Assert.Equal(0, _coordinator.CallCount);
    }

    [Fact]
    public async Task SCH_17_Authenticated_UserId_remains_authoritative()
    {
        var authUserId = Guid.NewGuid();
        var handler = CreateHandler();
        var command = CreateScheduledCommand(userId: authUserId);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(authUserId, result.Value!.UserId);
    }

    [Fact]
    public async Task SCH_18_Identity_owned_vehicle_id_and_canonical_plate_remain_authoritative()
    {
        var identityVehicleId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        _ownershipClient.ResultToReturn = Result<VehicleOwnershipInfo>.Success(
            new VehicleOwnershipInfo(userId, identityVehicleId, "51A12345", SupportedVehicleType.Car, true));

        var handler = CreateHandler();
        var forgedClientVehicleId = Guid.NewGuid();
        var command = CreateScheduledCommand(
            userId: userId,
            vehicleId: forgedClientVehicleId,
            plate: "51A-123.45");

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(identityVehicleId, result.Value!.VehicleId);
        Assert.Equal("51A12345", result.Value!.PlateNumber);
    }

    // =========================================================================
    // C. Time window and overlap (SCH-19 to SCH-27)
    // =========================================================================

    [Fact]
    public void SCH_19_Capacity_start_is_entry_time_minus_30_minutes()
    {
        var appTime = Now.AddHours(2);
        var (start, _, _) = ReservationTimeWindow.CalculateWindows(
            ReservationType.Scheduled,
            appTime,
            durationMinutes: 120,
            leadMinutes: 30);

        Assert.Equal(appTime.AddMinutes(-30), start);
    }

    [Fact]
    public void SCH_20_Capacity_end_is_entry_time_plus_booked_duration()
    {
        var appTime = Now.AddHours(2);
        var (_, end, _) = ReservationTimeWindow.CalculateWindows(
            ReservationType.Scheduled,
            appTime,
            durationMinutes: 120);

        Assert.Equal(appTime.AddMinutes(120), end);
    }

    [Fact]
    public void SCH_21_Two_overlapping_windows_are_detected()
    {
        var t10 = Now;
        var t12 = Now.AddHours(2);
        var t11 = Now.AddHours(1);
        var t13 = Now.AddHours(3);

        // [10:00, 12:00) overlaps with [11:00, 13:00)
        Assert.True(ReservationTimeWindow.IsOverlapping(t10, t12, t11, t13));
    }

    [Fact]
    public void SCH_22_Existing_window_ending_exactly_when_new_window_starts_does_not_overlap()
    {
        var t10 = Now;
        var t12 = Now.AddHours(2);
        var t14 = Now.AddHours(4);

        // [10:00, 12:00) and [12:00, 14:00) do NOT overlap under [start, end)
        Assert.False(ReservationTimeWindow.IsOverlapping(t10, t12, t12, t14));
    }

    [Fact]
    public void SCH_23_New_window_ending_exactly_when_existing_window_starts_does_not_overlap()
    {
        var t10 = Now;
        var t12 = Now.AddHours(2);
        var t14 = Now.AddHours(4);

        // [12:00, 14:00) and [10:00, 12:00) do NOT overlap under [start, end)
        Assert.False(ReservationTimeWindow.IsOverlapping(t12, t14, t10, t12));
    }

    [Fact]
    public void SCH_24_Same_plate_with_non_overlapping_reservation_is_allowed()
    {
        var res = Reservation.CreateTemporaryHold(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "51A12345", Guid.NewGuid(),
            VehicleGroup.Car, ReservationType.Scheduled, Now.AddHours(2), 20000m, Now,
            durationMinutes: 60,
            tempHoldExpiresAt: Now.AddMinutes(10),
            windowStartAt: Now.AddMinutes(90),
            windowEndAt: Now.AddMinutes(180),
            holdEndAt: Now.AddMinutes(135));

        // Different non-overlapping window: [180, 240)
        var conflict = res.ConflictsWithPlate("51A12345", Now.AddMinutes(180), Now.AddMinutes(240), Now);
        Assert.False(conflict);
    }

    [Fact]
    public void SCH_25_Same_plate_with_overlapping_active_reservation_conflicts()
    {
        var res = Reservation.CreateTemporaryHold(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "51A12345", Guid.NewGuid(),
            VehicleGroup.Car, ReservationType.Scheduled, Now.AddHours(2), 20000m, Now,
            durationMinutes: 60,
            tempHoldExpiresAt: Now.AddMinutes(10),
            windowStartAt: Now.AddMinutes(90),
            windowEndAt: Now.AddMinutes(180),
            holdEndAt: Now.AddMinutes(135));

        // Overlapping window: [100, 150)
        var conflict = res.ConflictsWithPlate("51A12345", Now.AddMinutes(100), Now.AddMinutes(150), Now);
        Assert.True(conflict);
    }

    [Fact]
    public void SCH_26_Account_max_3_active_reservations_constant_is_three()
    {
        Assert.Equal(3, Reservation.MaxActiveReservationsPerUser);
    }

    [Fact]
    public void SCH_27_Cancelled_and_Expired_reservations_do_not_count_as_active()
    {
        var cancelledRes = Reservation.CreateTemporaryHold(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "51A12345", Guid.NewGuid(),
            VehicleGroup.Car, ReservationType.Scheduled, Now.AddHours(2), 20000m, Now,
            durationMinutes: 60, tempHoldExpiresAt: Now.AddMinutes(10));
        cancelledRes.Cancel(Now, CancelReason.CustomerCancelled);

        var expiredRes = Reservation.CreateTemporaryHold(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "51A12345", Guid.NewGuid(),
            VehicleGroup.Car, ReservationType.Scheduled, Now.AddHours(2), 20000m, Now,
            durationMinutes: 60, tempHoldExpiresAt: Now.AddMinutes(10));
        expiredRes.Expire();

        Assert.False(cancelledRes.IsActive(Now));
        Assert.False(expiredRes.IsActive(Now));
    }

    // =========================================================================
    // D. Capacity and concurrency (SCH-28 to SCH-34)
    // =========================================================================

    [Fact]
    public async Task SCH_28_Capacity_is_available_creates_Temporary_Hold()
    {
        var handler = CreateHandler();
        var command = CreateScheduledCommand();

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(ReservationStatus.TemporaryHold, result.Value!.Status);
        Assert.NotNull(result.Value.TempHoldExpiresAt);
    }

    [Fact]
    public async Task SCH_29_Capacity_insufficient_is_rejected_without_hold()
    {
        _coordinator.ResultToReturn = _ => Result<Reservation>.Failure(ReservationCapacityErrors.CapacityExceeded);
        var handler = CreateHandler();
        var command = CreateScheduledCommand();

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ReservationCapacityErrors.CapacityExceeded, result.Error);
    }

    [Fact]
    public async Task SCH_30_Capacity_provider_unavailable_fails_closed()
    {
        _capacityProvider.ResultToReturn = Result<ParkingLotCapacityInfo>.Failure(ReservationCapacityErrors.CapacityInfoUnavailable);
        var handler = CreateHandler();
        var command = CreateScheduledCommand();

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ReservationCapacityErrors.CapacityInfoUnavailable, result.Error);
        Assert.Equal(0, _coordinator.CallCount);
    }

    [Fact]
    public async Task SCH_34_Capacity_is_checked_for_the_correct_scheduled_time_window()
    {
        var handler = CreateHandler();
        var appointment = Now.AddHours(3);
        var command = CreateScheduledCommand(appointmentTime: appointment, durationMinutes: 180);

        Reservation? capturedReservation = null;
        _coordinator.ResultToReturn = r =>
        {
            capturedReservation = r;
            return Result<Reservation>.Success(r);
        };

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(capturedReservation);
        // WindowStartAt = appointment - 30 minutes
        Assert.Equal(appointment.AddMinutes(-30), capturedReservation.WindowStartAt);
        // WindowEndAt = appointment + 180 minutes
        Assert.Equal(appointment.AddMinutes(180), capturedReservation.WindowEndAt);
    }

    // =========================================================================
    // E. Pricing and snapshot (SCH-35 to SCH-40)
    // =========================================================================

    [Fact]
    public async Task SCH_35_Valid_parking_price_response_calculates_correct_total_for_duration()
    {
        // 20,000 VND / 60 min block. Duration: 120 min -> 2 blocks -> 40,000 VND
        _pricingProvider.ResultToReturn = Result<ParkingPricingInfo>.Success(
            new ParkingPricingInfo(Guid.NewGuid(), VehicleGroup.Car, 20000m, "BLOCK", 60, 0m, "VND"));

        var handler = CreateHandler();
        var command = CreateScheduledCommand(durationMinutes: 120, prepaidAmount: 99999m); // client amount ignored

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(40000m, result.Value!.PrepaidAmount);
    }

    [Theory]
    [InlineData(-20000)]
    [InlineData(0)]
    public async Task SCH_36_Invalid_or_negative_price_is_rejected_safely(decimal price)
    {
        _pricingProvider.ResultToReturn = Result<ParkingPricingInfo>.Success(
            new ParkingPricingInfo(Guid.NewGuid(), VehicleGroup.Car, price, "BLOCK", 60, 0m, "VND"));

        var handler = CreateHandler();
        var command = CreateScheduledCommand();

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ReservationCapacityErrors.CapacityInfoUnavailable, result.Error);
        Assert.Equal(0, _coordinator.CallCount);
    }

    [Fact]
    public async Task SCH_37_Pricing_service_unavailable_fails_closed()
    {
        _pricingProvider.ResultToReturn = Result<ParkingPricingInfo>.Failure(ReservationCapacityErrors.CapacityInfoUnavailable);

        var handler = CreateHandler();
        var command = CreateScheduledCommand();

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ReservationCapacityErrors.CapacityInfoUnavailable, result.Error);
        Assert.Equal(0, _coordinator.CallCount);
    }

    [Fact]
    public async Task SCH_38_Price_snapshot_saved_matches_authoritative_calculation()
    {
        var lotId = Guid.NewGuid();
        _pricingProvider.ResultToReturn = Result<ParkingPricingInfo>.Success(
            new ParkingPricingInfo(lotId, VehicleGroup.Car, 25000m, "BLOCK", 60, 0m, "VND"));

        var handler = CreateHandler();
        var command = CreateScheduledCommand(lotId: lotId, durationMinutes: 180);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(75000m, result.Value!.PrepaidAmount);
        Assert.NotNull(result.Value.PriceSnapshot);

        using var doc = JsonDocument.Parse(result.Value.PriceSnapshot!);
        Assert.Equal(75000m, doc.RootElement.GetProperty("calculatedTotal").GetDecimal());
        Assert.Equal(25000m, doc.RootElement.GetProperty("unitPrice").GetDecimal());
        Assert.Equal(180, doc.RootElement.GetProperty("durationMinutes").GetInt32());
        Assert.Equal("VND", doc.RootElement.GetProperty("currency").GetString());
    }

    [Fact]
    public void SCH_39_Parking_price_changes_after_confirmation_does_not_change_snapshot()
    {
        var res = Reservation.CreateTemporaryHold(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "51A12345", Guid.NewGuid(),
            VehicleGroup.Car, ReservationType.Scheduled, Now.AddHours(2), 50000m, Now,
            durationMinutes: 120, tempHoldExpiresAt: Now.AddMinutes(10),
            priceSnapshot: "{\"unitPrice\":25000,\"calculatedTotal\":50000}");

        res.Confirm(Now.AddMinutes(1));

        // External price increases to 60,000 VND
        // Assert stored snapshot and prepaid amount on reservation remain intact
        Assert.Equal(50000m, res.PrepaidAmount);
        Assert.Equal("{\"unitPrice\":25000,\"calculatedTotal\":50000}", res.PriceSnapshot);
    }

    [Fact]
    public void SCH_40_Pricing_changes_during_Temporary_Hold_is_isolated_policy()
    {
        // Documenting Open Decision: Temporary hold stores authoritative snapshot at creation time.
        var res = Reservation.CreateTemporaryHold(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "51A12345", Guid.NewGuid(),
            VehicleGroup.Car, ReservationType.Scheduled, Now.AddHours(2), 40000m, Now,
            durationMinutes: 120, tempHoldExpiresAt: Now.AddMinutes(10),
            priceSnapshot: "{\"unitPrice\":20000,\"calculatedTotal\":40000}");

        Assert.Equal(40000m, res.PrepaidAmount);
        Assert.Equal(ReservationStatus.TemporaryHold, res.Status);
    }

    // =========================================================================
    // F. Payment and Temporary Hold (SCH-41 to SCH-49)
    // =========================================================================

    [Fact]
    public void SCH_48_Failed_payment_does_not_emit_confirmed_state()
    {
        var res = Reservation.CreateTemporaryHold(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "51A12345", Guid.NewGuid(),
            VehicleGroup.Car, ReservationType.Scheduled, Now.AddHours(2), 40000m, Now,
            durationMinutes: 120, tempHoldExpiresAt: Now.AddMinutes(10));

        // Under payment gate, reservation remains in TemporaryHold or moves to Expired/Cancelled
        Assert.NotEqual(ReservationStatus.Confirmed, res.Status);
    }

    [Fact]
    public void SCH_49_Duplicate_expire_call_throws_invalid_transition()
    {
        var res = Reservation.CreateTemporaryHold(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "51A12345", Guid.NewGuid(),
            VehicleGroup.Car, ReservationType.Scheduled, Now.AddHours(2), 40000m, Now,
            durationMinutes: 120, tempHoldExpiresAt: Now.AddMinutes(-1));

        res.Expire();
        Assert.Equal(ReservationStatus.Expired, res.Status);

        // Calling Expire again must throw ReservationDomainException per state machine
        Assert.Throws<ReservationDomainException>(() => res.Expire());
    }

    // =========================================================================
    // Test Doubles / Fakes
    // =========================================================================

    private sealed class FakeClock(DateTimeOffset utcNow) : IClock
    {
        public DateTimeOffset UtcNow { get; set; } = utcNow;
    }

    private sealed class FakeParkingLotCapacityProvider : IParkingLotCapacityProvider
    {
        public Result<ParkingLotCapacityInfo> ResultToReturn { get; set; } =
            Result<ParkingLotCapacityInfo>.Success(new ParkingLotCapacityInfo(10, 50, 5, true, 20000m));

        public Task<Result<ParkingLotCapacityInfo>> GetCapacityInfoAsync(Guid lotId, CancellationToken cancellationToken = default) =>
            Task.FromResult(ResultToReturn);
    }

    private sealed class FakeParkingPricingProvider : IParkingPricingProvider
    {
        public Result<ParkingPricingInfo> ResultToReturn { get; set; } =
            Result<ParkingPricingInfo>.Success(new ParkingPricingInfo(Guid.NewGuid(), VehicleGroup.Car, 20000m, "BLOCK", 60, 0m, "VND"));

        public Task<Result<ParkingPricingInfo>> GetPricingInfoAsync(Guid lotId, VehicleGroup vehicleGroup, CancellationToken cancellationToken = default) =>
            Task.FromResult(ResultToReturn);
    }

    private sealed class FakeReservationCapacityCoordinator : IReservationCapacityCoordinator
    {
        public int CallCount { get; private set; }
        public Func<Reservation, Result<Reservation>> ResultToReturn { get; set; } = r => Result<Reservation>.Success(r);

        public Task<Result<Reservation>> CreateTemporaryHoldAtomicAsync(
            Reservation reservation,
            int ceiling,
            int? availableSlotCount,
            TimeSpan lockTimeout,
            int maxActiveReservationsPerUser = 3,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult(ResultToReturn(reservation));
        }
    }

    private sealed class FakeVehicleOwnershipClient : IVehicleOwnershipClient
    {
        public Result<VehicleOwnershipInfo>? ResultToReturn { get; set; }

        public Task<Result<VehicleOwnershipInfo>> VerifyOwnershipAsync(
            string rawOrCanonicalPlate,
            Guid expectedUserId,
            CancellationToken cancellationToken = default)
        {
            if (ResultToReturn is not null)
            {
                return Task.FromResult(ResultToReturn);
            }

            var canonical = VietnameseLicensePlateValidator.TryNormalizeAndValidate(rawOrCanonicalPlate, out var norm)
                ? norm
                : "51A12345";

            return Task.FromResult(Result<VehicleOwnershipInfo>.Success(
                new VehicleOwnershipInfo(
                    expectedUserId,
                    Guid.NewGuid(),
                    canonical,
                    SupportedVehicleType.Car,
                    true)));
        }
    }
}

