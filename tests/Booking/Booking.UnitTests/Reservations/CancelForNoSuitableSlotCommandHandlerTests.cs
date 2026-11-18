using Microsoft.Extensions.Logging.Abstractions;
using ParkingSystem.Booking.Application.Common.Messaging.Outbox;
using ParkingSystem.Booking.Application.Payments;
using ParkingSystem.Booking.Application.Reservations;
using ParkingSystem.Booking.Application.Reservations.Commands.CancelForNoSuitableSlot;
using ParkingSystem.Booking.Domain.Reservations;
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
    public async Task S04_RefundAsyncThrowsException_LeavesOutcomeNullAndReturnsCancelledTrue()
    {
        var lotId = Guid.NewGuid();
        var reservationId = Guid.NewGuid();
        var res = CreateHold(reservationId, lotId, "51A12345", prepaidAmount: 50000m);
        _repo.Add(res);

        _paymentClient.ThrowOnRefund = true;

        var handler = CreateHandler();
        var command = new CancelForNoSuitableSlotCommand(lotId, "51A12345", Now);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value!.Cancelled);
        Assert.Equal(reservationId, result.Value.ReservationId);

        Assert.Equal(ReservationStatus.Cancelled, res.Status);
        Assert.Null(res.PrepaidOutcome);
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

        Assert.Empty(_paymentClient.RefundCalls);
        Assert.Empty(_outbox.Events);
    }

    [Fact]
    public async Task S06_NoMatchingReservation_ReturnsCancelledFalse()
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
    public async Task S07_OutsideArrivalWindow_ReturnsCancelledFalse()
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
    public async Task S20_PlateNormalization_MatchesPlateWithDifferentSpacingAndPunctuation()
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

    // -------------------------------------------------------------------------
    // Doubles
    // -------------------------------------------------------------------------
    private sealed class DbUpdateConcurrencyException : Exception { }

    private sealed class FakeReservationRepository : IReservationRepository
    {
        private readonly List<Reservation> _list = [];
        private Reservation? _reloadResult;
        public Action? OnSaveChanges { get; set; }

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
        public Task<IReadOnlyList<Reservation>> ListPaymentReconciliationCandidatesAsync(DateTimeOffset now, int batchSize, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<Reservation>>([]);
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

        public Task<Result<ReservationDebitResult>> DebitAsync(Guid reservationId, Guid accountId, Guid lotId, long amountVnd, PrepaymentPurpose purpose = PrepaymentPurpose.Deposit, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result<ReservationDebitResult>.Failure(ReservationPaymentErrors.Unavailable));

        public Task<Result<ReservationDebitState>> GetDebitStateAsync(Guid reservationId, Guid accountId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result<ReservationDebitState>.Failure(ReservationPaymentErrors.Unavailable));

        public Task<Result<ReservationDebitState>> RefundAsync(Guid reservationId, Guid accountId, long amountVnd, string reason, CancellationToken cancellationToken = default)
        {
            if (ThrowOnRefund)
            {
                throw new InvalidOperationException("Payment client timeout simulation");
            }

            RefundCalls.Add((reservationId, accountId, amountVnd, reason));
            return Task.FromResult(RefundResultToReturn);
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
