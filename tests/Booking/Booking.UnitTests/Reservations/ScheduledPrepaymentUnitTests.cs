using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using ParkingSystem.Booking.Application.Common.Messaging.Outbox;
using ParkingSystem.Booking.Application.Payments;
using ParkingSystem.Booking.Application.Reservations;
using ParkingSystem.Booking.Application.Reservations.Commands.ConfirmReservation;
using ParkingSystem.Booking.Domain.Reservations;
using ParkingSystem.BuildingBlocks.Results;
using ParkingSystem.BuildingBlocks.Time;
using ParkingSystem.Contracts.Events.Booking;
using Xunit;

namespace ParkingSystem.Tests.Booking.UnitTests.Reservations;

/// <summary>
/// Unit tests covering US-RES-004 "Prepay 100% for a scheduled booking" (FR-PAY-007, MF-07, BR-10.a, S-01..S-11).
/// </summary>
public sealed class ScheduledPrepaymentUnitTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 10, 10, 0, 0, TimeSpan.Zero);
    private readonly FakeClock _clock = new(Now);
    private readonly FakeReservationRepository _repo = new();
    private readonly FakeReservationPaymentClient _paymentClient = new();
    private readonly RecordingOutbox _outbox = new();
    private readonly RecordingLogger<ConfirmReservationCommandHandler> _logger = new();

    private ConfirmReservationCommandHandler CreateHandler() =>
        new(_repo, _paymentClient, _clock, _logger, _outbox);

    private static Reservation CreateScheduledHold(
        Guid reservationId,
        Guid userId,
        decimal prepaidAmount = 50000m,
        string? priceSnapshot = "{\"total\":50000,\"rate\":25000}",
        DateTimeOffset? expiresAt = null)
    {
        return Reservation.CreateTemporaryHold(
            reservationId,
            userId,
            Guid.NewGuid(),
            "51A12345",
            Guid.NewGuid(),
            VehicleGroup.Car,
            ReservationType.Scheduled,
            Now.AddHours(2),
            prepaidAmount,
            Now.AddMinutes(-5),
            durationMinutes: 120,
            tempHoldExpiresAt: expiresAt ?? Now.AddMinutes(5),
            priceSnapshot: priceSnapshot);
    }

    private static Reservation CreateDrivingHold(
        Guid reservationId,
        Guid userId,
        decimal prepaidAmount = 20000m)
    {
        return Reservation.CreateTemporaryHold(
            reservationId,
            userId,
            Guid.NewGuid(),
            "51A12345",
            Guid.NewGuid(),
            VehicleGroup.Car,
            ReservationType.HoldWhileDriving,
            Now.AddMinutes(20),
            prepaidAmount,
            Now.AddMinutes(-2),
            tempHoldExpiresAt: Now.AddMinutes(5));
    }

    // =========================================================================
    // S-01: scheduled, debit OK -> Confirmed; DebitAsync called once with purpose Scheduled and stored amount; 1 event with ReservationType "Scheduled"
    // =========================================================================
    [Fact]
    public async Task S_01_Scheduled_debit_ok_confirms_with_purpose_scheduled_and_emits_event()
    {
        var reservationId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var reservation = CreateScheduledHold(reservationId, userId, prepaidAmount: 50000m);
        _repo.Add(reservation);

        var handler = CreateHandler();
        var command = new ConfirmReservationCommand(reservationId, userId, "Wallet", 50000m);

        var result = await handler.Handle(command, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(ReservationStatus.Confirmed.ToString(), result.Value!.Status);
        Assert.Equal(ReservationStatus.Confirmed, reservation.Status);

        var call = Assert.Single(_paymentClient.DebitCalls);
        Assert.Equal(reservationId, call.ReservationId);
        Assert.Equal(userId, call.AccountId);
        Assert.Equal(50000L, call.AmountVnd);
        Assert.Equal(PrepaymentPurpose.Scheduled, call.Purpose);

        var evt = Assert.Single(_outbox.Events);
        var confirmedEvt = Assert.IsType<ReservationConfirmedIntegrationEvent>(evt);
        Assert.Equal("Scheduled", confirmedEvt.ReservationType);
        Assert.Equal(50000m, confirmedEvt.PrepaidAmount);
        Assert.Equal(reservationId, confirmedEvt.ReservationId);
    }

    // =========================================================================
    // S-02: client DepositAmount is ignored
    // =========================================================================
    [Fact]
    public async Task S_02_Client_deposit_amount_is_ignored()
    {
        var reservationId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var reservation = CreateScheduledHold(reservationId, userId, prepaidAmount: 50000m);
        _repo.Add(reservation);

        var handler = CreateHandler();
        // Client attempts to pass a different deposit amount (e.g. 10000m instead of stored 50000m)
        var command = new ConfirmReservationCommand(reservationId, userId, "Wallet", DepositAmount: 10000m);

        var result = await handler.Handle(command, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        var call = Assert.Single(_paymentClient.DebitCalls);
        Assert.Equal(50000L, call.AmountVnd);
        Assert.NotEqual(10000L, call.AmountVnd);
    }

    // =========================================================================
    // S-03: pricing provider would now return a different price -> amount debited is the stored one; snapshot unchanged; pricing provider not called
    // =========================================================================
    [Fact]
    public async Task S_03_Pricing_provider_price_change_ignored_amount_debited_is_stored_one()
    {
        var reservationId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        const string initialSnapshot = "{\"total\":50000,\"rate\":25000}";
        var reservation = CreateScheduledHold(reservationId, userId, prepaidAmount: 50000m, priceSnapshot: initialSnapshot);
        _repo.Add(reservation);

        // Confirm handler does NOT depend on or call IParkingPricingProvider
        var handler = CreateHandler();
        var command = new ConfirmReservationCommand(reservationId, userId, "Wallet", 50000m);

        var result = await handler.Handle(command, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        var call = Assert.Single(_paymentClient.DebitCalls);
        Assert.Equal(50000L, call.AmountVnd);
        Assert.Equal(initialSnapshot, reservation.PriceSnapshot);
    }

    // =========================================================================
    // S-04: insufficient -> TemporaryHold, exact code; retry after top-up -> Confirmed
    // =========================================================================
    [Fact]
    public async Task S_04_Insufficient_balance_keeps_temporary_hold_and_retry_after_topup_confirms()
    {
        var reservationId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var reservation = CreateScheduledHold(reservationId, userId, prepaidAmount: 50000m);
        _repo.Add(reservation);

        // 1. Initial attempt: insufficient balance
        _paymentClient.DebitHandlerWithPurpose = (_, _, _, _, _, _) =>
            Task.FromResult(Result<ReservationDebitResult>.Success(new ReservationDebitResult(
                ReservationDebitOutcome.InsufficientBalance, null, 1000L)));

        var handler = CreateHandler();
        var command = new ConfirmReservationCommand(reservationId, userId, "Wallet", 50000m);

        var result1 = await handler.Handle(command, TestContext.Current.CancellationToken);

        Assert.True(result1.IsFailure);
        Assert.Equal(ReservationErrors.InsufficientWalletBalance, result1.Error);
        Assert.Equal(ReservationStatus.TemporaryHold, reservation.Status);
        Assert.Empty(_outbox.Events);
        Assert.Empty(_paymentClient.RefundCalls);

        // 2. Retry after top-up: debit succeeds
        _paymentClient.DebitHandlerWithPurpose = (_, _, _, _, _, _) =>
            Task.FromResult(Result<ReservationDebitResult>.Success(new ReservationDebitResult(
                ReservationDebitOutcome.Succeeded, "LEDGER-TOPUP-01", 100000L)));

        var result2 = await handler.Handle(command, TestContext.Current.CancellationToken);

        Assert.True(result2.IsSuccess);
        Assert.Equal(ReservationStatus.Confirmed, reservation.Status);
        Assert.Single(_outbox.Events);
        Assert.Equal(2, _paymentClient.DebitCalls.Count);
    }

    // =========================================================================
    // S-05: wallet not active / amount mismatch -> Cancelled PaymentFailed, no event
    // =========================================================================
    [Theory]
    [InlineData(ReservationDebitOutcome.WalletNotActive)]
    [InlineData(ReservationDebitOutcome.AmountMismatch)]
    public async Task S_05_Wallet_not_active_or_amount_mismatch_cancels_with_payment_failed_and_no_event(ReservationDebitOutcome outcome)
    {
        var reservationId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var reservation = CreateScheduledHold(reservationId, userId, prepaidAmount: 50000m);
        _repo.Add(reservation);

        _paymentClient.DebitHandlerWithPurpose = (_, _, _, _, _, _) =>
            Task.FromResult(Result<ReservationDebitResult>.Success(new ReservationDebitResult(
                outcome, null, 0L)));

        var handler = CreateHandler();
        var command = new ConfirmReservationCommand(reservationId, userId, "Wallet", 50000m);

        var result = await handler.Handle(command, TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal(ReservationErrors.PaymentFailed, result.Error);
        Assert.Equal(ReservationStatus.Cancelled, reservation.Status);
        Assert.Equal(CancelReason.PaymentFailed, reservation.CancelReason);
        Assert.Empty(_outbox.Events);
        Assert.Empty(_paymentClient.RefundCalls);
    }

    // =========================================================================
    // S-06: transient + DEBITED -> Confirmed, no second debit; transient + NOT_DEBITED -> PaymentUnconfirmed, TemporaryHold
    // =========================================================================
    [Fact]
    public async Task S_06_Transient_failure_reconciles_via_debit_state()
    {
        var reservationId1 = Guid.NewGuid();
        var userId1 = Guid.NewGuid();
        var res1 = CreateScheduledHold(reservationId1, userId1, prepaidAmount: 50000m);
        _repo.Add(res1);

        // Part 1: Transient Debit failure + GetDebitStateAsync returns Debited -> Confirmed, no second debit
        _paymentClient.DebitHandlerWithPurpose = (_, _, _, _, _, _) =>
            Task.FromResult(Result<ReservationDebitResult>.Failure(ReservationPaymentErrors.Unavailable));
        _paymentClient.GetStateHandler = (_, _, _) =>
            Task.FromResult(Result<ReservationDebitState>.Success(ReservationDebitState.Debited));

        var handler = CreateHandler();
        var result1 = await handler.Handle(new ConfirmReservationCommand(reservationId1, userId1, "Wallet", 50000m), TestContext.Current.CancellationToken);

        Assert.True(result1.IsSuccess);
        Assert.Equal(ReservationStatus.Confirmed, res1.Status);
        Assert.Single(_paymentClient.DebitCalls);
        Assert.Single(_outbox.Events);

        // Part 2: Transient Debit failure + GetDebitStateAsync returns NotDebited -> PaymentUnconfirmed, stays TemporaryHold
        var reservationId2 = Guid.NewGuid();
        var userId2 = Guid.NewGuid();
        var res2 = CreateScheduledHold(reservationId2, userId2, prepaidAmount: 50000m);
        _repo.Add(res2);

        _paymentClient.GetStateHandler = (_, _, _) =>
            Task.FromResult(Result<ReservationDebitState>.Success(ReservationDebitState.NotDebited));

        var result2 = await handler.Handle(new ConfirmReservationCommand(reservationId2, userId2, "Wallet", 50000m), TestContext.Current.CancellationToken);

        Assert.True(result2.IsFailure);
        Assert.Equal(ReservationErrors.PaymentUnconfirmed, result2.Error);
        Assert.Equal(ReservationStatus.TemporaryHold, res2.Status);
        Assert.Empty(_paymentClient.RefundCalls);
    }

    // =========================================================================
    // S-07: hold expires during debit -> refund once, Expired, no event
    // =========================================================================
    [Fact]
    public async Task S_07_Hold_expires_during_debit_refunds_once_and_expires()
    {
        var reservationId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var reservation = CreateScheduledHold(reservationId, userId, prepaidAmount: 50000m, expiresAt: Now.AddMinutes(5));
        _repo.Add(reservation);

        _paymentClient.DebitHandlerWithPurpose = (_, _, _, _, _, _) =>
        {
            // Clock advances past expiration time during debit call
            _clock.UtcNow = Now.AddMinutes(10);
            return Task.FromResult(Result<ReservationDebitResult>.Success(new ReservationDebitResult(
                ReservationDebitOutcome.Succeeded, "LEDGER-01", 100000L)));
        };

        var handler = CreateHandler();
        var result = await handler.Handle(new ConfirmReservationCommand(reservationId, userId, "Wallet", 50000m), TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal(ReservationErrors.HoldExpired, result.Error);
        Assert.Equal(ReservationStatus.Expired, reservation.Status);
        Assert.Empty(_outbox.Events);

        var refund = Assert.Single(_paymentClient.RefundCalls);
        Assert.Equal(reservationId, refund.ReservationId);
        Assert.Equal(50000L, refund.AmountVnd);
    }

    // =========================================================================
    // S-08: save fails after debit -> compensation; second confirm returns PaymentFailed without debit
    // =========================================================================
    [Fact]
    public async Task S_08_Save_fails_after_debit_compensates_and_second_confirm_returns_payment_failed_without_debit()
    {
        var reservationId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var reservation = CreateScheduledHold(reservationId, userId, prepaidAmount: 50000m);
        _repo.Add(reservation);

        _repo.OnSaveChangesAsync = _ =>
        {
            _repo.OnSaveChangesAsync = null; // Next save (compensation) succeeds
            throw new Exception("Simulated database failure on confirm save.");
        };

        var handler = CreateHandler();
        var command = new ConfirmReservationCommand(reservationId, userId, "Wallet", 50000m);

        var result1 = await handler.Handle(command, TestContext.Current.CancellationToken);

        Assert.True(result1.IsFailure);
        Assert.Equal(ReservationErrors.PaymentUnconfirmed, result1.Error);
        Assert.Single(_paymentClient.RefundCalls);
        Assert.Equal(ReservationStatus.Cancelled, reservation.Status);
        Assert.Equal(CancelReason.PaymentFailed, reservation.CancelReason);
        Assert.Equal(PrepaidOutcome.Refunded, reservation.PrepaidOutcome);

        // Second confirm returns PaymentFailed without debiting
        var debitsBefore = _paymentClient.DebitCalls.Count;
        var result2 = await handler.Handle(command, TestContext.Current.CancellationToken);

        Assert.True(result2.IsFailure);
        Assert.Equal(ReservationErrors.PaymentFailed, result2.Error);
        Assert.Equal(debitsBefore, _paymentClient.DebitCalls.Count);
    }

    // =========================================================================
    // S-09: ALREADY_REFUNDED -> Cancelled PaymentFailed with PrepaidOutcome Refunded
    // =========================================================================
    [Fact]
    public async Task S_09_Already_refunded_cancels_with_payment_failed_and_prepaid_outcome_refunded()
    {
        var reservationId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var reservation = CreateScheduledHold(reservationId, userId, prepaidAmount: 50000m);
        _repo.Add(reservation);

        _paymentClient.DebitHandlerWithPurpose = (_, _, _, _, _, _) =>
            Task.FromResult(Result<ReservationDebitResult>.Success(new ReservationDebitResult(
                ReservationDebitOutcome.AlreadyRefunded, null, 100000L)));

        var handler = CreateHandler();
        var result = await handler.Handle(new ConfirmReservationCommand(reservationId, userId, "Wallet", 50000m), TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal(ReservationErrors.PaymentFailed, result.Error);
        Assert.Equal(ReservationStatus.Cancelled, reservation.Status);
        Assert.Equal(CancelReason.PaymentFailed, reservation.CancelReason);
        Assert.Equal(PrepaidOutcome.Refunded, reservation.PrepaidOutcome);
        Assert.Empty(_paymentClient.RefundCalls);
    }

    // =========================================================================
    // S-10: scheduled with PrepaidAmount 0, or null snapshot, or invalid snapshot JSON -> Cancelled PaymentFailed, Payment NOT called
    // =========================================================================
    [Theory]
    [InlineData(0, "{\"total\":50000}")]
    [InlineData(50000, null)]
    [InlineData(50000, "")]
    [InlineData(50000, "   ")]
    [InlineData(50000, "{not-a-valid-json:")]
    public async Task S_10_Scheduled_prepayment_guard_rejects_invalid_amount_or_snapshot_without_payment_call(
        decimal prepaidAmount, string? priceSnapshot)
    {
        var reservationId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var reservation = CreateScheduledHold(reservationId, userId, prepaidAmount: prepaidAmount, priceSnapshot: priceSnapshot);
        _repo.Add(reservation);

        var handler = CreateHandler();
        var command = new ConfirmReservationCommand(reservationId, userId, "Wallet", prepaidAmount);

        var result = await handler.Handle(command, TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal(ReservationErrors.PaymentFailed, result.Error);
        Assert.Equal(ReservationStatus.Cancelled, reservation.Status);
        Assert.Equal(CancelReason.PaymentFailed, reservation.CancelReason);
        Assert.Empty(_paymentClient.DebitCalls);
        Assert.Empty(_paymentClient.RefundCalls);
        Assert.Empty(_outbox.Events);
        Assert.Contains(_logger.Logs, l => l.Level == LogLevel.Error && l.Message.Contains(reservationId.ToString()));
    }

    // =========================================================================
    // S-11: regression: HoldWhileDriving still sends purpose Deposit; zero-deposit HWD still confirms without Payment
    // =========================================================================
    [Fact]
    public async Task S_11_Regression_hold_while_driving_purpose_and_zero_deposit()
    {
        var userId = Guid.NewGuid();

        // 1. HoldWhileDriving with deposit > 0 -> Debit called with purpose Deposit
        var res1Id = Guid.NewGuid();
        var res1 = CreateDrivingHold(res1Id, userId, prepaidAmount: 20000m);
        _repo.Add(res1);

        var handler = CreateHandler();
        var result1 = await handler.Handle(new ConfirmReservationCommand(res1Id, userId, "Wallet", 20000m), TestContext.Current.CancellationToken);

        Assert.True(result1.IsSuccess);
        var call = Assert.Single(_paymentClient.DebitCalls);
        Assert.Equal(PrepaymentPurpose.Deposit, call.Purpose);
        Assert.Equal(20000L, call.AmountVnd);

        // 2. HoldWhileDriving with deposit == 0 -> Confirmed directly without calling Payment
        var res2Id = Guid.NewGuid();
        var res2 = CreateDrivingHold(res2Id, userId, prepaidAmount: 0m);
        _repo.Add(res2);

        var result2 = await handler.Handle(new ConfirmReservationCommand(res2Id, userId, null, 0m), TestContext.Current.CancellationToken);

        Assert.True(result2.IsSuccess);
        Assert.Equal(ReservationStatus.Confirmed, res2.Status);
        Assert.Single(_paymentClient.DebitCalls); // No second debit call was made
    }

    // =========================================================================
    // Test Doubles
    // =========================================================================

    private sealed class FakeClock(DateTimeOffset initial) : IClock
    {
        public DateTimeOffset UtcNow { get; set; } = initial;
    }

    private sealed class RecordingOutbox : IOutboxWriter
    {
        public List<object> Events { get; } = [];
        public void Enqueue<TEvent>(TEvent integrationEvent, string eventType, int eventVersion,
            Guid? correlationId = null, Guid? causationId = null) where TEvent : class => Events.Add(integrationEvent);
    }

    private sealed class RecordingLogger<T> : ILogger<T>
    {
        public List<(LogLevel Level, string Message)> Logs { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Logs.Add((logLevel, formatter(state, exception)));
    }

    private sealed class FakeReservationRepository : IReservationRepository
    {
        private readonly List<Reservation> _list = [];
        private readonly Dictionary<Guid, (ReservationStatus Status, DateTimeOffset? ConfirmedAt, PrepaidOutcome? Outcome, DateTimeOffset? CancelledAt, CancelReason? Reason)> _snapshots = [];

        public Func<CancellationToken, Task>? OnSaveChangesAsync { get; set; }

        public void Add(Reservation r)
        {
            _list.Add(r);
            _snapshots[r.Id] = (r.Status, r.ConfirmedAt, r.PrepaidOutcome, r.CancelledAt, r.CancelReason);
        }

        public Task<Reservation?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(_list.FirstOrDefault(r => r.Id == id));

        public Task ReloadAsync(Reservation reservation, CancellationToken cancellationToken = default)
        {
            if (_snapshots.TryGetValue(reservation.Id, out var snap))
            {
                if (snap.Status != reservation.Status)
                {
                    if (snap.Status == ReservationStatus.Confirmed) reservation.Confirm(snap.ConfirmedAt ?? DateTimeOffset.UtcNow);
                    else if (snap.Status == ReservationStatus.Cancelled) reservation.Cancel(snap.CancelledAt ?? DateTimeOffset.UtcNow, snap.Reason ?? CancelReason.PaymentFailed, snap.Outcome);
                    else if (snap.Status == ReservationStatus.Expired) reservation.Expire();
                }
            }
            return Task.CompletedTask;
        }

        public void DiscardPendingChanges()
        {
            foreach (var r in _list)
            {
                if (_snapshots.TryGetValue(r.Id, out var snap))
                {
                    typeof(Reservation).GetProperty(nameof(Reservation.Status))!.SetValue(r, snap.Status);
                    typeof(Reservation).GetProperty(nameof(Reservation.ConfirmedAt))!.SetValue(r, snap.ConfirmedAt);
                    typeof(Reservation).GetProperty(nameof(Reservation.PrepaidOutcome))!.SetValue(r, snap.Outcome);
                    typeof(Reservation).GetProperty(nameof(Reservation.CancelledAt))!.SetValue(r, snap.CancelledAt);
                    typeof(Reservation).GetProperty(nameof(Reservation.CancelReason))!.SetValue(r, snap.Reason);
                }
            }
        }

        public Task<IReadOnlyList<Reservation>> ListByUserIdAsync(Guid userId, ReservationStatus? status = null, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Reservation>>(_list.Where(r => r.UserId == userId && (!status.HasValue || r.Status == status.Value)).ToList());

        public Task<IReadOnlyList<Reservation>> ListActiveByLotIdAsync(Guid lotId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Reservation>>(_list.Where(r => r.LotId == lotId && r.Status == ReservationStatus.Confirmed).ToList());

        public Task<Reservation?> FindActiveConfirmedByLotAndPlateAsync(Guid lotId, string plateNumber, DateTimeOffset arrivalTime, CancellationToken cancellationToken = default) =>
            Task.FromResult(_list.FirstOrDefault(r => r.LotId == lotId && r.PlateNumber == plateNumber && r.Status == ReservationStatus.Confirmed));

        public Task<IReadOnlyList<Reservation>> ListPaymentReconciliationCandidatesAsync(DateTimeOffset now, int batchSize, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Reservation>>([]);

        public Task<IReadOnlyList<Reservation>> ListNoShowCandidatesAsync(DateTimeOffset now, int batchSize, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Reservation>>([]);

        public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            if (OnSaveChangesAsync != null)
            {
                await OnSaveChangesAsync(cancellationToken);
            }
            foreach (var r in _list)
            {
                _snapshots[r.Id] = (r.Status, r.ConfirmedAt, r.PrepaidOutcome, r.CancelledAt, r.CancelReason);
            }
        }
    }

    private sealed class FakeReservationPaymentClient : IReservationPaymentClient
    {
        public List<(Guid ReservationId, Guid AccountId, Guid LotId, long AmountVnd, PrepaymentPurpose Purpose)> DebitCalls { get; } = [];
        public List<(Guid ReservationId, Guid AccountId)> GetStateCalls { get; } = [];
        public List<(Guid ReservationId, Guid AccountId, long AmountVnd, string Reason)> RefundCalls { get; } = [];

        public Func<Guid, Guid, Guid, long, PrepaymentPurpose, CancellationToken, Task<Result<ReservationDebitResult>>>? DebitHandlerWithPurpose { get; set; }
        public Func<Guid, Guid, CancellationToken, Task<Result<ReservationDebitState>>>? GetStateHandler { get; set; }
        public Func<Guid, Guid, long, string, CancellationToken, Task<Result<ReservationDebitState>>>? RefundHandler { get; set; }

        public Task<Result<ReservationDebitResult>> DebitAsync(
            Guid reservationId, Guid accountId, Guid lotId, long amountVnd, PrepaymentPurpose purpose, CancellationToken cancellationToken = default)
        {
            DebitCalls.Add((reservationId, accountId, lotId, amountVnd, purpose));
            if (DebitHandlerWithPurpose is not null) return DebitHandlerWithPurpose(reservationId, accountId, lotId, amountVnd, purpose, cancellationToken);
            return Task.FromResult(Result<ReservationDebitResult>.Success(new ReservationDebitResult(
                ReservationDebitOutcome.Succeeded, "LEDGER-DEFAULT", 100000L)));
        }

        public Task<Result<ReservationDebitState>> GetDebitStateAsync(
            Guid reservationId, Guid accountId, CancellationToken cancellationToken = default)
        {
            GetStateCalls.Add((reservationId, accountId));
            if (GetStateHandler is not null) return GetStateHandler(reservationId, accountId, cancellationToken);
            return Task.FromResult(Result<ReservationDebitState>.Success(ReservationDebitState.Debited));
        }

        public Task<Result<ReservationDebitState>> RefundAsync(
            Guid reservationId, Guid accountId, long amountVnd, string reason, CancellationToken cancellationToken = default)
        {
            RefundCalls.Add((reservationId, accountId, amountVnd, reason));
            if (RefundHandler is not null) return RefundHandler(reservationId, accountId, amountVnd, reason, cancellationToken);
            return Task.FromResult(Result<ReservationDebitState>.Success(ReservationDebitState.Refunded));
        }
    }
}

