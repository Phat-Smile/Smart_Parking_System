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

public sealed class ConfirmReservationPaymentGateTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 4, 12, 0, 0, TimeSpan.Zero);

    private readonly FakeReservationRepository _repo = new();
    private readonly FakeClock _clock = new(Now);
    private readonly TestLogger<ConfirmReservationCommandHandler> _logger = new();

    private ConfirmReservationCommandHandler CreateHandler() =>
        new(_repo, _clock, _logger);

    private static Reservation CreateHold(
        Guid reservationId,
        Guid userId,
        ReservationType type = ReservationType.HoldWhileDriving,
        decimal prepaidAmount = 0m,
        DateTimeOffset? expiresAt = null,
        ReservationStatus status = ReservationStatus.TemporaryHold)
    {
        var res = Reservation.CreateTemporaryHold(
            reservationId,
            userId,
            Guid.NewGuid(),
            "51A12345",
            Guid.NewGuid(),
            VehicleGroup.Car,
            type,
            Now.AddMinutes(20),
            prepaidAmount,
            Now.AddMinutes(-5),
            durationMinutes: type == ReservationType.Scheduled ? 60 : null,
            tempHoldExpiresAt: expiresAt ?? Now.AddMinutes(5));

        if (status == ReservationStatus.Cancelled)
        {
            res.Cancel(Now, CancelReason.CustomerCancelled);
        }

        return res;
    }

    // =========================================================================
    // T-C1a: HoldWhileDriving, cọc > 0 -> bị chặn payment_not_available
    // =========================================================================
    [Fact]
    public async Task T_C1a_HoldWhileDriving_with_positive_deposit_is_blocked()
    {
        var userId = Guid.NewGuid();
        var reservation = CreateHold(Guid.NewGuid(), userId, ReservationType.HoldWhileDriving, prepaidAmount: 20000m);
        _repo.Add(reservation);

        var handler = CreateHandler();
        var command = new ConfirmReservationCommand(reservation.Id, userId, "Wallet", 20000m);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ReservationErrors.PaymentNotAvailable, result.Error);
        Assert.Equal(ReservationStatus.TemporaryHold, reservation.Status);
        Assert.Equal(0, _repo.SaveChangesCount);
    }

    // =========================================================================
    // T-C1b: Scheduled (cả cọc 0đ lẫn > 0đ) -> bị chặn payment_not_available
    // =========================================================================
    [Theory]
    [InlineData(0)]
    [InlineData(50000)]
    public async Task T_C1b_Scheduled_reservations_are_always_blocked(decimal prepaidAmount)
    {
        var userId = Guid.NewGuid();
        var reservation = CreateHold(Guid.NewGuid(), userId, ReservationType.Scheduled, prepaidAmount: prepaidAmount);
        _repo.Add(reservation);

        var handler = CreateHandler();
        var command = new ConfirmReservationCommand(reservation.Id, userId, "Wallet", prepaidAmount);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ReservationErrors.PaymentNotAvailable, result.Error);
        Assert.Equal(ReservationStatus.TemporaryHold, reservation.Status);
        Assert.Equal(0, _repo.SaveChangesCount);
    }

    // =========================================================================
    // T-C1c: HoldWhileDriving, cọc 0đ -> Đã xác nhận thành công
    // =========================================================================
    [Fact]
    public async Task T_C1c_HoldWhileDriving_with_zero_deposit_confirms_successfully()
    {
        var userId = Guid.NewGuid();
        var reservation = CreateHold(Guid.NewGuid(), userId, ReservationType.HoldWhileDriving, prepaidAmount: 0m);
        _repo.Add(reservation);

        var handler = CreateHandler();
        var command = new ConfirmReservationCommand(reservation.Id, userId, null, 0m);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(ReservationStatus.Confirmed.ToString(), result.Value!.Status);
        Assert.StartsWith("RES-QR-", result.Value.QrCodeToken);
        Assert.Equal(ReservationStatus.Confirmed, reservation.Status);
        Assert.Equal(1, _repo.SaveChangesCount);
    }

    // =========================================================================
    // T-C1d: Đặt chỗ hết hạn -> lỗi hold_expired, không phải payment
    // =========================================================================
    [Fact]
    public async Task T_C1d_Expired_hold_returns_hold_expired_before_payment_gate()
    {
        var userId = Guid.NewGuid();
        // Expired 1 minute ago
        var reservation = CreateHold(Guid.NewGuid(), userId, ReservationType.HoldWhileDriving, prepaidAmount: 20000m, expiresAt: Now.AddMinutes(-1));
        _repo.Add(reservation);

        var handler = CreateHandler();
        var command = new ConfirmReservationCommand(reservation.Id, userId, "Wallet", 20000m);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ReservationErrors.HoldExpired, result.Error);
        Assert.NotEqual(ReservationErrors.PaymentNotAvailable, result.Error);
    }

    // =========================================================================
    // T-C1e: Đúng MỘT log Error có cấu trúc, đầy đủ trường, không lộ secret/biển số
    // =========================================================================
    [Fact]
    public async Task T_C1e_Logs_structured_error_with_required_fields_and_no_pii()
    {
        var userId = Guid.NewGuid();
        var reservation = CreateHold(Guid.NewGuid(), userId, ReservationType.HoldWhileDriving, prepaidAmount: 30000m);
        _repo.Add(reservation);

        var handler = CreateHandler();
        var command = new ConfirmReservationCommand(reservation.Id, userId, "Wallet", 30000m);

        await handler.Handle(command, CancellationToken.None);

        var errorLogs = _logger.Logs.Where(l => l.Level == LogLevel.Error).ToList();
        Assert.Single(errorLogs);

        var logMessage = errorLogs[0].Message;
        Assert.Contains(reservation.Id.ToString(), logMessage);
        Assert.Contains(userId.ToString(), logMessage);
        Assert.Contains(reservation.LotId.ToString(), logMessage);
        Assert.Contains("30000", logMessage);
        Assert.Contains("PaymentRequired", logMessage);

        // Assert no license plate or secrets leaked
        Assert.DoesNotContain("51A12345", logMessage);
        Assert.DoesNotContain("Bearer", logMessage, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ServiceKey", logMessage, StringComparison.OrdinalIgnoreCase);
    }

    // =========================================================================
    // T-C1f: Payment_not_available error code & message
    // =========================================================================
    [Fact]
    public void T_C1f_Payment_not_available_error_definition()
    {
        Assert.Equal("booking.reservation.payment_not_available", ReservationErrors.PaymentNotAvailable.Code);
        Assert.False(string.IsNullOrWhiteSpace(ReservationErrors.PaymentNotAvailable.Message));
    }

    // =========================================================================
    // T-C1g: Không tìm thấy, sai chủ, sai trạng thái -> thứ tự cũ bảo toàn
    // =========================================================================
    [Fact]
    public async Task T_C1g_Pre_existing_validation_checks_remain_intact()
    {
        var handler = CreateHandler();

        // 1. Not found
        var notFoundResult = await handler.Handle(new ConfirmReservationCommand(Guid.NewGuid(), Guid.NewGuid(), null, 0), CancellationToken.None);
        Assert.Equal(ReservationErrors.NotFound, notFoundResult.Error);

        // 2. Access denied (wrong user)
        var ownerId = Guid.NewGuid();
        var res = CreateHold(Guid.NewGuid(), ownerId, ReservationType.HoldWhileDriving, 0m);
        _repo.Add(res);
        var accessDeniedResult = await handler.Handle(new ConfirmReservationCommand(res.Id, Guid.NewGuid(), null, 0), CancellationToken.None);
        Assert.Equal(ReservationErrors.AccessDenied, accessDeniedResult.Error);

        // 3. Invalid status (cancelled)
        var cancelledRes = CreateHold(Guid.NewGuid(), ownerId, ReservationType.HoldWhileDriving, 0m, status: ReservationStatus.Cancelled);
        _repo.Add(cancelledRes);
        var invalidStatusResult = await handler.Handle(new ConfirmReservationCommand(cancelledRes.Id, ownerId, null, 0), CancellationToken.None);
        Assert.Equal(ReservationErrors.InvalidStatus, invalidStatusResult.Error);
    }

    // =========================================================================
    // T-P1: ReservationEnabled == false -> lot_reservations_disabled (409)
    // =========================================================================
    [Fact]
    public async Task T_P1_Lot_reservations_disabled_rejects_and_does_not_call_coordinator()
    {
        var capacityProvider = new FakeCapacityProvider
        {
            InfoToReturn = new ParkingLotCapacityInfo(10, 50, 5, ReservationEnabled: false, DepositAmount: 20000m)
        };
        var coordinator = new FakeCoordinator();
        var ownership = new FakeOwnership();

        var handler = new CreateTemporaryHoldCommandHandler(ownership, capacityProvider, coordinator, _clock, new TestLogger<CreateTemporaryHoldCommandHandler>());
        var command = new CreateTemporaryHoldCommand(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "51A12345", Guid.NewGuid(), VehicleGroup.Car, ReservationType.HoldWhileDriving, Now.AddMinutes(20), null, 0m);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ReservationCapacityErrors.LotReservationsDisabled, result.Error);
        Assert.Equal(0, coordinator.CallCount);
    }

    // =========================================================================
    // T-P2: HoldWhileDriving -> PrepaidAmount = DepositAmount từ provider
    // =========================================================================
    [Theory]
    [InlineData(0)]
    [InlineData(99999)]
    public async Task T_P2_HoldWhileDriving_overrides_client_prepaid_amount_with_provider_deposit(decimal clientPrepaid)
    {
        const decimal authoritativeDeposit = 30000m;
        var capacityProvider = new FakeCapacityProvider
        {
            InfoToReturn = new ParkingLotCapacityInfo(10, 50, 5, ReservationEnabled: true, DepositAmount: authoritativeDeposit)
        };
        var coordinator = new FakeCoordinator();
        var ownership = new FakeOwnership();

        var handler = new CreateTemporaryHoldCommandHandler(ownership, capacityProvider, coordinator, _clock, new TestLogger<CreateTemporaryHoldCommandHandler>());
        var command = new CreateTemporaryHoldCommand(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "51A12345", Guid.NewGuid(), VehicleGroup.Car, ReservationType.HoldWhileDriving, Now.AddMinutes(20), null, clientPrepaid);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(authoritativeDeposit, coordinator.CapturedReservation!.PrepaidAmount);
    }

    // =========================================================================
    // T-P3: DepositAmount = 0 -> tạo đặt chỗ với 0đ
    // =========================================================================
    [Fact]
    public async Task T_P3_Zero_deposit_lot_creates_reservation_with_zero_prepaid_amount()
    {
        var capacityProvider = new FakeCapacityProvider
        {
            InfoToReturn = new ParkingLotCapacityInfo(10, 50, 5, ReservationEnabled: true, DepositAmount: 0m)
        };
        var coordinator = new FakeCoordinator();
        var ownership = new FakeOwnership();

        var handler = new CreateTemporaryHoldCommandHandler(ownership, capacityProvider, coordinator, _clock, new TestLogger<CreateTemporaryHoldCommandHandler>());
        var command = new CreateTemporaryHoldCommand(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "51A12345", Guid.NewGuid(), VehicleGroup.Car, ReservationType.HoldWhileDriving, Now.AddMinutes(20), null, 50000m);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(0m, coordinator.CapturedReservation!.PrepaidAmount);
    }

    // =========================================================================
    // T-P4: DepositAmount âm hoặc có phần thập phân -> CapacityInfoUnavailable
    // =========================================================================
    [Theory]
    [InlineData(-1000)]
    [InlineData(20000.50)]
    [InlineData(15000.0001)]
    public async Task T_P4_Negative_or_fractional_deposit_fails_closed_with_CapacityInfoUnavailable(decimal invalidDeposit)
    {
        var capacityProvider = new FakeCapacityProvider
        {
            InfoToReturn = new ParkingLotCapacityInfo(10, 50, 5, ReservationEnabled: true, DepositAmount: invalidDeposit)
        };
        var coordinator = new FakeCoordinator();
        var ownership = new FakeOwnership();

        var handler = new CreateTemporaryHoldCommandHandler(ownership, capacityProvider, coordinator, _clock, new TestLogger<CreateTemporaryHoldCommandHandler>());
        var command = new CreateTemporaryHoldCommand(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "51A12345", Guid.NewGuid(), VehicleGroup.Car, ReservationType.HoldWhileDriving, Now.AddMinutes(20), null, 0m);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ReservationCapacityErrors.CapacityInfoUnavailable, result.Error);
        Assert.Equal(0, coordinator.CallCount);
    }

    // =========================================================================
    // T-P5: Scheduled -> PrepaidAmount vẫn theo client gửi
    // =========================================================================
    [Fact]
    public async Task T_P5_Scheduled_preserves_client_prepaid_amount_as_documented_risk()
    {
        const decimal clientAmount = 75000m;
        var capacityProvider = new FakeCapacityProvider
        {
            InfoToReturn = new ParkingLotCapacityInfo(10, 50, 5, ReservationEnabled: true, DepositAmount: 20000m)
        };
        var coordinator = new FakeCoordinator();
        var ownership = new FakeOwnership();

        var handler = new CreateTemporaryHoldCommandHandler(ownership, capacityProvider, coordinator, _clock, new TestLogger<CreateTemporaryHoldCommandHandler>());
        var command = new CreateTemporaryHoldCommand(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "51A12345", Guid.NewGuid(), VehicleGroup.Car, ReservationType.Scheduled, Now.AddHours(2), 60, clientAmount);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(clientAmount, coordinator.CapturedReservation!.PrepaidAmount);
    }

    // Doubles
    private sealed class FakeReservationRepository : IReservationRepository
    {
        private readonly List<Reservation> _list = [];
        public int SaveChangesCount { get; private set; }

        public void Add(Reservation r) => _list.Add(r);

        public Task<Reservation?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(_list.FirstOrDefault(r => r.Id == id));

        public Task<IReadOnlyList<Reservation>> ListByUserIdAsync(Guid userId, ReservationStatus? status, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Reservation>>(_list.Where(r => r.UserId == userId && (!status.HasValue || r.Status == status.Value)).ToList());

        public Task<IReadOnlyList<Reservation>> ListActiveByLotIdAsync(Guid lotId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Reservation>>(_list.Where(r => r.LotId == lotId && (r.Status == ReservationStatus.TemporaryHold || r.Status == ReservationStatus.Confirmed)).ToList());

        public Task<Reservation?> FindActiveConfirmedByLotAndPlateAsync(Guid lotId, string plateNumber, DateTimeOffset arrivalTime, CancellationToken cancellationToken = default) =>
            Task.FromResult(_list.FirstOrDefault(r => r.LotId == lotId && r.PlateNumber == plateNumber && r.Status == ReservationStatus.Confirmed));

        public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            SaveChangesCount++;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeCapacityProvider : IParkingLotCapacityProvider
    {
        public ParkingLotCapacityInfo InfoToReturn { get; set; } = new(10, 50, 5, ReservationEnabled: true, DepositAmount: 0m);

        public Task<Result<ParkingLotCapacityInfo>> GetCapacityInfoAsync(Guid lotId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result<ParkingLotCapacityInfo>.Success(InfoToReturn));
    }

    private sealed class FakeCoordinator : IReservationCapacityCoordinator
    {
        public int CallCount { get; private set; }
        public Reservation? CapturedReservation { get; private set; }

        public Task<Result<Reservation>> CreateTemporaryHoldAtomicAsync(
            Reservation reservation, int ceiling, int? availableSlotCount, TimeSpan lockTimeout, int maxActiveReservationsPerUser = 3, CancellationToken cancellationToken = default)
        {
            CallCount++;
            CapturedReservation = reservation;
            return Task.FromResult(Result<Reservation>.Success(reservation));
        }
    }

    private sealed class FakeOwnership : IVehicleOwnershipClient
    {
        public Task<Result<VehicleOwnershipInfo>> VerifyOwnershipAsync(string rawOrCanonicalPlate, Guid expectedUserId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result<VehicleOwnershipInfo>.Success(new VehicleOwnershipInfo(expectedUserId, Guid.NewGuid(), "51A12345", SupportedVehicleType.Car, true)));
    }

    private sealed class FakeClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset UtcNow => now;
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
