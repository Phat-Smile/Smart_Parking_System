using Microsoft.Extensions.Logging;
using ParkingSystem.Booking.Application.Common.Messaging.Outbox;
using ParkingSystem.Booking.Application.Payments;
using ParkingSystem.Booking.Application.Reservations;
using ParkingSystem.Booking.Application.Reservations.Commands.ConfirmReservation;
using ParkingSystem.Booking.Application.Vehicles;
using ParkingSystem.Booking.Domain.Reservations;
using ParkingSystem.Contracts.Events.Booking;
using ParkingSystem.BuildingBlocks.Results;
using ParkingSystem.BuildingBlocks.Time;
using Xunit;

namespace ParkingSystem.Tests.Booking.UnitTests.Reservations;

public sealed class ConfirmReservationDepositSagaTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 8, 12, 0, 0, TimeSpan.Zero);

    private readonly FakeReservationRepository _repo = new();
    private readonly FakeClock _clock = new(Now);
    private readonly RecordingLogger<ConfirmReservationCommandHandler> _logger = new();
    private readonly RecordingOutbox _outbox = new();
    private readonly FakeReservationPaymentClient _paymentClient = new();

    private ConfirmReservationCommandHandler CreateHandler() =>
        new(_repo, _paymentClient, _clock, _logger, _outbox);

    private static Reservation CreateHold(
        Guid reservationId,
        Guid userId,
        ReservationType type = ReservationType.HoldWhileDriving,
        decimal prepaidAmount = 20000m,
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
    // T-01: deposit > 0, debit OK -> Confirmed, 1 event, debit called once with reservation id and stored amount
    // =========================================================================
    [Fact]
    public async Task T_01_Deposit_positive_debit_ok_confirms_and_emits_one_event()
    {
        var userId = Guid.NewGuid();
        var reservation = CreateHold(Guid.NewGuid(), userId, ReservationType.HoldWhileDriving, prepaidAmount: 20000m);
        _repo.Add(reservation);

        var handler = CreateHandler();
        var command = new ConfirmReservationCommand(reservation.Id, userId, "Wallet", 20000m);

        var result = await handler.Handle(command, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(ReservationStatus.Confirmed, reservation.Status);
        Assert.Equal(1, _repo.SaveChangesCount);

        var call = Assert.Single(_paymentClient.DebitCalls);
        Assert.Equal(reservation.Id, call.ReservationId);
        Assert.Equal(userId, call.AccountId);
        Assert.Equal(reservation.LotId, call.LotId);
        Assert.Equal(20000L, call.AmountVnd);
        Assert.Empty(_paymentClient.RefundCalls);

        var evt = Assert.Single(_outbox.Events);
        var confirmedEvt = Assert.IsType<ReservationConfirmedIntegrationEvent>(evt);
        Assert.Equal(reservation.Id, confirmedEvt.ReservationId);
        Assert.Equal(userId, confirmedEvt.UserId);
        Assert.Equal(reservation.LotId, confirmedEvt.LotId);
        Assert.Equal(ReservationType.HoldWhileDriving.ToString(), confirmedEvt.ReservationType);
        Assert.Equal(20000m, confirmedEvt.PrepaidAmount);
        Assert.Equal(reservation.AppointmentTime, confirmedEvt.AppointmentTime);
        Assert.Equal(Now, confirmedEvt.ConfirmedAtUtc);
    }

    // =========================================================================
    // T-02: client DepositAmount differs -> stored PrepaidAmount is used
    // =========================================================================
    [Fact]
    public async Task T_02_Client_deposit_amount_differs_stored_prepaid_amount_is_used()
    {
        var userId = Guid.NewGuid();
        var reservation = CreateHold(Guid.NewGuid(), userId, ReservationType.HoldWhileDriving, prepaidAmount: 25000m);
        _repo.Add(reservation);

        var handler = CreateHandler();
        var command = new ConfirmReservationCommand(reservation.Id, userId, "Wallet", 999999m);

        var result = await handler.Handle(command, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        var call = Assert.Single(_paymentClient.DebitCalls);
        Assert.Equal(25000L, call.AmountVnd);
    }

    // =========================================================================
    // T-03: zero deposit -> Confirmed, payment NOT called, 1 event
    // =========================================================================
    [Fact]
    public async Task T_03_Zero_deposit_confirms_without_calling_payment_and_emits_one_event()
    {
        var userId = Guid.NewGuid();
        var reservation = CreateHold(Guid.NewGuid(), userId, ReservationType.HoldWhileDriving, prepaidAmount: 0m);
        _repo.Add(reservation);

        var handler = CreateHandler();
        var command = new ConfirmReservationCommand(reservation.Id, userId, null, 0m);

        var result = await handler.Handle(command, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(ReservationStatus.Confirmed, reservation.Status);
        Assert.Empty(_paymentClient.DebitCalls);
        Assert.Empty(_paymentClient.RefundCalls);
        Assert.Single(_outbox.Events);
    }

    // =========================================================================
    // T-04: insufficient balance -> TemporaryHold, no event, exact error InsufficientWalletBalance
    // =========================================================================
    [Fact]
    public async Task T_04_Insufficient_balance_leaves_temporary_hold_no_event_exact_error()
    {
        var userId = Guid.NewGuid();
        var reservation = CreateHold(Guid.NewGuid(), userId, ReservationType.HoldWhileDriving, prepaidAmount: 30000m);
        _repo.Add(reservation);

        _paymentClient.DebitHandler = (_, _, _, _, _) => Task.FromResult(
            Result<ReservationDebitResult>.Success(new ReservationDebitResult(
                ReservationDebitOutcome.InsufficientBalance, null, 5000L)));

        var handler = CreateHandler();
        var command = new ConfirmReservationCommand(reservation.Id, userId, "Wallet", 30000m);

        var result = await handler.Handle(command, TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal(ReservationErrors.InsufficientWalletBalance, result.Error);
        Assert.Equal(ReservationStatus.TemporaryHold, reservation.Status);
        Assert.Empty(_outbox.Events);
        Assert.Empty(_paymentClient.RefundCalls);
    }

    // =========================================================================
    // T-05: retry after insufficient balance -> Confirmed
    // =========================================================================
    [Fact]
    public async Task T_05_Retry_after_insufficient_balance_succeeds()
    {
        var userId = Guid.NewGuid();
        var reservation = CreateHold(Guid.NewGuid(), userId, ReservationType.HoldWhileDriving, prepaidAmount: 30000m);
        _repo.Add(reservation);

        var handler = CreateHandler();
        var command = new ConfirmReservationCommand(reservation.Id, userId, "Wallet", 30000m);

        // Attempt 1: Insufficient balance
        _paymentClient.DebitHandler = (_, _, _, _, _) => Task.FromResult(
            Result<ReservationDebitResult>.Success(new ReservationDebitResult(
                ReservationDebitOutcome.InsufficientBalance, null, 5000L)));

        var result1 = await handler.Handle(command, TestContext.Current.CancellationToken);
        Assert.True(result1.IsFailure);
        Assert.Equal(ReservationErrors.InsufficientWalletBalance, result1.Error);
        Assert.Equal(ReservationStatus.TemporaryHold, reservation.Status);

        // Attempt 2: After top-up -> debit succeeds
        _paymentClient.DebitHandler = (_, _, _, _, _) => Task.FromResult(
            Result<ReservationDebitResult>.Success(new ReservationDebitResult(
                ReservationDebitOutcome.Succeeded, "LEDGER-RETRY-01", 70000L)));

        var result2 = await handler.Handle(command, TestContext.Current.CancellationToken);
        Assert.True(result2.IsSuccess);
        Assert.Equal(ReservationStatus.Confirmed, reservation.Status);
        Assert.Single(_outbox.Events);
        Assert.Equal(2, _paymentClient.DebitCalls.Count);
    }

    // =========================================================================
    // T-06: wallet not active -> Cancelled with PaymentFailed, no event
    // =========================================================================
    [Fact]
    public async Task T_06_Wallet_not_active_cancels_reservation_with_PaymentFailed_no_event()
    {
        var userId = Guid.NewGuid();
        var reservation = CreateHold(Guid.NewGuid(), userId, ReservationType.HoldWhileDriving, prepaidAmount: 20000m);
        _repo.Add(reservation);

        _paymentClient.DebitHandler = (_, _, _, _, _) => Task.FromResult(
            Result<ReservationDebitResult>.Success(new ReservationDebitResult(
                ReservationDebitOutcome.WalletNotActive, null, 0L)));

        var handler = CreateHandler();
        var command = new ConfirmReservationCommand(reservation.Id, userId, "Wallet", 20000m);

        var result = await handler.Handle(command, TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal(ReservationErrors.PaymentFailed, result.Error);
        Assert.Equal(ReservationStatus.Cancelled, reservation.Status);
        Assert.Empty(_outbox.Events);
        Assert.Empty(_paymentClient.RefundCalls);
    }

    // =========================================================================
    // T-07: amount mismatch -> Cancelled with PaymentFailed, no event
    // =========================================================================
    [Fact]
    public async Task T_07_Amount_mismatch_cancels_reservation_with_PaymentFailed_no_event()
    {
        var userId = Guid.NewGuid();
        var reservation = CreateHold(Guid.NewGuid(), userId, ReservationType.HoldWhileDriving, prepaidAmount: 20000m);
        _repo.Add(reservation);

        _paymentClient.DebitHandler = (_, _, _, _, _) => Task.FromResult(
            Result<ReservationDebitResult>.Success(new ReservationDebitResult(
                ReservationDebitOutcome.AmountMismatch, null, 100000L)));

        var handler = CreateHandler();
        var command = new ConfirmReservationCommand(reservation.Id, userId, "Wallet", 20000m);

        var result = await handler.Handle(command, TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal(ReservationErrors.PaymentFailed, result.Error);
        Assert.Equal(ReservationStatus.Cancelled, reservation.Status);
        Assert.Empty(_outbox.Events);
        Assert.Empty(_paymentClient.RefundCalls);
    }

    // =========================================================================
    // T-08: transient failure + DEBITED on query -> Confirmed, debit not called twice
    // =========================================================================
    [Fact]
    public async Task T_08_Transient_failure_debited_on_query_confirms_debit_not_called_twice()
    {
        var userId = Guid.NewGuid();
        var reservation = CreateHold(Guid.NewGuid(), userId, ReservationType.HoldWhileDriving, prepaidAmount: 20000m);
        _repo.Add(reservation);

        _paymentClient.DebitHandler = (_, _, _, _, _) => Task.FromResult(
            Result<ReservationDebitResult>.Failure(ReservationPaymentErrors.Unavailable));

        _paymentClient.GetStateHandler = (_, _, _) => Task.FromResult(
            Result<ReservationDebitState>.Success(ReservationDebitState.Debited));

        var handler = CreateHandler();
        var command = new ConfirmReservationCommand(reservation.Id, userId, "Wallet", 20000m);

        var result = await handler.Handle(command, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(ReservationStatus.Confirmed, reservation.Status);
        Assert.Single(_paymentClient.DebitCalls);
        Assert.Single(_paymentClient.GetStateCalls);
        Assert.Single(_outbox.Events);
        Assert.Empty(_paymentClient.RefundCalls);
    }

    // =========================================================================
    // T-09: transient failure + NOT_DEBITED -> PaymentUnconfirmed, TemporaryHold, no refund
    // =========================================================================
    [Fact]
    public async Task T_09_Transient_failure_not_debited_on_query_returns_PaymentUnconfirmed_no_refund()
    {
        var userId = Guid.NewGuid();
        var reservation = CreateHold(Guid.NewGuid(), userId, ReservationType.HoldWhileDriving, prepaidAmount: 20000m);
        _repo.Add(reservation);

        _paymentClient.DebitHandler = (_, _, _, _, _) => Task.FromResult(
            Result<ReservationDebitResult>.Failure(ReservationPaymentErrors.Unavailable));

        _paymentClient.GetStateHandler = (_, _, _) => Task.FromResult(
            Result<ReservationDebitState>.Success(ReservationDebitState.NotDebited));

        var handler = CreateHandler();
        var command = new ConfirmReservationCommand(reservation.Id, userId, "Wallet", 20000m);

        var result = await handler.Handle(command, TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal(ReservationErrors.PaymentUnconfirmed, result.Error);
        Assert.Equal(ReservationStatus.TemporaryHold, reservation.Status);
        Assert.Empty(_outbox.Events);
        Assert.Empty(_paymentClient.RefundCalls);
    }

    // =========================================================================
    // T-10: transient failure + state query fails -> PaymentUnconfirmed, TemporaryHold, no refund
    // =========================================================================
    [Fact]
    public async Task T_10_Transient_failure_state_query_fails_returns_PaymentUnconfirmed_no_refund()
    {
        var userId = Guid.NewGuid();
        var reservation = CreateHold(Guid.NewGuid(), userId, ReservationType.HoldWhileDriving, prepaidAmount: 20000m);
        _repo.Add(reservation);

        _paymentClient.DebitHandler = (_, _, _, _, _) => Task.FromResult(
            Result<ReservationDebitResult>.Failure(ReservationPaymentErrors.Unavailable));

        _paymentClient.GetStateHandler = (_, _, _) => Task.FromResult(
            Result<ReservationDebitState>.Failure(ReservationPaymentErrors.Unavailable));

        var handler = CreateHandler();
        var command = new ConfirmReservationCommand(reservation.Id, userId, "Wallet", 20000m);

        var result = await handler.Handle(command, TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal(ReservationErrors.PaymentUnconfirmed, result.Error);
        Assert.Equal(ReservationStatus.TemporaryHold, reservation.Status);
        Assert.Empty(_outbox.Events);
        Assert.Empty(_paymentClient.RefundCalls);
    }

    // =========================================================================
    // T-11: expired before debit -> debit NOT called, HoldExpired
    // =========================================================================
    [Fact]
    public async Task T_11_Expired_before_debit_returns_HoldExpired_debit_not_called()
    {
        var userId = Guid.NewGuid();
        var reservation = CreateHold(Guid.NewGuid(), userId, ReservationType.HoldWhileDriving, prepaidAmount: 20000m,
            expiresAt: Now.AddMinutes(-1));
        _repo.Add(reservation);

        var handler = CreateHandler();
        var command = new ConfirmReservationCommand(reservation.Id, userId, "Wallet", 20000m);

        var result = await handler.Handle(command, TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal(ReservationErrors.HoldExpired, result.Error);
        Assert.Empty(_paymentClient.DebitCalls);
        Assert.Empty(_paymentClient.RefundCalls);
    }

    // =========================================================================
    // T-12: hold expires during debit -> refund once, not Confirmed, no event
    // =========================================================================
    [Fact]
    public async Task T_12_Hold_expires_during_debit_refunds_once_not_confirmed_no_event()
    {
        var userId = Guid.NewGuid();
        var reservation = CreateHold(Guid.NewGuid(), userId, ReservationType.HoldWhileDriving, prepaidAmount: 20000m);
        _repo.Add(reservation);

        _paymentClient.DebitHandler = (_, _, _, _, _) =>
        {
            // Advance clock past expiration
            _clock.UtcNow = Now.AddMinutes(15);
            return Task.FromResult(Result<ReservationDebitResult>.Success(new ReservationDebitResult(
                ReservationDebitOutcome.Succeeded, "LEDGER-01", 100000L)));
        };

        var handler = CreateHandler();
        var command = new ConfirmReservationCommand(reservation.Id, userId, "Wallet", 20000m);

        var result = await handler.Handle(command, TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal(ReservationErrors.HoldExpired, result.Error);
        Assert.Equal(ReservationStatus.Expired, reservation.Status);
        Assert.Empty(_outbox.Events);

        var refundCall = Assert.Single(_paymentClient.RefundCalls);
        Assert.Equal(reservation.Id, refundCall.ReservationId);
        Assert.Equal(userId, refundCall.AccountId);
        Assert.Equal(20000L, refundCall.AmountVnd);
    }

    // =========================================================================
    // T-13: cancelled during debit -> refund once
    // =========================================================================
    [Fact]
    public async Task T_13_Cancelled_during_debit_refunds_once()
    {
        var userId = Guid.NewGuid();
        var reservation = CreateHold(Guid.NewGuid(), userId, ReservationType.HoldWhileDriving, prepaidAmount: 20000m);
        _repo.Add(reservation);

        _paymentClient.DebitHandler = (_, _, _, _, _) =>
        {
            // Someone cancelled the reservation concurrently
            reservation.Cancel(Now, CancelReason.CustomerCancelled);
            return Task.FromResult(Result<ReservationDebitResult>.Success(new ReservationDebitResult(
                ReservationDebitOutcome.Succeeded, "LEDGER-01", 100000L)));
        };

        var handler = CreateHandler();
        var command = new ConfirmReservationCommand(reservation.Id, userId, "Wallet", 20000m);

        var result = await handler.Handle(command, TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal(ReservationErrors.InvalidStatus, result.Error);
        Assert.Equal(ReservationStatus.Cancelled, reservation.Status);
        Assert.Empty(_outbox.Events);

        var refundCall = Assert.Single(_paymentClient.RefundCalls);
        Assert.Equal(reservation.Id, refundCall.ReservationId);
        Assert.Equal(20000L, refundCall.AmountVnd);
    }

    // =========================================================================
    // T-14: Scheduled -> PaymentNotAvailable, debit not called
    // =========================================================================
    [Fact]
    public async Task T_14_Scheduled_returns_PaymentNotAvailable_debit_not_called()
    {
        var userId = Guid.NewGuid();
        var reservation = CreateHold(Guid.NewGuid(), userId, ReservationType.Scheduled, prepaidAmount: 50000m);
        _repo.Add(reservation);

        var handler = CreateHandler();
        var command = new ConfirmReservationCommand(reservation.Id, userId, "Wallet", 50000m);

        var result = await handler.Handle(command, TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal(ReservationErrors.PaymentNotAvailable, result.Error);
        Assert.Empty(_paymentClient.DebitCalls);
        Assert.Empty(_paymentClient.RefundCalls);
    }

    // =========================================================================
    // T-15: already Confirmed -> replay success, debit not called
    // =========================================================================
    [Fact]
    public async Task T_15_Already_confirmed_replays_success_debit_not_called()
    {
        var userId = Guid.NewGuid();
        var reservation = CreateHold(Guid.NewGuid(), userId, ReservationType.HoldWhileDriving, prepaidAmount: 20000m);
        reservation.Confirm(Now.AddMinutes(-1));
        _repo.Add(reservation);

        var handler = CreateHandler();
        var command = new ConfirmReservationCommand(reservation.Id, userId, "Wallet", 20000m);

        var result = await handler.Handle(command, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(ReservationStatus.Confirmed.ToString(), result.Value!.Status);
        Assert.StartsWith("RES-QR-", result.Value.QrCodeToken);
        Assert.Empty(_paymentClient.DebitCalls);
        Assert.Empty(_paymentClient.RefundCalls);
        Assert.Empty(_outbox.Events);
    }

    // =========================================================================
    // T-16: another user -> AccessDenied, debit not called
    // =========================================================================
    [Fact]
    public async Task T_16_Another_user_returns_AccessDenied_debit_not_called()
    {
        var ownerId = Guid.NewGuid();
        var anotherUser = Guid.NewGuid();
        var reservation = CreateHold(Guid.NewGuid(), ownerId, ReservationType.HoldWhileDriving, prepaidAmount: 20000m);
        _repo.Add(reservation);

        var handler = CreateHandler();
        var command = new ConfirmReservationCommand(reservation.Id, anotherUser, "Wallet", 20000m);

        var result = await handler.Handle(command, TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal(ReservationErrors.AccessDenied, result.Error);
        Assert.Empty(_paymentClient.DebitCalls);
    }

    // =========================================================================
    // Doubles / Fakes
    // =========================================================================
    private sealed class FakeReservationRepository : IReservationRepository
    {
        private readonly List<Reservation> _list = [];
        public int SaveChangesCount { get; private set; }

        public Task ReloadAsync(Reservation reservation, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<IReadOnlyList<Reservation>> ListPaymentReconciliationCandidatesAsync(
            DateTimeOffset now, int batchSize, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Reservation>>(_list.Where(r =>
                r.Status == ReservationStatus.TemporaryHold &&
                r.TempHoldExpiresAt < now &&
                r.PrepaidAmount > 0 &&
                r.PrepaidOutcome == null).Take(batchSize).ToList());

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
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) { }
    }

    private sealed class FakeReservationPaymentClient : IReservationPaymentClient
    {
        public List<(Guid ReservationId, Guid AccountId, Guid LotId, long AmountVnd)> DebitCalls { get; } = [];
        public List<(Guid ReservationId, Guid AccountId)> GetStateCalls { get; } = [];
        public List<(Guid ReservationId, Guid AccountId, long AmountVnd, string Reason)> RefundCalls { get; } = [];

        public Func<Guid, Guid, Guid, long, CancellationToken, Task<Result<ReservationDebitResult>>>? DebitHandler { get; set; }
        public Func<Guid, Guid, CancellationToken, Task<Result<ReservationDebitState>>>? GetStateHandler { get; set; }
        public Func<Guid, Guid, long, string, CancellationToken, Task<Result<ReservationDebitState>>>? RefundHandler { get; set; }

        public Task<Result<ReservationDebitResult>> DebitAsync(
            Guid reservationId, Guid accountId, Guid lotId, long amountVnd, CancellationToken cancellationToken = default)
        {
            DebitCalls.Add((reservationId, accountId, lotId, amountVnd));
            if (DebitHandler is not null) return DebitHandler(reservationId, accountId, lotId, amountVnd, cancellationToken);
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
