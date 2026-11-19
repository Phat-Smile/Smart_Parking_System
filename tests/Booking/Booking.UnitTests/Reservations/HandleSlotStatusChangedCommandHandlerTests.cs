using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ParkingSystem.Booking.Application.Common.Messaging.Outbox;
using ParkingSystem.Booking.Application.Payments;
using ParkingSystem.Booking.Application.Reservations;
using ParkingSystem.Booking.Application.Reservations.Commands.HandleSlotStatusChanged;
using ParkingSystem.Booking.Domain.Reservations;
using ParkingSystem.Booking.Infrastructure.Payments;
using ParkingSystem.BuildingBlocks.Results;
using ParkingSystem.BuildingBlocks.Time;
using ParkingSystem.Contracts.Events.Booking;
using Xunit;

namespace ParkingSystem.Tests.Booking.UnitTests.Reservations;

public sealed class HandleSlotStatusChangedCommandHandlerTests
{
    private static readonly DateTimeOffset FixedNow = new(2026, 11, 18, 10, 0, 0, TimeSpan.Zero);

    private readonly FakeReservationRepository _repo = new();
    private readonly FakeReservationPaymentClient _paymentClient = new();
    private readonly RecordingOutbox _outbox = new();
    private readonly FakeClock _clock = new(FixedNow);

    private HandleSlotStatusChangedCommandHandler CreateHandler() =>
        new(_repo, _paymentClient, _clock, NullLogger<HandleSlotStatusChangedCommandHandler>.Instance, _outbox);

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

    private static Reservation CreateHold(Guid lotId, decimal prepaidAmount = 50000m)
    {
        return Reservation.CreateTemporaryHold(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "51A12345",
            lotId,
            VehicleGroup.Car,
            ReservationType.Scheduled,
            FixedNow.AddHours(1),
            prepaidAmount,
            FixedNow);
    }

    [Fact]
    public async Task S15_WhenSlotEntersMaintenance_ConfirmedReservation_CancelsAndRefundsSuccessfully()
    {
        var lotId = Guid.NewGuid();
        var reservation = CreateHold(lotId, prepaidAmount: 50000m);
        reservation.Confirm(FixedNow.AddMinutes(5));
        _repo.Add(reservation);

        var handler = CreateHandler();
        var command = new HandleSlotStatusChangedCommand(
            lotId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            "A-01",
            "Maintenance",
            "Available",
            "Sensor",
            FixedNow,
            1);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value!.CancelledReservationsCount);
        Assert.Equal(ReservationStatus.Cancelled, reservation.Status);
        Assert.Equal(CancelReason.NoSuitableSlot, reservation.CancelReason);
        Assert.Equal(PrepaidOutcome.Refunded, reservation.PrepaidOutcome);
        Assert.Contains("Tiền đã được hoàn về ví", result.Value.Message);

        var evt = Assert.Single(_outbox.Events.OfType<ReservationCancelledIntegrationEvent>());
        Assert.Equal(reservation.Id, evt.ReservationId);
        Assert.Equal("NoSuitableSlot", evt.CancelReason);
        Assert.Equal(50000m, evt.RefundAmount);

