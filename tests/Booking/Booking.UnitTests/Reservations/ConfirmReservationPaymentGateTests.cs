using Microsoft.Extensions.Logging;
using ParkingSystem.Booking.Application.Parking;
using ParkingSystem.Booking.Application.Payments;
using ParkingSystem.Booking.Application.Reservations;
using ParkingSystem.Booking.Application.Reservations.Commands.ConfirmReservation;
using ParkingSystem.Booking.Application.Reservations.Commands.CreateTemporaryHold;
using ParkingSystem.Booking.Application.Vehicles;
using ParkingSystem.Booking.Domain.Reservations;
using ParkingSystem.Booking.Application.Common.Messaging.Outbox;
using ParkingSystem.Contracts.Events.Booking;
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

    private ConfirmReservationCommandHandler CreateHandler(
        IReservationPaymentClient? paymentClient = null,
        IOutboxWriter? outbox = null) =>
        new(_repo, paymentClient ?? new UnavailablePaymentClient(), _clock, _logger, outbox ?? new RecordingOutbox());

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
    // T-C1a: HoldWhileDriving, cọc > 0 -> payment unavailable -> payment_unconfirmed
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
        Assert.Equal(ReservationErrors.PaymentUnconfirmed, result.Error);
        Assert.Equal(ReservationStatus.TemporaryHold, reservation.Status);
        Assert.Equal(0, _repo.SaveChangesCount);
    }

    // =========================================================================
    // T-C1b (Replaced by S-10): Scheduled (PrepaidAmount <= 0đ) -> bị huỷ với payment_failed
    // =========================================================================
    [Fact]
    public async Task T_C1b_Scheduled_reservations_are_always_blocked()
    {
        const decimal prepaidAmount = 0m;
        var userId = Guid.NewGuid();
        var reservation = CreateHold(Guid.NewGuid(), userId, ReservationType.Scheduled, prepaidAmount: prepaidAmount);
        _repo.Add(reservation);

        var handler = CreateHandler();
        var command = new ConfirmReservationCommand(reservation.Id, userId, "Wallet", prepaidAmount);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ReservationErrors.PaymentFailed, result.Error);
        Assert.Equal(ReservationStatus.Cancelled, reservation.Status);
        Assert.Equal(CancelReason.PaymentFailed, reservation.CancelReason);
        Assert.Equal(1, _repo.SaveChangesCount);
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

    [Fact]
    public async Task Confirmation_enqueues_one_customer_event_and_retry_does_not_duplicate_it()
    {
        var userId = Guid.NewGuid();
        var reservation = CreateHold(Guid.NewGuid(), userId);
        _repo.Add(reservation);
        var outbox = new RecordingOutbox();
        var handler = CreateHandler(outbox: outbox);
        var command = new ConfirmReservationCommand(reservation.Id, userId, null, 0m);

        Assert.True((await handler.Handle(command, TestContext.Current.CancellationToken)).IsSuccess);
        Assert.True((await handler.Handle(command, TestContext.Current.CancellationToken)).IsSuccess);
        var message = Assert.IsType<ReservationConfirmedIntegrationEvent>(Assert.Single(outbox.Events));
        Assert.Equal(userId, message.UserId);
        Assert.Equal(reservation.Id, message.ReservationId);
    }

    private sealed class RecordingOutbox : IOutboxWriter
    {
        public List<object> Events { get; } = [];
        public void Enqueue<TEvent>(TEvent integrationEvent, string eventType, int eventVersion,
            Guid? correlationId = null, Guid? causationId = null) where TEvent : class => Events.Add(integrationEvent);
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
    // T-C1e (Replaced by S-10): Đúng MỘT log Error có cấu trúc khi prepayment không hợp lệ, không lộ secret/biển số
    // =========================================================================
    [Fact]
    public async Task T_C1e_Logs_structured_error_with_required_fields_and_no_pii()
    {
        var userId = Guid.NewGuid();
        var reservation = CreateHold(Guid.NewGuid(), userId, ReservationType.Scheduled, prepaidAmount: 30000m);
        _repo.Add(reservation);

        var handler = CreateHandler();
        var command = new ConfirmReservationCommand(reservation.Id, userId, "Wallet", 30000m);

        await handler.Handle(command, CancellationToken.None);

        var errorLogs = _logger.Logs.Where(l => l.Level == LogLevel.Error).ToList();
        Assert.Single(errorLogs);

        var logMessage = errorLogs[0].Message;
        Assert.Contains(reservation.Id.ToString(), logMessage);
        Assert.Contains("30000", logMessage);
        Assert.Contains("invalid", logMessage, StringComparison.OrdinalIgnoreCase);

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

        var handler = new CreateTemporaryHoldCommandHandler(ownership, capacityProvider, coordinator, _clock, new TestLogger<CreateTemporaryHoldCommandHandler>(), new UnconfiguredParkingPricingProvider());
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

        var handler = new CreateTemporaryHoldCommandHandler(ownership, capacityProvider, coordinator, _clock, new TestLogger<CreateTemporaryHoldCommandHandler>(), new UnconfiguredParkingPricingProvider());
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

        var handler = new CreateTemporaryHoldCommandHandler(ownership, capacityProvider, coordinator, _clock, new TestLogger<CreateTemporaryHoldCommandHandler>(), new UnconfiguredParkingPricingProvider());
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

        var handler = new CreateTemporaryHoldCommandHandler(ownership, capacityProvider, coordinator, _clock, new TestLogger<CreateTemporaryHoldCommandHandler>(), new UnconfiguredParkingPricingProvider());
        var command = new CreateTemporaryHoldCommand(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "51A12345", Guid.NewGuid(), VehicleGroup.Car, ReservationType.HoldWhileDriving, Now.AddMinutes(20), null, 0m);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ReservationCapacityErrors.CapacityInfoUnavailable, result.Error);
        Assert.Equal(0, coordinator.CallCount);
    }

    // =========================================================================
    // T-P5: Scheduled -> Tính tiền theo Provider, không tin PrepaidAmount client gửi (BR-10.a)
    // =========================================================================
    [Fact]
    public async Task T_P5_Scheduled_ignores_client_prepaid_amount_and_calculates_from_provider()
    {
        const decimal clientAmount = 75000m;
        var capacityProvider = new FakeCapacityProvider
        {
            InfoToReturn = new ParkingLotCapacityInfo(10, 50, 5, ReservationEnabled: true, DepositAmount: 20000m)
        };
        var coordinator = new FakeCoordinator();
        var ownership = new FakeOwnership();
        var pricingProvider = new FakePricingProvider
        {
            InfoToReturn = new ParkingPricingInfo(Guid.NewGuid(), VehicleGroup.Car, 30000m, "PerEntry", null, 0m)
        };

        var handler = new CreateTemporaryHoldCommandHandler(ownership, capacityProvider, coordinator, _clock, new TestLogger<CreateTemporaryHoldCommandHandler>(), pricingProvider);
        var command = new CreateTemporaryHoldCommand(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "51A12345", Guid.NewGuid(), VehicleGroup.Car, ReservationType.Scheduled, Now.AddHours(2), 60, clientAmount);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(30000m, coordinator.CapturedReservation!.PrepaidAmount);
    }

    // Doubles
    private sealed class FakeReservationRepository : IReservationRepository
    {
        public Task ReloadAsync(Reservation reservation, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void DiscardPendingChanges() { }
        public Task<IReadOnlyList<Reservation>> ListPaymentReconciliationCandidatesAsync(DateTimeOffset now, int batchSize, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Reservation>>(_list.Where(r => r.Status == ReservationStatus.TemporaryHold && r.TempHoldExpiresAt < now && r.PrepaidAmount > 0 && r.PrepaidOutcome == null).Take(batchSize).ToList());

        public Task<IReadOnlyList<Reservation>> ListNoShowCandidatesAsync(DateTimeOffset now, int batchSize, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Reservation>>([]);

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

        public Task<Reservation?> FindCancelledNoSuitableSlotByLotAndPlateAsync(Guid lotId, string plateNumber, DateTimeOffset arrivalTime, CancellationToken cancellationToken = default) =>
            Task.FromResult<Reservation?>(null);

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

    private sealed class FakePricingProvider : IParkingPricingProvider
    {
        public ParkingPricingInfo InfoToReturn { get; set; } = new(Guid.NewGuid(), VehicleGroup.Car, 30000m, "PerEntry", null, 0m);

        public Task<Result<ParkingPricingInfo>> GetPricingInfoAsync(Guid lotId, VehicleGroup vehicleGroup, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result<ParkingPricingInfo>.Success(InfoToReturn));
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

    private sealed class UnavailablePaymentClient : ParkingSystem.Booking.Application.Payments.IReservationPaymentClient
    {
        public Task<Result<ParkingSystem.Booking.Application.Payments.ReservationDebitResult>> DebitAsync(Guid reservationId, Guid accountId, Guid lotId, long amountVnd, ParkingSystem.Booking.Application.Payments.PrepaymentPurpose purpose, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result<ParkingSystem.Booking.Application.Payments.ReservationDebitResult>.Failure(ParkingSystem.Booking.Application.Payments.ReservationPaymentErrors.Unavailable));

        public Task<Result<ParkingSystem.Booking.Application.Payments.ReservationDebitState>> GetDebitStateAsync(Guid reservationId, Guid accountId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result<ParkingSystem.Booking.Application.Payments.ReservationDebitState>.Failure(ParkingSystem.Booking.Application.Payments.ReservationPaymentErrors.Unavailable));

        public Task<Result<ParkingSystem.Booking.Application.Payments.ReservationDebitState>> RefundAsync(Guid reservationId, Guid accountId, long amountVnd, string reason, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result<ParkingSystem.Booking.Application.Payments.ReservationDebitState>.Failure(ParkingSystem.Booking.Application.Payments.ReservationPaymentErrors.Unavailable));
    }
}
