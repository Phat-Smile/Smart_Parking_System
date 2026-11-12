using Microsoft.Extensions.Logging.Abstractions;
using ParkingSystem.Booking.Application.Parking;
using ParkingSystem.Booking.Application.Reservations;
using ParkingSystem.Booking.Application.Reservations.Commands.CreateTemporaryHold;
using ParkingSystem.Booking.Application.Vehicles;
using ParkingSystem.Booking.Domain.Reservations;
using ParkingSystem.BuildingBlocks.Results;
using ParkingSystem.BuildingBlocks.Time;
using Xunit;

namespace ParkingSystem.Tests.Booking.UnitTests.Reservations;

public sealed class CreateTemporaryHoldCommandHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 15, 10, 0, 0, TimeSpan.Zero);

    private readonly FakeVehicleOwnershipClient _ownershipClient = new();
    private readonly FakeParkingLotCapacityProvider _capacityProvider = new();
    private readonly FakeReservationCapacityCoordinator _coordinator = new();
    private readonly FakeClock _clock = new(Now);
    private readonly BookingCapacityOptions _options = new()
    {
        LeadTimeMinutes = 30,
        GracePeriodMinutes = 15,
        TempHoldDurationMinutes = 10,
        LockTimeoutSeconds = 5
    };

    private readonly FakeParkingPricingProvider _pricingProvider = new();

    private CreateTemporaryHoldCommandHandler CreateHandler()
    {
        return new CreateTemporaryHoldCommandHandler(
            _ownershipClient,
            _capacityProvider,
            _coordinator,
            _clock,
            NullLogger<CreateTemporaryHoldCommandHandler>.Instance,
            _pricingProvider,
            _options);
    }

    // ==========================================
    // U6: Port được gọi TRƯỚC khi mở transaction
    // ==========================================
    [Fact]
    public async Task U6_Port_is_called_before_coordinator_and_call_order_is_strictly_enforced()
    {
        var callOrder = new List<string>();

        _capacityProvider.OnCall = () => callOrder.Add("CapacityProvider.GetCapacityInfoAsync");
        _capacityProvider.ResultToReturn = Result<ParkingLotCapacityInfo>.Success(new ParkingLotCapacityInfo(10, 50, 5, true, 0m));

        _coordinator.OnCall = () => callOrder.Add("Coordinator.CreateTemporaryHoldAtomicAsync");
        _coordinator.ResultToReturn = r => Result<Reservation>.Success(r);

        var handler = CreateHandler();
        var command = new CreateTemporaryHoldCommand(
            ReservationId: Guid.NewGuid(),
            UserId: Guid.NewGuid(),
            VehicleId: Guid.NewGuid(),
            PlateNumber: "51F-111.11",
            LotId: Guid.NewGuid(),
            VehicleGroup: VehicleGroup.Car,
            ReservationType: ReservationType.Scheduled,
            AppointmentTime: Now.AddHours(2),
            DurationMinutes: 60,
            PrepaidAmount: 20000m);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, callOrder.Count);
        Assert.Equal("CapacityProvider.GetCapacityInfoAsync", callOrder[0]);
        Assert.Equal("Coordinator.CreateTemporaryHoldAtomicAsync", callOrder[1]);
    }

    // ==========================================
    // U7: Điều kiện (ii) chỉ áp dụng khi khung đã bắt đầu
    // ==========================================
    [Fact]
    public async Task U7_Condition_ii_is_skipped_for_future_reservation_even_if_available_slots_is_zero()
    {
        // Future reservation: appointment at 12:00, lead 30m -> window starts at 11:30.
        // Now is 10:00 (now < windowStartAt).
        // Even if AvailableSlotCount = 0, condition (ii) is NOT evaluated yet (availableSlotCount parameter passed is null).
        _capacityProvider.ResultToReturn = Result<ParkingLotCapacityInfo>.Success(new ParkingLotCapacityInfo(10, 50, 0, true, 0m));
        _coordinator.ResultToReturn = r => Result<Reservation>.Success(r);

        var handler = CreateHandler();
        var command = new CreateTemporaryHoldCommand(
            ReservationId: Guid.NewGuid(),
            UserId: Guid.NewGuid(),
            VehicleId: Guid.NewGuid(),
            PlateNumber: "51F-111.11",
            LotId: Guid.NewGuid(),
            VehicleGroup: VehicleGroup.Car,
            ReservationType: ReservationType.Scheduled,
            AppointmentTime: Now.AddHours(2),
            DurationMinutes: 60,
            PrepaidAmount: 20000m);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, _coordinator.CallCount);
        Assert.Equal(5, _coordinator.LastCeiling);
        Assert.Null(_coordinator.LastAvailableSlotCount); // Null because window has not started!
    }

    [Fact]
    public async Task U7_Condition_ii_is_applied_when_reservation_window_has_started()
    {
        // Window already started: appointment at 10:15, lead 30m -> window started at 09:45.
        // Now is 10:00 (now >= windowStartAt).
        // AvailableSlotCount = 3 -> Passed to coordinator!
        _capacityProvider.ResultToReturn = Result<ParkingLotCapacityInfo>.Success(new ParkingLotCapacityInfo(10, 50, 3, true, 0m));
        _coordinator.ResultToReturn = r => Result<Reservation>.Success(r);

        var handler = CreateHandler();
        var command = new CreateTemporaryHoldCommand(
            ReservationId: Guid.NewGuid(),
            UserId: Guid.NewGuid(),
            VehicleId: Guid.NewGuid(),
            PlateNumber: "51F-111.11",
            LotId: Guid.NewGuid(),
            VehicleGroup: VehicleGroup.Car,
            ReservationType: ReservationType.HoldWhileDriving,
            AppointmentTime: Now.AddMinutes(15),
            DurationMinutes: null,
            PrepaidAmount: 20000m);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, _coordinator.CallCount);
        Assert.Equal(5, _coordinator.LastCeiling);
        Assert.Equal(3, _coordinator.LastAvailableSlotCount); // Passed because window has started!
    }

    // ==========================================
    // U8: Port lỗi/quá deadline/dữ liệu bất thường -> CapacityInfoUnavailable, không ghi DB
    // ==========================================
    [Fact]
    public async Task U8_When_port_fails_returns_CapacityInfoUnavailable_and_never_calls_coordinator()
    {
        _capacityProvider.ResultToReturn = Result<ParkingLotCapacityInfo>.Failure(new Error("parking.error", "Parking service timeout"));

        var handler = CreateHandler();
        var command = new CreateTemporaryHoldCommand(
            ReservationId: Guid.NewGuid(),
            UserId: Guid.NewGuid(),
            VehicleId: Guid.NewGuid(),
            PlateNumber: "51F-111.11",
            LotId: Guid.NewGuid(),
            VehicleGroup: VehicleGroup.Car,
            ReservationType: ReservationType.HoldWhileDriving,
            AppointmentTime: Now.AddMinutes(20),
            DurationMinutes: null,
            PrepaidAmount: 20000m);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ReservationCapacityErrors.CapacityInfoUnavailable.Code, result.Error!.Code);
        Assert.Equal(0, _coordinator.CallCount); // Coordinator never called!
    }

    [Theory]
    [InlineData(-1, 50, 5)]  // Negative valid capacity
    [InlineData(10, -5, 5)]  // Negative ratio
    [InlineData(10, 105, 5)] // Ratio > 100
    [InlineData(10, 50, -1)] // Negative available slots
    public async Task U8_When_port_returns_abnormal_data_returns_CapacityInfoUnavailable_and_never_calls_coordinator(
        int validCapacity, int ratio, int availableSlots)
    {
        _capacityProvider.ResultToReturn = Result<ParkingLotCapacityInfo>.Success(new ParkingLotCapacityInfo(validCapacity, ratio, availableSlots, true, 0m));

        var handler = CreateHandler();
        var command = new CreateTemporaryHoldCommand(
            ReservationId: Guid.NewGuid(),
            UserId: Guid.NewGuid(),
            VehicleId: Guid.NewGuid(),
            PlateNumber: "51F-111.11",
            LotId: Guid.NewGuid(),
            VehicleGroup: VehicleGroup.Car,
            ReservationType: ReservationType.HoldWhileDriving,
            AppointmentTime: Now.AddMinutes(20),
            DurationMinutes: null,
            PrepaidAmount: 20000m);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ReservationCapacityErrors.CapacityInfoUnavailable.Code, result.Error!.Code);
        Assert.Equal(0, _coordinator.CallCount);
    }

    // ==========================================
    // U9: Cấu hình tùy chỉnh (Options binding verification)
    // ==========================================
    [Fact]
    public async Task U9_CustomOptions_configure_lead_grace_hold_and_lock_timeout_correctly()
    {
        var customOptions = new BookingCapacityOptions
        {
            LeadTimeMinutes = 45,
            GracePeriodMinutes = 20,
            TempHoldDurationMinutes = 15,
            LockTimeoutSeconds = 8
        };

        var fakePricing = new FakeParkingPricingProvider();
        fakePricing.ResultToReturn = Result<ParkingPricingInfo>.Success(
            new ParkingPricingInfo(Guid.NewGuid(), VehicleGroup.Car, 20000m, "PerEntry", null, 0m));

        var handler = new CreateTemporaryHoldCommandHandler(
            _ownershipClient,
            _capacityProvider,
            _coordinator,
            _clock,
            NullLogger<CreateTemporaryHoldCommandHandler>.Instance,
            fakePricing,
            customOptions);

        _capacityProvider.ResultToReturn = Result<ParkingLotCapacityInfo>.Success(new ParkingLotCapacityInfo(10, 50, 5, true, 0m));
        Reservation? capturedReservation = null;

        _coordinator.ResultToReturn = r =>
        {
            capturedReservation = r;
            return Result<Reservation>.Success(r);
        };

        var appointment = Now.AddHours(2);
        var command = new CreateTemporaryHoldCommand(
            ReservationId: Guid.NewGuid(),
            UserId: Guid.NewGuid(),
            VehicleId: Guid.NewGuid(),
            PlateNumber: "51F-CUSTOM",
            LotId: Guid.NewGuid(),
            VehicleGroup: VehicleGroup.Car,
            ReservationType: ReservationType.Scheduled,
            AppointmentTime: appointment,
            DurationMinutes: 60,
            PrepaidAmount: 20000m);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(capturedReservation);
        // Window start = appointment - 45m
        Assert.Equal(appointment.AddMinutes(-45), capturedReservation.WindowStartAt);
        // Hold end = appointment + 20m
        Assert.Equal(appointment.AddMinutes(20), capturedReservation.HoldEndAt);
        // Temp hold expires = Now + 15m
        Assert.Equal(Now.AddMinutes(15), capturedReservation.TempHoldExpiresAt);
        // Lock timeout = 8s
        Assert.Equal(TimeSpan.FromSeconds(8), _coordinator.LastLockTimeout);
    }

    // ==========================================
    // Test Doubles / Fakes
    // ==========================================
    private sealed class FakeClock(DateTimeOffset utcNow) : IClock
    {
        public DateTimeOffset UtcNow { get; set; } = utcNow;
    }

    private sealed class FakeParkingLotCapacityProvider : IParkingLotCapacityProvider
    {
        public Action? OnCall { get; set; }
        public Result<ParkingLotCapacityInfo> ResultToReturn { get; set; } = Result<ParkingLotCapacityInfo>.Success(new ParkingLotCapacityInfo(10, 50, 5, true, 0m));

        public Task<Result<ParkingLotCapacityInfo>> GetCapacityInfoAsync(Guid lotId, CancellationToken cancellationToken = default)
        {
            OnCall?.Invoke();
            return Task.FromResult(ResultToReturn);
        }
    }

    private sealed class FakeReservationCapacityCoordinator : IReservationCapacityCoordinator
    {
        public Action? OnCall { get; set; }
        public int CallCount { get; private set; }
        public int LastCeiling { get; private set; }
        public int? LastAvailableSlotCount { get; private set; }
        public TimeSpan LastLockTimeout { get; private set; }
        public int LastMaxActiveReservationsPerUser { get; private set; }
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
            LastCeiling = ceiling;
            LastAvailableSlotCount = availableSlotCount;
            LastLockTimeout = lockTimeout;
            LastMaxActiveReservationsPerUser = maxActiveReservationsPerUser;
            OnCall?.Invoke();
            return Task.FromResult(ResultToReturn(reservation));
        }
    }

    private sealed class FakeVehicleOwnershipClient : IVehicleOwnershipClient
    {
        public Func<string, Guid, CancellationToken, Task<Result<VehicleOwnershipInfo>>>? OnVerify { get; set; }
        public Action? OnCall { get; set; }

        public Task<Result<VehicleOwnershipInfo>> VerifyOwnershipAsync(
            string rawOrCanonicalPlate,
            Guid expectedUserId,
            CancellationToken cancellationToken = default)
        {
            OnCall?.Invoke();
            if (OnVerify is not null)
            {
                return OnVerify(rawOrCanonicalPlate, expectedUserId, cancellationToken);
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

    private sealed class FakeParkingPricingProvider : IParkingPricingProvider
    {
        public Result<ParkingPricingInfo> ResultToReturn { get; set; } =
            Result<ParkingPricingInfo>.Success(new ParkingPricingInfo(Guid.NewGuid(), VehicleGroup.Car, 20000m, "PerEntry", null, 0m));

        public Task<Result<ParkingPricingInfo>> GetPricingInfoAsync(
            Guid lotId,
            VehicleGroup vehicleGroup,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(ResultToReturn);
        }
    }
}
