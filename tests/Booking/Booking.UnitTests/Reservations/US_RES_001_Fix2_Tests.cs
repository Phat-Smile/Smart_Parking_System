using System.Reflection;
using Microsoft.Extensions.Logging;
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

public sealed class US_RES_001_Fix2_Tests
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

    public US_RES_001_Fix2_Tests()
    {
        _capacityProvider.ResultToReturn = Result<ParkingLotCapacityInfo>.Success(
            new ParkingLotCapacityInfo(20, 50, 10, ReservationEnabled: true, DepositAmount: 0m));
        _coordinator.ResultToReturn = r => Result<Reservation>.Success(r);
    }

    private CreateTemporaryHoldCommandHandler CreateHandler() =>
        new(_ownershipClient, _capacityProvider, _coordinator, _clock, _logger, new UnconfiguredParkingPricingProvider(), _options);

    private static CreateTemporaryHoldCommand CreateCommand(
        Guid? userId = null,
        string plate = "51A-123.45",
        ReservationType resType = ReservationType.HoldWhileDriving,
        VehicleGroup group = VehicleGroup.Car,
        decimal prepaidAmount = 0m,
        int? durationMinutes = null)
    {
        return new CreateTemporaryHoldCommand(
            ReservationId: Guid.NewGuid(),
            UserId: userId ?? Guid.NewGuid(),
            VehicleId: Guid.NewGuid(),
            PlateNumber: plate,
            LotId: Guid.NewGuid(),
            VehicleGroup: group,
            ReservationType: resType,
            AppointmentTime: Now.AddMinutes(20),
            DurationMinutes: resType == ReservationType.Scheduled ? (durationMinutes ?? 60) : null,
            PrepaidAmount: prepaidAmount);
    }

    // =========================================================================
    // FIX2-01: Xe máy của người khác -> lỗi ownership gộp, không phải UnsupportedVehicleType
    // =========================================================================
    [Fact]
    [Trait("Requirement", "FIX2-01")]
    public async Task FIX2_01_Other_users_motorbike_fails_ownership_and_returns_ownership_error()
    {
        // Identity returns ownership mismatch when plate belongs to someone else (even if vehicle is motorbike)
        _ownershipClient.ResultToReturn = Result<VehicleOwnershipInfo>.Failure(VehicleOwnershipErrors.VehicleOwnershipMismatch);

        var handler = CreateHandler();
        var command = CreateCommand(plate: "59A1-123.45");

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(VehicleOwnershipErrors.VehicleOwnershipMismatch.Code, result.Error!.Code);
        Assert.NotEqual(VehicleOwnershipErrors.UnsupportedVehicleType.Code, result.Error!.Code);
        Assert.Equal(0, _capacityProvider.CallCount);
    }

    // =========================================================================
    // FIX2-02: AccountNotEligible -> 403 mã riêng; không gọi provider
    // =========================================================================
    [Fact]
    [Trait("Requirement", "FIX2-02")]
    public async Task FIX2_02_AccountNotEligible_returns_account_not_eligible_error_and_skips_provider()
    {
        _ownershipClient.ResultToReturn = Result<VehicleOwnershipInfo>.Failure(VehicleOwnershipErrors.AccountNotEligible);

        var handler = CreateHandler();
        var command = CreateCommand();

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(VehicleOwnershipErrors.AccountNotEligible.Code, result.Error!.Code);
        Assert.Equal(0, _capacityProvider.CallCount);
        Assert.Equal(0, _coordinator.CallCount);

        // Verification log is Information level
        Assert.Contains(_logger.Logs, l => l.Level == LogLevel.Information && l.Message.Contains(VehicleOwnershipErrors.AccountNotEligible.Code));
    }

    // =========================================================================
    // FIX2-03: EmptyPlate, InvalidPlateFormat -> 400; không gọi provider
    // =========================================================================
    [Theory]
    [InlineData("booking.vehicle.empty_plate")]
    [InlineData("booking.vehicle.invalid_plate_format")]
    [Trait("Requirement", "FIX2-03")]
    public async Task FIX2_03_Empty_or_invalid_plate_returns_format_error_without_provider_call(string errorCode)
    {
        var targetError = errorCode == "booking.vehicle.empty_plate"
            ? VehicleOwnershipErrors.EmptyPlate
            : VehicleOwnershipErrors.InvalidPlateFormat;

        _ownershipClient.ResultToReturn = Result<VehicleOwnershipInfo>.Failure(targetError);

        var handler = CreateHandler();
        var command = CreateCommand();

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(targetError.Code, result.Error!.Code);
        Assert.Equal(0, _capacityProvider.CallCount);

        // Per Table 4.3, EmptyPlate and InvalidPlateFormat do NOT log
        Assert.DoesNotContain(_logger.Logs, l => l.Message.Contains(targetError.Code));
    }

    // =========================================================================
    // FIX2-04: Lỗi hạ tầng Identity -> 503 thông điệp chung; mức log đúng
    // =========================================================================
    [Theory]
    [InlineData("booking.vehicle.service_auth_failed", LogLevel.Error)]
    [InlineData("booking.vehicle.communication_failed", LogLevel.Error)]
    [InlineData("booking.vehicle.service_unavailable", LogLevel.Warning)]
    [InlineData("booking.vehicle.deadline_exceeded", LogLevel.Warning)]
    [InlineData("booking.vehicle.cancelled", LogLevel.Warning)]
    [Trait("Requirement", "FIX2-04")]
    public async Task FIX2_04_Infrastructure_errors_log_at_expected_levels(string errorCode, LogLevel expectedLogLevel)
    {
        var error = typeof(VehicleOwnershipErrors)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.FieldType == typeof(Error))
            .Select(f => (Error)f.GetValue(null)!)
            .First(e => e.Code == errorCode);

        _ownershipClient.ResultToReturn = Result<VehicleOwnershipInfo>.Failure(error);

        var handler = CreateHandler();
        var command = CreateCommand();

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(errorCode, result.Error!.Code);

        // Assert log level
        Assert.Contains(_logger.Logs, l => l.Level == expectedLogLevel && l.Message.Contains(errorCode));
    }

    // =========================================================================
    // FIX2-05: Ownership được gọi trước provider (spy thứ tự)
    // =========================================================================
    [Fact]
    [Trait("Requirement", "FIX2-05")]
    public async Task FIX2_05_Vehicle_ownership_is_verified_strictly_before_capacity_provider()
    {
        var callSequence = new List<string>();

        _ownershipClient.OnCall = () => callSequence.Add("VerifyOwnership");
        _capacityProvider.OnCall = () => callSequence.Add("GetCapacityInfo");
        _coordinator.OnCall = () => callSequence.Add("CreateHoldAtomic");

        var handler = CreateHandler();
        var command = CreateCommand();

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(3, callSequence.Count);
        Assert.Equal("VerifyOwnership", callSequence[0]);
        Assert.Equal("GetCapacityInfo", callSequence[1]);
        Assert.Equal("CreateHoldAtomic", callSequence[2]);
    }

    // =========================================================================
    // FIX2-06: Cả HoldWhileDriving và Scheduled đều bị kiểm tra ownership
    // =========================================================================
    [Theory]
    [InlineData(ReservationType.HoldWhileDriving)]
    [InlineData(ReservationType.Scheduled)]
    [Trait("Requirement", "FIX2-06")]
    public async Task FIX2_06_Both_HoldWhileDriving_and_Scheduled_enforce_ownership_check(ReservationType type)
    {
        _ownershipClient.ResultToReturn = Result<VehicleOwnershipInfo>.Failure(VehicleOwnershipErrors.VehicleOwnershipMismatch);

        var handler = CreateHandler();
        var command = CreateCommand(resType: type);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(VehicleOwnershipErrors.VehicleOwnershipMismatch.Code, result.Error!.Code);
        Assert.Equal(1, _ownershipClient.CallCount);
        Assert.Equal(0, _capacityProvider.CallCount);
    }

    // =========================================================================
    // FIX2-07: UserId truyền cho VerifyOwnershipAsync đúng bằng UserId của command
    // =========================================================================
    [Fact]
    [Trait("Requirement", "FIX2-07")]
    public async Task FIX2_07_UserId_passed_to_VerifyOwnership_matches_command_UserId_exactly()
    {
        var expectedUserId = Guid.NewGuid();
        Guid? capturedUserId = null;

        _ownershipClient.OnVerify = (plate, userId, ct) =>
        {
            capturedUserId = userId;
            return Task.FromResult(Result<VehicleOwnershipInfo>.Success(
                new VehicleOwnershipInfo(userId, Guid.NewGuid(), "51A12345", SupportedVehicleType.Car, true)));
        };

        var handler = CreateHandler();
        var command = CreateCommand(userId: expectedUserId);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(expectedUserId, capturedUserId);
    }

    // =========================================================================
    // FIX2-08: Caller hủy -> OperationCanceledException đi lên; không log Error; không lưu
    // =========================================================================
    [Fact]
    [Trait("Requirement", "FIX2-08")]
    public async Task FIX2_08_Caller_cancellation_throws_OperationCanceledException_without_error_log()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var handler = CreateHandler();
        var command = CreateCommand();

        await Assert.ThrowsAsync<OperationCanceledException>(() => handler.Handle(command, cts.Token));

        Assert.Equal(0, _capacityProvider.CallCount);
        Assert.Equal(0, _coordinator.CallCount);
        Assert.DoesNotContain(_logger.Logs, l => l.Level == LogLevel.Error);
    }

    // =========================================================================
    // FIX2-10: Scheduled: PrepaidAmount vẫn theo client (đặc tả hành vi hiện tại)
    // =========================================================================
    [Fact]
    [Trait("Requirement", "FIX2-10")]
    public async Task FIX2_10_Scheduled_reservation_uses_authoritative_pricing_from_provider()
    {
        _capacityProvider.ResultToReturn = Result<ParkingLotCapacityInfo>.Success(
            new ParkingLotCapacityInfo(20, 50, 10, ReservationEnabled: true, DepositAmount: 30000m));

        Reservation? createdReservation = null;
        _coordinator.ResultToReturn = r =>
        {
            createdReservation = r;
            return Result<Reservation>.Success(r);
        };

        var fakePricing = new FakeParkingPricingProvider
        {
            ResultToReturn = Result<ParkingPricingInfo>.Success(
                new ParkingPricingInfo(Guid.NewGuid(), VehicleGroup.Car, 20000m, "PerEntry", null, 0m))
        };

        var handler = new CreateTemporaryHoldCommandHandler(
            _ownershipClient, _capacityProvider, _coordinator, _clock, _logger, fakePricing, _options);

        const decimal clientPrepaid = 15000m;
        var command = CreateCommand(resType: ReservationType.Scheduled, prepaidAmount: clientPrepaid, durationMinutes: 60);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(createdReservation);
        // BR-10.a: Scheduled PrepaidAmount comes from pricing provider (20,000 VND), client-supplied amount (15,000 VND) is ignored
        Assert.Equal(20000m, createdReservation.PrepaidAmount);
        Assert.NotEqual(clientPrepaid, createdReservation.PrepaidAmount);
    }

    // =========================================================================
    // FIX2-11: Đặt chỗ cọc > 0 mà: không tìm thấy / không phải chủ / sai trạng thái / hết hạn
    //          -> trả đúng lỗi cũ, KHÔNG phải lỗi payment
    // =========================================================================
    [Theory]
    [InlineData("NotFound")]
    [InlineData("NotOwner")]
    [InlineData("InvalidStatus")]
    [InlineData("Expired")]
    [Trait("Requirement", "FIX2-11")]
    public async Task FIX2_11_Confirm_with_deposit_fails_with_standard_errors_before_payment_gate(string scenario)
    {
        var ownerId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var resId = Guid.NewGuid();

        var repo = new FakeReservationRepository();
        var testLogger = new TestLogger<ConfirmReservationCommandHandler>();

        // Build reservation with PrepaidAmount > 0 (deposit required)
        var reservation = Reservation.CreateTemporaryHold(
            resId,
            ownerId,
            Guid.NewGuid(),
            "51A12345",
            Guid.NewGuid(),
            VehicleGroup.Car,
            ReservationType.HoldWhileDriving,
            Now.AddMinutes(20),
            prepaidAmount: 20000m,
            createdAt: Now.AddMinutes(-5),
            durationMinutes: null,
            tempHoldExpiresAt: scenario == "Expired" ? Now.AddMinutes(-1) : Now.AddMinutes(5));

        if (scenario == "InvalidStatus")
        {
            reservation.Cancel(Now, CancelReason.CustomerCancelled);
        }

        if (scenario != "NotFound")
        {
            repo.Add(reservation);
        }

        var confirmHandler = new ConfirmReservationCommandHandler(repo, _clock, testLogger);

        var commandUserId = scenario == "NotOwner" ? otherUserId : ownerId;
        var command = new ConfirmReservationCommand(resId, commandUserId, PaymentMethod: "Wallet", DepositAmount: 20000m);

        var result = await confirmHandler.Handle(command, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.NotEqual(ReservationErrors.PaymentNotAvailable.Code, result.Error!.Code);

        switch (scenario)
        {
            case "NotFound":
                Assert.Equal(ReservationErrors.NotFound.Code, result.Error.Code);
                break;
            case "NotOwner":
                Assert.Equal(ReservationErrors.AccessDenied.Code, result.Error.Code);
                break;
            case "InvalidStatus":
                Assert.Equal(ReservationErrors.InvalidStatus.Code, result.Error.Code);
                break;
            case "Expired":
                Assert.Equal(ReservationErrors.HoldExpired.Code, result.Error.Code);
                break;
        }
    }

    // =========================================================================
    // FIX2-12: Reflection: ParkingLotCapacityInfo không có mặc định ở hai trường mới (B1)
    // =========================================================================
    [Fact]
    [Trait("Requirement", "FIX2-12")]
    public void FIX2_12_ParkingLotCapacityInfo_constructor_has_no_default_values_for_new_fields()
    {
        var type = typeof(ParkingLotCapacityInfo);
        var constructor = type.GetConstructors().First();
        var parameters = constructor.GetParameters();

        var reservationEnabledParam = parameters.First(p => p.Name == "ReservationEnabled");
        var depositAmountParam = parameters.First(p => p.Name == "DepositAmount");

        Assert.False(reservationEnabledParam.HasDefaultValue, "ReservationEnabled must not have a default value");
        Assert.False(depositAmountParam.HasDefaultValue, "DepositAmount must not have a default value");
    }

    // =========================================================================
    // FIX2-13: Cọc âm hoặc có phần thập phân -> CapacityInfoUnavailable, không lưu
    // =========================================================================
    [Theory]
    [InlineData(-1000)]
    [InlineData(20000.5)]
    [Trait("Requirement", "FIX2-13")]
    public async Task FIX2_13_Abnormal_deposit_amount_returns_CapacityInfoUnavailable_and_never_saves(decimal abnormalDeposit)
    {
        _capacityProvider.ResultToReturn = Result<ParkingLotCapacityInfo>.Success(
            new ParkingLotCapacityInfo(20, 50, 10, ReservationEnabled: true, DepositAmount: abnormalDeposit));

        var handler = CreateHandler();
        var command = CreateCommand();

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ReservationCapacityErrors.CapacityInfoUnavailable.Code, result.Error!.Code);
        Assert.Equal(0, _coordinator.CallCount);

        // Error log written
        Assert.Contains(_logger.Logs, l => l.Level == LogLevel.Error && l.Message.Contains("DepositAmount"));
    }

    // =========================================================================
    // FIX2-14: Log cổng xác nhận có đủ trường ở B5; không chứa biển số/token/service key
    // =========================================================================
    [Fact]
    [Trait("Requirement", "FIX2-14")]
    public async Task FIX2_14_Payment_gate_log_contains_all_required_fields_and_no_sensitive_data()
    {
        var ownerId = Guid.NewGuid();
        var lotId = Guid.NewGuid();
        var resId = Guid.NewGuid();

        var repo = new FakeReservationRepository();
        var testLogger = new TestLogger<ConfirmReservationCommandHandler>();

        var reservation = Reservation.CreateTemporaryHold(
            resId,
            ownerId,
            Guid.NewGuid(),
            "51A12345",
            lotId,
            VehicleGroup.Car,
            ReservationType.HoldWhileDriving,
            Now.AddMinutes(20),
            prepaidAmount: 25000m,
            createdAt: Now.AddMinutes(-5),
            durationMinutes: null,
            tempHoldExpiresAt: Now.AddMinutes(5));

        repo.Add(reservation);

        var confirmHandler = new ConfirmReservationCommandHandler(repo, _clock, testLogger);
        var command = new ConfirmReservationCommand(resId, ownerId, PaymentMethod: "Wallet", DepositAmount: 25000m);

        var result = await confirmHandler.Handle(command, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ReservationErrors.PaymentNotAvailable.Code, result.Error!.Code);

        var errorLog = Assert.Single(testLogger.Logs, l => l.Level == LogLevel.Error);

        // Verify required fields present in log
        Assert.Contains(resId.ToString(), errorLog.Message);
        Assert.Contains(ownerId.ToString(), errorLog.Message);
        Assert.Contains(lotId.ToString(), errorLog.Message);
        Assert.Contains("HoldWhileDriving", errorLog.Message);
        Assert.Contains("TemporaryHold", errorLog.Message);
        Assert.Contains("25000", errorLog.Message);
        Assert.Contains("PaymentRequired", errorLog.Message);

        // Verify no sensitive plate/token data leaked
        Assert.DoesNotContain("51A12345", errorLog.Message);
        Assert.DoesNotContain("token", errorLog.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret", errorLog.Message, StringComparison.OrdinalIgnoreCase);
    }

    // ==========================================
    // Test Doubles / Fakes
    // ==========================================
    private sealed class FakeVehicleOwnershipClient : IVehicleOwnershipClient
    {
        public int CallCount { get; private set; }
        public Action? OnCall { get; set; }
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
        public Result<ParkingLotCapacityInfo> ResultToReturn { get; set; } = Result<ParkingLotCapacityInfo>.Success(
            new ParkingLotCapacityInfo(20, 50, 10, ReservationEnabled: true, DepositAmount: 0m));

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

    private sealed class FakeParkingPricingProvider : IParkingPricingProvider
    {
        public Result<ParkingPricingInfo> ResultToReturn { get; set; } =
            Result<ParkingPricingInfo>.Success(new ParkingPricingInfo(Guid.NewGuid(), VehicleGroup.Car, 20000m, "PerEntry", null, 0m));

        public Task<Result<ParkingPricingInfo>> GetPricingInfoAsync(Guid lotId, VehicleGroup vehicleGroup, CancellationToken cancellationToken = default) =>
            Task.FromResult(ResultToReturn);
    }

    private sealed class FakeClock(DateTimeOffset utcNow) : IClock
    {
        public DateTimeOffset UtcNow => utcNow;
    }

    private sealed class FakeReservationRepository : IReservationRepository
    {
        private readonly List<Reservation> _list = new();

        public void Add(Reservation reservation) => _list.Add(reservation);

        public Task<Reservation?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(_list.FirstOrDefault(r => r.Id == id));

        public Task<IReadOnlyList<Reservation>> ListByUserIdAsync(Guid userId, ReservationStatus? status, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Reservation>>(_list.Where(r => r.UserId == userId && (!status.HasValue || r.Status == status.Value)).ToList());

        public Task<IReadOnlyList<Reservation>> ListActiveByLotIdAsync(Guid lotId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Reservation>>(_list.Where(r => r.LotId == lotId && (r.Status == ReservationStatus.TemporaryHold || r.Status == ReservationStatus.Confirmed)).ToList());

        public Task<Reservation?> FindActiveConfirmedByLotAndPlateAsync(Guid lotId, string plateNumber, DateTimeOffset arrivalTime, CancellationToken cancellationToken = default) =>
            Task.FromResult(_list.FirstOrDefault(r => r.LotId == lotId && r.PlateNumber == plateNumber && r.Status == ReservationStatus.Confirmed));

        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
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
