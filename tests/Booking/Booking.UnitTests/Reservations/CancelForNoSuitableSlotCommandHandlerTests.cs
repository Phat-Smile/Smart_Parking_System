using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ParkingSystem.Booking.Application.Common.Messaging.Outbox;
using ParkingSystem.Booking.Application.Payments;
using ParkingSystem.Booking.Application.Reservations;
using ParkingSystem.Booking.Application.Reservations.Commands.CancelForNoSuitableSlot;
using ParkingSystem.Booking.Domain.Reservations;
using ParkingSystem.Booking.Infrastructure.Payments;
using ParkingSystem.BuildingBlocks.Results;
using ParkingSystem.BuildingBlocks.Time;
using ParkingSystem.Contracts.Events.Booking;
using Xunit;

namespace ParkingSystem.Tests.Booking.UnitTests.Reservations;

public sealed class CancelForNoSuitableSlotCommandHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 18, 10, 0, 0, TimeSpan.Zero);

    private readonly FakeReservationRepository _repo = new();
    private readonly FakeReservationPaymentClient _paymentClient = new();
    private readonly RecordingOutbox _outbox = new();
    private readonly FakeClock _clock = new(Now);

    private CancelForNoSuitableSlotCommandHandler CreateHandler() =>
        new(_repo, _paymentClient, _clock, NullLogger<CancelForNoSuitableSlotCommandHandler>.Instance, _outbox);

    private ReservationPaymentReconciliationWorker CreateWorker()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IReservationRepository>(_repo);
        services.AddSingleton<IReservationPaymentClient>(_paymentClient);
        services.AddSingleton<IClock>(_clock);
        var sp = services.BuildServiceProvider();
        var scopeFactory = sp.GetRequiredService<IServiceScopeFactory>();
        var workerOptions = Options.Create(new ReservationPaymentReconciliationOptions { BatchSize = 50 });
        return new ReservationPaymentReconciliationWorker(scopeFactory, workerOptions, NullLogger<ReservationPaymentReconciliationWorker>.Instance);
    }

    private static Reservation CreateHold(
        Guid id,
        Guid lotId,
        string plateNumber,
        ReservationStatus status = ReservationStatus.Confirmed,
        decimal prepaidAmount = 50000m,
        DateTimeOffset? appointmentTime = null)
    {
        var appTime = appointmentTime ?? Now.AddMinutes(10);
        var res = Reservation.CreateTemporaryHold(
            id,
            Guid.NewGuid(),
            Guid.NewGuid(),
            plateNumber,
            lotId,
            VehicleGroup.Car,
            ReservationType.HoldWhileDriving,
            appTime,
            prepaidAmount,
            Now.AddMinutes(-5));

        if (status == ReservationStatus.Confirmed)
        {
            res.Confirm(Now.AddMinutes(-4));
        }

        return res;
    }

    [Fact]
    public async Task S01_ConfirmedReservation_CancelsAndRefundsSuccessfully()
    {
        var lotId = Guid.NewGuid();
        var reservationId = Guid.NewGuid();
        var res = CreateHold(reservationId, lotId, "51A12345", prepaidAmount: 50000m);
        _repo.Add(res);

        var handler = CreateHandler();
        var command = new CancelForNoSuitableSlotCommand(lotId, "51A12345", Now);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value!.Cancelled);
        Assert.Equal(reservationId, result.Value.ReservationId);

        Assert.Equal(ReservationStatus.Cancelled, res.Status);
        Assert.Equal(CancelReason.NoSuitableSlot, res.CancelReason);
        Assert.False(res.IsNoShow);
        Assert.Equal(PrepaidOutcome.Refunded, res.PrepaidOutcome);

        // Outbox event
        var evt = Assert.Single(_outbox.Events.OfType<ReservationCancelledIntegrationEvent>());
        Assert.Equal(reservationId, evt.ReservationId);
        Assert.Equal("NoSuitableSlot", evt.CancelReason);
        Assert.Equal(50000m, evt.RefundAmount);
        Assert.Equal(0m, evt.ForfeitedAmount);

        // Payment refund call
        var call = Assert.Single(_paymentClient.RefundCalls);
        Assert.Equal(reservationId, call.ReservationId);
        Assert.Equal(50000, call.AmountVnd);
        Assert.Equal("NoSuitableSlot", call.Reason);
    }

    [Fact]
    public async Task S02_ZeroPrepaidAmount_CancelsWithoutCallingRefund()
    {
        var lotId = Guid.NewGuid();
        var reservationId = Guid.NewGuid();
        var res = CreateHold(reservationId, lotId, "51A12345", prepaidAmount: 0m);
        _repo.Add(res);

        var handler = CreateHandler();
        var command = new CancelForNoSuitableSlotCommand(lotId, "51A12345", Now);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value!.Cancelled);
        Assert.Equal(reservationId, result.Value.ReservationId);

        Assert.Equal(ReservationStatus.Cancelled, res.Status);
        Assert.Equal(CancelReason.NoSuitableSlot, res.CancelReason);
        Assert.Null(res.PrepaidOutcome);

        var evt = Assert.Single(_outbox.Events.OfType<ReservationCancelledIntegrationEvent>());
        Assert.Equal(0m, evt.RefundAmount);

        Assert.Empty(_paymentClient.RefundCalls);
    }

    [Fact]
    public async Task S03_RefundAsyncFails_LeavesOutcomeNullForReconciliationWorker()
    {
        var lotId = Guid.NewGuid();
        var reservationId = Guid.NewGuid();
        var res = CreateHold(reservationId, lotId, "51A12345", prepaidAmount: 50000m);
        _repo.Add(res);

        _paymentClient.RefundResultToReturn = Result<ReservationDebitState>.Failure(ReservationPaymentErrors.Unavailable);

        var handler = CreateHandler();
        var command = new CancelForNoSuitableSlotCommand(lotId, "51A12345", Now);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value!.Cancelled);
        Assert.Equal(reservationId, result.Value.ReservationId);

        Assert.Equal(ReservationStatus.Cancelled, res.Status);
        Assert.Equal(CancelReason.NoSuitableSlot, res.CancelReason);
        Assert.Null(res.PrepaidOutcome); // Pending worker

        Assert.Single(_paymentClient.RefundCalls);
        Assert.Single(_outbox.Events.OfType<ReservationCancelledIntegrationEvent>());
    }

    [Fact]
    public async Task S04_AfterFailedRefund_ReconciliationWorkerRefundsExactlyOnce_SecondRunZeroRefunds()
    {
        var lotId = Guid.NewGuid();
        var reservationId = Guid.NewGuid();
        var res = CreateHold(reservationId, lotId, "51A12345", prepaidAmount: 50000m);
        _repo.Add(res);

        _paymentClient.RefundResultToReturn = Result<ReservationDebitState>.Failure(ReservationPaymentErrors.Unavailable);

        var handler = CreateHandler();
        var command = new CancelForNoSuitableSlotCommand(lotId, "51A12345", Now);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value!.Cancelled);
        Assert.Equal(reservationId, result.Value.ReservationId);

        Assert.Equal(ReservationStatus.Cancelled, res.Status);
        Assert.Equal(CancelReason.NoSuitableSlot, res.CancelReason);
        Assert.Null(res.PrepaidOutcome); // Pending worker

        // Reset payment client to succeed when worker runs
        _paymentClient.RefundCalls.Clear();
        _paymentClient.RefundResultToReturn = Result<ReservationDebitState>.Success(ReservationDebitState.Refunded);

        var worker = CreateWorker();

        // Pass 1: Worker reconciles the failed refund -> exactly 1 refund, outcome Refunded
        var processed1 = await worker.ReconcileBatchAsync(CancellationToken.None);
        Assert.Equal(1, processed1);
        var call = Assert.Single(_paymentClient.RefundCalls);
        Assert.Equal(reservationId, call.ReservationId);
        Assert.Equal(50000, call.AmountVnd);
        Assert.Equal("NoSuitableSlot", call.Reason);
        Assert.Equal(PrepaidOutcome.Refunded, res.PrepaidOutcome);

        // Pass 2: Second worker run -> 0 refunds
        _paymentClient.RefundCalls.Clear();
        var processed2 = await worker.ReconcileBatchAsync(CancellationToken.None);
        Assert.Equal(0, processed2);
        Assert.Empty(_paymentClient.RefundCalls);
    }

    [Fact]
    public async Task S05_IdempotentRepeat_ReturnsCancelledTrueWithoutDoubleRefundOrEvent()
    {
        var lotId = Guid.NewGuid();
        var reservationId = Guid.NewGuid();
        var res = CreateHold(reservationId, lotId, "51A12345", prepaidAmount: 50000m);
        res.CancelForNoSuitableSlot(Now.AddMinutes(-1));
        res.RecordCancellationRefunded();
        _repo.Add(res);

        var handler = CreateHandler();
        var command = new CancelForNoSuitableSlotCommand(lotId, "51A12345", Now);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value!.Cancelled);
        Assert.Equal(reservationId, result.Value.ReservationId);

        Assert.True(_repo.FindCancelledCallsCount >= 1, "Expected FindCancelledNoSuitableSlotByLotAndPlateAsync to be queried.");
        Assert.Empty(_paymentClient.RefundCalls);
        Assert.Empty(_outbox.Events);
    }

    [Fact]
    public async Task S06_UsedReservation_ReturnsCancelledFalse_AndRemainsUsed()
    {
        var lotId = Guid.NewGuid();
        var reservationId = Guid.NewGuid();
        var res = CreateHold(reservationId, lotId, "51A12345", prepaidAmount: 50000m);
        res.Use(Now.AddMinutes(-2), Guid.NewGuid());
        _repo.Add(res);

        var handler = CreateHandler();
        var command = new CancelForNoSuitableSlotCommand(lotId, "51A12345", Now);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value!.Cancelled);
        Assert.Null(result.Value.ReservationId);
        Assert.Equal(ReservationStatus.Used, res.Status);
        Assert.Empty(_paymentClient.RefundCalls);
        Assert.Empty(_outbox.Events);
    }

    [Fact]
    public void S07_Domain_UsedReservationThrowsOnCancelOrExpire_AndCancelForNoSuitableSlotThrowsOnInvalidStatuses()
    {
        // 1. Used reservation invariant (BR-03.c): never cancelled or expired
        var usedRes = CreateHold(Guid.NewGuid(), Guid.NewGuid(), "51A12345", prepaidAmount: 50000m);
        usedRes.Use(Now.AddMinutes(-5), Guid.NewGuid());

        Assert.Throws<ReservationDomainException>(() => usedRes.Cancel(Now, CancelReason.CustomerCancelled));
        Assert.Throws<ReservationDomainException>(() => usedRes.Cancel(Now.AddMinutes(20), CancelReason.NoShow));
        Assert.Throws<ReservationDomainException>(() => usedRes.Expire());
        Assert.Throws<ReservationDomainException>(() => usedRes.CancelForNoSuitableSlot(Now));

        // 2. CancelForNoSuitableSlot throws on TemporaryHold
        var holdRes = CreateHold(Guid.NewGuid(), Guid.NewGuid(), "51A12345", status: ReservationStatus.TemporaryHold);
        Assert.Throws<ReservationDomainException>(() => holdRes.CancelForNoSuitableSlot(Now));

        // 3. CancelForNoSuitableSlot throws on already Cancelled
        var cancelledRes = CreateHold(Guid.NewGuid(), Guid.NewGuid(), "51A12345", status: ReservationStatus.Confirmed);
        cancelledRes.CancelForNoSuitableSlot(Now);
        Assert.Throws<ReservationDomainException>(() => cancelledRes.CancelForNoSuitableSlot(Now));

        // 4. CancelForNoSuitableSlot throws on Expired
        var expiredRes = CreateHold(Guid.NewGuid(), Guid.NewGuid(), "51A12345", status: ReservationStatus.TemporaryHold);
        expiredRes.Expire();
        Assert.Throws<ReservationDomainException>(() => expiredRes.CancelForNoSuitableSlot(Now));
    }

    [Fact]
    public async Task NoMatchingReservation_ReturnsCancelledFalse()
    {
        var lotId = Guid.NewGuid();
        var handler = CreateHandler();
        var command = new CancelForNoSuitableSlotCommand(lotId, "51A99999", Now);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value!.Cancelled);
        Assert.Null(result.Value.ReservationId);
        Assert.Empty(_paymentClient.RefundCalls);
        Assert.Empty(_outbox.Events);
    }

    [Fact]
    public async Task OutsideArrivalWindow_ReturnsCancelledFalse()
    {
        var lotId = Guid.NewGuid();
        var reservationId = Guid.NewGuid();
        // Appointment is 3 hours away -> arrival window [WindowStartAt - 15m, Cutoff) does not match Now
        var res = CreateHold(reservationId, lotId, "51A12345", appointmentTime: Now.AddHours(3));
        _repo.Add(res);

        var handler = CreateHandler();
        var command = new CancelForNoSuitableSlotCommand(lotId, "51A12345", Now);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value!.Cancelled);
        Assert.Null(result.Value.ReservationId);
    }

    [Fact]
    public async Task S08_ConcurrencyConflict_WhenReloadedIsCancelledNoSuitableSlot_ReturnsCancelledTrue()
    {
        var lotId = Guid.NewGuid();
        var reservationId = Guid.NewGuid();
        var res = CreateHold(reservationId, lotId, "51A12345", prepaidAmount: 50000m);
        _repo.Add(res);

        _repo.OnSaveChanges = () =>
        {
            var concurrent = CreateHold(reservationId, lotId, "51A12345", prepaidAmount: 50000m);
            concurrent.CancelForNoSuitableSlot(Now);
            _repo.SetReloadResult(concurrent);
            throw new DbUpdateConcurrencyException();
        };

        var handler = CreateHandler();
        var command = new CancelForNoSuitableSlotCommand(lotId, "51A12345", Now);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value!.Cancelled);
        Assert.Equal(reservationId, result.Value.ReservationId);
        // Concurrency conflict path must NEVER call payment
        Assert.Empty(_paymentClient.RefundCalls);
    }

    [Fact]
    public async Task S09_ConcurrencyConflict_WhenReloadedIsUsed_ReturnsCancelledFalse()
    {
        var lotId = Guid.NewGuid();
        var reservationId = Guid.NewGuid();
        var res = CreateHold(reservationId, lotId, "51A12345", prepaidAmount: 50000m);
        _repo.Add(res);

        _repo.OnSaveChanges = () =>
        {
            var concurrent = CreateHold(reservationId, lotId, "51A12345", prepaidAmount: 50000m);
            concurrent.Use(Now, Guid.NewGuid());
            _repo.SetReloadResult(concurrent);
            throw new DbUpdateConcurrencyException();
        };

        var handler = CreateHandler();
        var command = new CancelForNoSuitableSlotCommand(lotId, "51A12345", Now);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value!.Cancelled);
        Assert.Empty(_paymentClient.RefundCalls);
    }

    [Fact]
    public async Task PlateNormalization_MatchesPlateWithDifferentSpacingAndPunctuation()
    {
        var lotId = Guid.NewGuid();
        var reservationId = Guid.NewGuid();
        // Reservation created with formatted plate
        var res = CreateHold(reservationId, lotId, "51A-123.45", prepaidAmount: 50000m);
        _repo.Add(res);

        var handler = CreateHandler();
        // Request arrives with unformatted plate "51a 12345"
        var command = new CancelForNoSuitableSlotCommand(lotId, "51a 12345", Now);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value!.Cancelled);
        Assert.Equal(reservationId, result.Value.ReservationId);
    }

    [Fact]
    public async Task S20_PaymentSlowerThanOneSecondDeadline_HandlerReturnsCancelledTrueUnderTwoSeconds_WorkerReconcilesLater()
    {
        var lotId = Guid.NewGuid();
        var reservationId = Guid.NewGuid();
        var res = CreateHold(reservationId, lotId, "51A12345", prepaidAmount: 50000m);
        _repo.Add(res);

        // Fake RefundAsync waits on its CancellationToken for 10 seconds.
        // Handler has 1-second timeout CTS which will cancel after 1s.
        _paymentClient.DelayOnRefundMs = 10_000;

        var handler = CreateHandler();
        var command = new CancelForNoSuitableSlotCommand(lotId, "51A12345", Now);

        var sw = Stopwatch.StartNew();
        var result = await handler.Handle(command, CancellationToken.None);
        sw.Stop();

        Assert.True(result.IsSuccess);
        Assert.True(result.Value!.Cancelled);
        Assert.Equal(reservationId, result.Value.ReservationId);
        Assert.True(sw.ElapsedMilliseconds < 2000, $"Handler took {sw.ElapsedMilliseconds}ms, expected under 2000ms.");

        Assert.Equal(ReservationStatus.Cancelled, res.Status);
        Assert.Null(res.PrepaidOutcome); // Timed out, pending reconciliation worker

        // Later, reconciliation worker runs with responsive payment client -> exactly 1 refund
        _paymentClient.DelayOnRefundMs = 0;
        _paymentClient.RefundCalls.Clear();
        _paymentClient.RefundResultToReturn = Result<ReservationDebitState>.Success(ReservationDebitState.Refunded);

        var worker = CreateWorker();
        var processed = await worker.ReconcileBatchAsync(CancellationToken.None);

        Assert.Equal(1, processed);
        var call = Assert.Single(_paymentClient.RefundCalls);
        Assert.Equal(reservationId, call.ReservationId);
        Assert.Equal(50000, call.AmountVnd);
        Assert.Equal("NoSuitableSlot", call.Reason);
        Assert.Equal(PrepaidOutcome.Refunded, res.PrepaidOutcome);
    }

    // -------------------------------------------------------------------------
    // Doubles
    // -------------------------------------------------------------------------
    private sealed class DbUpdateConcurrencyException : Exception { }

    private sealed class FakeReservationRepository : IReservationRepository
    {
        private readonly List<Reservation> _list = [];
        private Reservation? _reloadResult;
        public Action? OnSaveChanges { get; set; }
        public int FindCancelledCallsCount { get; private set; }

        public void Add(Reservation r) => _list.Add(r);
        public void SetReloadResult(Reservation r) => _reloadResult = r;

        public Task<Reservation?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(_reloadResult ?? _list.FirstOrDefault(r => r.Id == id));

        public Task<IReadOnlyList<Reservation>> ListByUserIdAsync(Guid userId, ReservationStatus? status, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Reservation>>(_list.Where(r => r.UserId == userId && (!status.HasValue || r.Status == status.Value)).ToList());

        public Task<IReadOnlyList<Reservation>> ListActiveByLotIdAsync(Guid lotId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Reservation>>(_list.Where(r => r.LotId == lotId && (r.Status == ReservationStatus.TemporaryHold || r.Status == ReservationStatus.Confirmed)).ToList());

        public Task<Reservation?> FindActiveConfirmedByLotAndPlateAsync(Guid lotId, string plateNumber, DateTimeOffset arrivalTime, CancellationToken cancellationToken = default)
        {
            var normalized = LicensePlateNormalizer.Normalize(plateNumber);
            var utcArrival = arrivalTime.ToUniversalTime();

            var candidate = _list.FirstOrDefault(r =>
                r.LotId == lotId &&
                LicensePlateNormalizer.Normalize(r.PlateNumber) == normalized &&
                r.Status == ReservationStatus.Confirmed &&
                utcArrival >= r.WindowStartAt.AddMinutes(-15) &&
                utcArrival < Reservation.GetNoShowCutoff(r.AppointmentTime));

            return Task.FromResult(candidate);
        }

        public Task<Reservation?> FindCancelledNoSuitableSlotByLotAndPlateAsync(Guid lotId, string plateNumber, DateTimeOffset arrivalTime, CancellationToken cancellationToken = default)
        {
            FindCancelledCallsCount++;
            var normalized = LicensePlateNormalizer.Normalize(plateNumber);
            var utcArrival = arrivalTime.ToUniversalTime();

            var candidate = _list.FirstOrDefault(r =>
                r.LotId == lotId &&
                LicensePlateNormalizer.Normalize(r.PlateNumber) == normalized &&
                r.Status == ReservationStatus.Cancelled &&
                r.CancelReason == CancelReason.NoSuitableSlot &&
                utcArrival >= r.WindowStartAt.AddMinutes(-15) &&
                utcArrival < Reservation.GetNoShowCutoff(r.AppointmentTime));

            return Task.FromResult(candidate);
        }

        public Task ReloadAsync(Reservation reservation, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void DiscardPendingChanges() { }

        public Task<IReadOnlyList<Reservation>> ListPaymentReconciliationCandidatesAsync(DateTimeOffset now, int batchSize, CancellationToken cancellationToken = default)
        {
            var candidates = _list.Where(r =>
                (r.PrepaidAmount > 0m &&
                 r.ConfirmedAt == null &&
                 r.PrepaidOutcome == null &&
                 (r.Status == ReservationStatus.TemporaryHold ||
                  r.Status == ReservationStatus.Expired ||
                  r.Status == ReservationStatus.Cancelled))
                ||
                (r.Status == ReservationStatus.Cancelled &&
                 (r.CancelReason == CancelReason.CustomerCancelled ||
                  r.CancelReason == CancelReason.NoSuitableSlot) &&
                 r.ConfirmedAt != null &&
                 r.PrepaidAmount > 0m &&
                 r.PrepaidOutcome == null))
                .Take(batchSize)
                .ToList();

            return Task.FromResult<IReadOnlyList<Reservation>>(candidates);
        }

        public Task<IReadOnlyList<Reservation>> ListNoShowCandidatesAsync(DateTimeOffset now, int batchSize, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<Reservation>>([]);

        public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            OnSaveChanges?.Invoke();
            return Task.CompletedTask;
        }
    }

    private sealed class FakeReservationPaymentClient : IReservationPaymentClient
    {
        public List<(Guid ReservationId, Guid AccountId, long AmountVnd, string Reason)> RefundCalls { get; } = [];
        public Result<ReservationDebitState> RefundResultToReturn { get; set; } = Result<ReservationDebitState>.Success(ReservationDebitState.Refunded);
        public bool ThrowOnRefund { get; set; }
        public int DelayOnRefundMs { get; set; }

        public Task<Result<ReservationDebitResult>> DebitAsync(Guid reservationId, Guid accountId, Guid lotId, long amountVnd, PrepaymentPurpose purpose = PrepaymentPurpose.Deposit, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result<ReservationDebitResult>.Failure(ReservationPaymentErrors.Unavailable));

        public Task<Result<ReservationDebitState>> GetDebitStateAsync(Guid reservationId, Guid accountId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result<ReservationDebitState>.Failure(ReservationPaymentErrors.Unavailable));

        public async Task<Result<ReservationDebitState>> RefundAsync(Guid reservationId, Guid accountId, long amountVnd, string reason, CancellationToken cancellationToken = default)
        {
            if (DelayOnRefundMs > 0)
            {
                await Task.Delay(DelayOnRefundMs, cancellationToken);
            }

            if (ThrowOnRefund)
            {
                throw new InvalidOperationException("Payment client timeout simulation");
            }

            RefundCalls.Add((reservationId, accountId, amountVnd, reason));
            return RefundResultToReturn;
        }
    }

    private sealed class RecordingOutbox : IOutboxWriter
    {
        public List<object> Events { get; } = [];

        public void Enqueue<TEvent>(TEvent integrationEvent, string eventType, int eventVersion,
            Guid? correlationId = null, Guid? causationId = null) where TEvent : class =>
            Events.Add(integrationEvent);
    }

    private sealed class FakeClock(DateTimeOffset initial) : IClock
    {
        public DateTimeOffset UtcNow => initial;
    }
}
