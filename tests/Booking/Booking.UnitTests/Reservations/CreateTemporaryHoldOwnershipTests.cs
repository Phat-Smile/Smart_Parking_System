using Microsoft.Extensions.Logging;
using ParkingSystem.Booking.Application.Parking;
using ParkingSystem.Booking.Application.Reservations;
using ParkingSystem.Booking.Application.Reservations.Commands.CreateTemporaryHold;
using ParkingSystem.Booking.Application.Vehicles;
using ParkingSystem.Booking.Domain.Reservations;
using ParkingSystem.BuildingBlocks.Results;
using ParkingSystem.BuildingBlocks.Time;
using Xunit;

namespace ParkingSystem.Tests.Booking.UnitTests.Reservations;

public sealed class CreateTemporaryHoldOwnershipTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 4, 10, 0, 0, TimeSpan.Zero);

    private readonly FakeVehicleOwnershipClient _ownershipClient = new();
    private readonly FakeParkingLotCapacityProvider _capacityProvider = new();
    private readonly FakeReservationCapacityCoordinator _coordinator = new();
    private readonly FakeClock _clock = new(Now);
    private readonly TestLogger<CreateTemporaryHoldCommandHandler> _logger = new();
    private readonly BookingCapacityOptions _options = new()
    {
        LeadTimeMinutes = 30,
        GracePeriodMinutes = 15,
        TempHoldDurationMinutes = 10,
        LockTimeoutSeconds = 5
    };

    public CreateTemporaryHoldOwnershipTests()
    {
        _capacityProvider.ResultToReturn = Result<ParkingLotCapacityInfo>.Success(new ParkingLotCapacityInfo(20, 50, 10, true, 0m));
        _coordinator.ResultToReturn = r => Result<Reservation>.Success(r);
    }

    private CreateTemporaryHoldCommandHandler CreateHandler() =>
        new(_ownershipClient, _capacityProvider, _coordinator, _clock, _logger, new UnconfiguredParkingPricingProvider(), _options);

    private static CreateTemporaryHoldCommand CreateCommand(
        Guid? userId = null,
        string plate = "51A-123.45",
        ReservationType resType = ReservationType.HoldWhileDriving,
        VehicleGroup group = VehicleGroup.Car,
        Guid? vehicleId = null)
    {
        return new CreateTemporaryHoldCommand(
            ReservationId: Guid.NewGuid(),
            UserId: userId ?? Guid.NewGuid(),
            VehicleId: vehicleId ?? Guid.NewGuid(),
            PlateNumber: plate,
            LotId: Guid.NewGuid(),
            VehicleGroup: group,
            ReservationType: resType,
            AppointmentTime: Now.AddMinutes(20),
            DurationMinutes: resType == ReservationType.Scheduled ? 60 : null,
            PrepaidAmount: 0m);
    }

    // =========================================================================
    // T174-01 & T174-01b: Lỗi ownership mismatch và un-registered ở tầng Application
    // =========================================================================
    [Fact]
    public async Task T174_01_Plate_belonging_to_another_user_fails_ownership_without_calling_provider()
    {
        _ownershipClient.ResultToReturn = Result<VehicleOwnershipInfo>.Failure(VehicleOwnershipErrors.VehicleOwnershipMismatch);
        var handler = CreateHandler();
        var command = CreateCommand();

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(VehicleOwnershipErrors.VehicleOwnershipMismatch, result.Error);
        Assert.Equal(0, _capacityProvider.CallCount);
        Assert.Equal(0, _coordinator.CallCount);
    }

    [Fact]
    public async Task T174_01b_Unregistered_plate_fails_ownership_without_calling_provider()
    {
        _ownershipClient.ResultToReturn = Result<VehicleOwnershipInfo>.Failure(VehicleOwnershipErrors.VehicleNotRegistered);
        var handler = CreateHandler();
        var command = CreateCommand();

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(VehicleOwnershipErrors.VehicleNotRegistered, result.Error);
        Assert.Equal(0, _capacityProvider.CallCount);
        Assert.Equal(0, _coordinator.CallCount);
    }

    // =========================================================================
    // T174-02: Xe máy của chính mình -> trả lỗi UnsupportedVehicleType
    // =========================================================================
    [Fact]
    public async Task T174_02_Own_motorbike_returns_unsupported_vehicle_type_without_calling_provider()
    {
        _ownershipClient.ResultToReturn = Result<VehicleOwnershipInfo>.Failure(VehicleOwnershipErrors.UnsupportedVehicleType);
        var handler = CreateHandler();
        var command = CreateCommand(group: VehicleGroup.Car); // client claims Car

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(VehicleOwnershipErrors.UnsupportedVehicleType, result.Error);
        Assert.Equal(0, _capacityProvider.CallCount);
        Assert.Equal(0, _coordinator.CallCount);
    }

    // =========================================================================
    // T174-03: Biển sai định dạng hoặc rỗng -> trả lỗi tương ứng
    // =========================================================================
    [Theory]
    [InlineData("booking.vehicle.invalid_plate_format")]
    [InlineData("booking.vehicle.empty_plate")]
    public async Task T174_03_Invalid_or_empty_plate_returns_validation_error_without_calling_provider(string errorCode)
    {
        var error = errorCode == "booking.vehicle.empty_plate"
            ? VehicleOwnershipErrors.EmptyPlate
            : VehicleOwnershipErrors.InvalidPlateFormat;

        _ownershipClient.ResultToReturn = Result<VehicleOwnershipInfo>.Failure(error);
        var handler = CreateHandler();
        var command = CreateCommand(plate: "INVALID-PLATE");

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(error, result.Error);
        Assert.Equal(0, _capacityProvider.CallCount);
        Assert.Equal(0, _coordinator.CallCount);
    }

    // =========================================================================
    // T174-04: Lỗi Identity (timeout, unavailable, auth-failed) -> ghi log tương ứng
    // =========================================================================
    [Fact]
    public async Task T174_04_Infrastructure_failures_log_error_and_return_error()
    {
        _ownershipClient.ResultToReturn = Result<VehicleOwnershipInfo>.Failure(VehicleOwnershipErrors.ServiceAuthFailed);
        var handler = CreateHandler();
        var command = CreateCommand();

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(VehicleOwnershipErrors.ServiceAuthFailed, result.Error);
        Assert.Equal(0, _capacityProvider.CallCount);
        Assert.Contains(_logger.Logs, l => l.Level == LogLevel.Error);
    }

    [Fact]
    public async Task T174_04_Transient_failures_log_warning_and_return_error()
    {
        _ownershipClient.ResultToReturn = Result<VehicleOwnershipInfo>.Failure(VehicleOwnershipErrors.DeadlineExceeded);
        var handler = CreateHandler();
        var command = CreateCommand();

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(VehicleOwnershipErrors.DeadlineExceeded, result.Error);
        Assert.Equal(0, _capacityProvider.CallCount);
        Assert.Contains(_logger.Logs, l => l.Level == LogLevel.Warning);
    }

    // =========================================================================
    // T174-05: Thành công -> lưu canonical plate và vehicleId từ Identity
    // =========================================================================
    [Fact]
    public async Task T174_05_Success_stores_canonical_plate_and_identity_vehicle_id()
    {
        var userId = Guid.NewGuid();
        var identityVehicleId = Guid.NewGuid();
        var clientVehicleId = Guid.NewGuid();

        _ownershipClient.ResultToReturn = Result<VehicleOwnershipInfo>.Success(
            new VehicleOwnershipInfo(userId, identityVehicleId, "51A12345", SupportedVehicleType.Car, true));

        var handler = CreateHandler();
        var command = CreateCommand(userId: userId, plate: "51a-123.45", vehicleId: clientVehicleId);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        var res = result.Value!;
        Assert.Equal("51A12345", res.PlateNumber);
        Assert.Equal(identityVehicleId, res.VehicleId);
        Assert.NotEqual(clientVehicleId, res.VehicleId);
        Assert.Equal(VehicleGroup.Car, res.VehicleGroup);
        Assert.Equal(1, _capacityProvider.CallCount);
        Assert.Equal(1, _coordinator.CallCount);
    }

    // =========================================================================
    // T174-05b: AccountNotEligible -> trả AccountNotEligible
    // =========================================================================
    [Fact]
    public async Task T174_05b_AccountNotEligible_returns_failure()
    {
        _ownershipClient.ResultToReturn = Result<VehicleOwnershipInfo>.Failure(VehicleOwnershipErrors.AccountNotEligible);
        var handler = CreateHandler();
        var command = CreateCommand();

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(VehicleOwnershipErrors.AccountNotEligible, result.Error);
        Assert.Equal(0, _capacityProvider.CallCount);
    }

    // =========================================================================
    // T174-O1: Thứ tự gọi - Ownership được gọi TRƯỚC provider
    // =========================================================================
    [Fact]
    public async Task T174_O1_Ownership_is_called_before_capacity_provider()
    {
        var callSequence = new List<string>();
        _ownershipClient.OnCall = () => callSequence.Add("OwnershipClient");
        _capacityProvider.OnCall = () => callSequence.Add("CapacityProvider");

        var handler = CreateHandler();
        var command = CreateCommand();

        await handler.Handle(command, CancellationToken.None);

        Assert.Equal(2, callSequence.Count);
        Assert.Equal("OwnershipClient", callSequence[0]);
        Assert.Equal("CapacityProvider", callSequence[1]);
    }

    // =========================================================================
    // T174-O2: Cả HoldWhileDriving và Scheduled đều bị kiểm tra ownership
    // =========================================================================
    [Theory]
    [InlineData(ReservationType.HoldWhileDriving)]
    [InlineData(ReservationType.Scheduled)]
    public async Task T174_O2_Both_reservation_types_execute_ownership_verification(ReservationType type)
    {
        var handler = CreateHandler();
        var command = CreateCommand(resType: type);

        await handler.Handle(command, CancellationToken.None);

        Assert.Equal(1, _ownershipClient.CallCount);
    }

    // =========================================================================
    // T174-O3: UserId truyền cho VerifyOwnershipAsync đúng bằng UserId của command
    // =========================================================================
    [Fact]
    public async Task T174_O3_UserId_passed_to_ownership_client_matches_command_user_id()
    {
        var expectedUserId = Guid.NewGuid();
        Guid? capturedUserId = null;

        _ownershipClient.OnVerify = (plate, uid, ct) =>
        {
            capturedUserId = uid;
            return Task.FromResult(Result<VehicleOwnershipInfo>.Success(
                new VehicleOwnershipInfo(uid, Guid.NewGuid(), "51A12345", SupportedVehicleType.Car, true)));
        };

        var handler = CreateHandler();
        var command = CreateCommand(userId: expectedUserId);

        await handler.Handle(command, CancellationToken.None);

        Assert.Equal(expectedUserId, capturedUserId);
    }

    // =========================================================================
    // T174-O4: Caller hủy -> OperationCanceledException đi lên, không log Error
    // =========================================================================
    [Fact]
    public async Task T174_O4_Caller_cancellation_bubbles_up_without_logging_error()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        _ownershipClient.OnVerify = (plate, uid, ct) =>
        {
            ct.ThrowIfCancellationRequested();
            return Task.FromResult(Result<VehicleOwnershipInfo>.Failure(VehicleOwnershipErrors.RequestCancelled));
        };

        var handler = CreateHandler();
        var command = CreateCommand();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => handler.Handle(command, cts.Token));
        Assert.DoesNotContain(_logger.Logs, l => l.Level == LogLevel.Error);
    }

    // =========================================================================
    // T174-O5: Client gửi VehicleId lạ và VehicleGroup=Motorbike, Identity trả Car -> kết quả theo Identity
    // =========================================================================
    [Fact]
    public async Task T174_O5_Client_supplied_vehicle_id_and_group_are_overridden_by_identity()
    {
        var userId = Guid.NewGuid();
        var identityVehicleId = Guid.NewGuid();
        var clientVehicleId = Guid.NewGuid();

        _ownershipClient.ResultToReturn = Result<VehicleOwnershipInfo>.Success(
            new VehicleOwnershipInfo(userId, identityVehicleId, "51A12345", SupportedVehicleType.Car, true));

        var handler = CreateHandler();
        var command = CreateCommand(
            userId: userId,
            vehicleId: clientVehicleId,
            group: VehicleGroup.Motorbike); // client sent Motorbike

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        var res = result.Value!;
        Assert.Equal(VehicleGroup.Car, res.VehicleGroup);
        Assert.Equal(identityVehicleId, res.VehicleId);
    }

    // Test Doubles
    private sealed class FakeVehicleOwnershipClient : IVehicleOwnershipClient
    {
        public Action? OnCall { get; set; }
        public int CallCount { get; private set; }
        public Result<VehicleOwnershipInfo>? ResultToReturn { get; set; }
        public Func<string, Guid, CancellationToken, Task<Result<VehicleOwnershipInfo>>>? OnVerify { get; set; }

        public Task<Result<VehicleOwnershipInfo>> VerifyOwnershipAsync(
            string rawOrCanonicalPlate,
            Guid expectedUserId,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            OnCall?.Invoke();

            if (OnVerify is not null)
            {
                return OnVerify(rawOrCanonicalPlate, expectedUserId, cancellationToken);
            }

            if (ResultToReturn is not null)
            {
                return Task.FromResult(ResultToReturn);
            }

            return Task.FromResult(Result<VehicleOwnershipInfo>.Success(
                new VehicleOwnershipInfo(expectedUserId, Guid.NewGuid(), "51A12345", SupportedVehicleType.Car, true)));
        }
    }

    private sealed class FakeParkingLotCapacityProvider : IParkingLotCapacityProvider
    {
        public Action? OnCall { get; set; }
        public int CallCount { get; private set; }
        public Result<ParkingLotCapacityInfo> ResultToReturn { get; set; } = Result<ParkingLotCapacityInfo>.Success(new ParkingLotCapacityInfo(20, 50, 10, true, 0m));

        public Task<Result<ParkingLotCapacityInfo>> GetCapacityInfoAsync(Guid lotId, CancellationToken cancellationToken = default)
        {
            CallCount++;
            OnCall?.Invoke();
            return Task.FromResult(ResultToReturn);
        }
    }

    private sealed class FakeReservationCapacityCoordinator : IReservationCapacityCoordinator
    {
        public Action? OnCall { get; set; }
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
            OnCall?.Invoke();
            return Task.FromResult(ResultToReturn(reservation));
        }
    }

    private sealed class FakeClock(DateTimeOffset utcNow) : IClock
    {
        public DateTimeOffset UtcNow => utcNow;
    }

    private sealed class TestLogger<T> : ILogger<T>
    {
        public List<(LogLevel Level, string Message)> Logs { get; } = new();

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Logs.Add((logLevel, formatter(state, exception)));
        }
    }
}