        var call = Assert.Single(_paymentClient.RefundCalls);
        Assert.Equal(reservation.Id, call.ReservationId);
        Assert.Equal(50000, call.AmountVnd);
    }

    [Fact]
    public async Task S16_WhenSlotEntersMaintenance_RefundAsyncFails_WorkerReconcilesRefund()
    {
        var lotId = Guid.NewGuid();
        var reservation = CreateHold(lotId, prepaidAmount: 50000m);
        reservation.Confirm(FixedNow.AddMinutes(5));
        _repo.Add(reservation);

        _paymentClient.RefundResultToReturn = Result<ReservationDebitState>.Failure(ReservationPaymentErrors.Unavailable);

        var handler = CreateHandler();
        var command = new HandleSlotStatusChangedCommand(
            lotId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            "A-01",
            "Maintenance",
            "Available",
            "Sensor",
            FixedNow,
            1);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value!.CancelledReservationsCount);
        Assert.Equal(ReservationStatus.Cancelled, reservation.Status);
        Assert.Null(reservation.PrepaidOutcome); // Pending worker
        Assert.Contains("Tiền hoàn đang được xử lý", result.Value.Message);

        // Run worker with responsive payment client -> exactly 1 refund, outcome Refunded
        _paymentClient.RefundCalls.Clear();
        _paymentClient.RefundResultToReturn = Result<ReservationDebitState>.Success(ReservationDebitState.Refunded);

        var worker = CreateWorker();
        var processed = await worker.ReconcileBatchAsync(CancellationToken.None);

        Assert.Equal(1, processed);
        var call = Assert.Single(_paymentClient.RefundCalls);
        Assert.Equal(reservation.Id, call.ReservationId);
        Assert.Equal(50000, call.AmountVnd);
        Assert.Equal("NoSuitableSlot", call.Reason);
        Assert.Equal(PrepaidOutcome.Refunded, reservation.PrepaidOutcome);
    }

    [Fact]
    public async Task S17_WhenSlotEntersMaintenance_TemporaryHold_WorkerRefundsOnlyWhenDebited_NeverWhenNotDebited()
    {
        var lotId = Guid.NewGuid();
        var hold1 = CreateHold(lotId, prepaidAmount: 50000m); // In TemporaryHold, not yet Confirmed
        _repo.Add(hold1);

        var handler = CreateHandler();
        var command1 = new HandleSlotStatusChangedCommand(
            lotId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            "A-01",
            "Maintenance",
            "Available",
            "Sensor",
            FixedNow,
            1);

        var result1 = await handler.Handle(command1, CancellationToken.None);

        Assert.True(result1.IsSuccess);
        Assert.Equal(1, result1.Value!.CancelledReservationsCount);
        Assert.Equal(ReservationStatus.Cancelled, hold1.Status);
        Assert.Equal(CancelReason.NoSuitableSlot, hold1.CancelReason);
        Assert.Null(hold1.PrepaidOutcome);

        Assert.Empty(_paymentClient.RefundCalls);
        var evt = Assert.Single(_outbox.Events.OfType<ReservationCancelledIntegrationEvent>());
        Assert.Equal(0m, evt.RefundAmount);

        // Case 1: Payment client GetDebitState returns Debited -> worker refunds exactly once, outcome Refunded
        _paymentClient.DebitStateResultToReturn = Result<ReservationDebitState>.Success(ReservationDebitState.Debited);
        _paymentClient.RefundResultToReturn = Result<ReservationDebitState>.Success(ReservationDebitState.Refunded);

        var worker = CreateWorker();
        var processed1 = await worker.ReconcileBatchAsync(CancellationToken.None);

        Assert.Equal(1, processed1);
        var call = Assert.Single(_paymentClient.RefundCalls);
        Assert.Equal(hold1.Id, call.ReservationId);
        Assert.Equal(50000, call.AmountVnd);
        Assert.Equal("ReconciliationRefund", call.Reason);
        Assert.Equal(PrepaidOutcome.Refunded, hold1.PrepaidOutcome);

        // Case 2: Another TemporaryHold cancelled by slot status, but GetDebitState returns NotDebited -> 0 refunds
        var hold2 = CreateHold(lotId, prepaidAmount: 50000m);
        _repo.Add(hold2);

        var command2 = new HandleSlotStatusChangedCommand(
            lotId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            "A-02",
            "Maintenance",
            "Available",
            "Sensor",
            FixedNow,
            2);

        var result2 = await handler.Handle(command2, CancellationToken.None);
        Assert.True(result2.IsSuccess);
        Assert.Equal(1, result2.Value!.CancelledReservationsCount);

        _paymentClient.RefundCalls.Clear();
        _paymentClient.DebitStateResultToReturn = Result<ReservationDebitState>.Success(ReservationDebitState.NotDebited);

        var processed2 = await worker.ReconcileBatchAsync(CancellationToken.None);
        Assert.Equal(1, processed2);
        Assert.Empty(_paymentClient.RefundCalls); // NEVER refunds when NotDebited
        Assert.Null(hold2.PrepaidOutcome);
    }

    [Fact]
    public async Task S18_WhenSlotBecomesAvailable_NonDisruptive_DoesNotCancelAnyReservations()
    {
        var lotId = Guid.NewGuid();
        var hold = CreateHold(lotId);
        _repo.Add(hold);

        var handler = CreateHandler();
        var command = new HandleSlotStatusChangedCommand(
            lotId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            "A-01",
            "Available",
            "Maintenance",
            "Sensor",
            FixedNow,
            2);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.Value!.CancelledReservationsCount);
        Assert.Equal(ReservationStatus.TemporaryHold, hold.Status);
        Assert.Empty(_paymentClient.RefundCalls);
        Assert.Empty(_outbox.Events);
    }

    // -------------------------------------------------------------------------
    // Doubles
    // -------------------------------------------------------------------------
    private sealed class FakeReservationRepository : IReservationRepository
    {
        private readonly List<Reservation> _list = [];

        public void Add(Reservation r) => _list.Add(r);

        public Task<Reservation?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(_list.FirstOrDefault(r => r.Id == id));

        public Task<IReadOnlyList<Reservation>> ListByUserIdAsync(Guid userId, ReservationStatus? status, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Reservation>>(_list.Where(r => r.UserId == userId && (!status.HasValue || r.Status == status.Value)).ToList());

        public Task<IReadOnlyList<Reservation>> ListActiveByLotIdAsync(Guid lotId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Reservation>>(_list.Where(r => r.LotId == lotId && (r.Status == ReservationStatus.TemporaryHold || r.Status == ReservationStatus.Confirmed)).ToList());

        public Task<Reservation?> FindActiveConfirmedByLotAndPlateAsync(Guid lotId, string plateNumber, DateTimeOffset arrivalTime, CancellationToken cancellationToken = default) =>
            Task.FromResult<Reservation?>(null);

        public Task<Reservation?> FindCancelledNoSuitableSlotByLotAndPlateAsync(Guid lotId, string plateNumber, DateTimeOffset arrivalTime, CancellationToken cancellationToken = default) =>
            Task.FromResult<Reservation?>(null);

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

        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeReservationPaymentClient : IReservationPaymentClient
    {
        public List<(Guid ReservationId, Guid AccountId, long AmountVnd, string Reason)> RefundCalls { get; } = [];
        public Result<ReservationDebitState> RefundResultToReturn { get; set; } = Result<ReservationDebitState>.Success(ReservationDebitState.Refunded);
        public Result<ReservationDebitState> DebitStateResultToReturn { get; set; } = Result<ReservationDebitState>.Failure(ReservationPaymentErrors.Unavailable);

        public Task<Result<ReservationDebitResult>> DebitAsync(Guid reservationId, Guid accountId, Guid lotId, long amountVnd, PrepaymentPurpose purpose = PrepaymentPurpose.Deposit, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result<ReservationDebitResult>.Failure(ReservationPaymentErrors.Unavailable));

        public Task<Result<ReservationDebitState>> GetDebitStateAsync(Guid reservationId, Guid accountId, CancellationToken cancellationToken = default) =>
            Task.FromResult(DebitStateResultToReturn);

        public Task<Result<ReservationDebitState>> RefundAsync(Guid reservationId, Guid accountId, long amountVnd, string reason, CancellationToken cancellationToken = default)
        {
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

