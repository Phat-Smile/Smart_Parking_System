using Microsoft.Extensions.Logging.Abstractions;
using ParkingSystem.Booking.Application.Common.Messaging.Outbox;
using ParkingSystem.Booking.Application.Payments;
using ParkingSystem.Booking.Application.Reservations;
using ParkingSystem.Booking.Application.Reservations.Commands.CancelReservation;
using ParkingSystem.Booking.Domain.Reservations;
using ParkingSystem.BuildingBlocks.Results;
using ParkingSystem.BuildingBlocks.Time;
using ParkingSystem.Contracts.Events.Booking;
using Xunit;

namespace ParkingSystem.Tests.Booking.UnitTests.Reservations;

public sealed class ReservationCancellationRefundTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 10, 10, 0, 0, TimeSpan.Zero);

    private readonly FakeReservationRepository _repo = new();
    private readonly FakeReservationPaymentClient _paymentClient = new();
    private readonly RecordingOutbox _outbox = new();
    private readonly FakeClock _clock = new(Now);

    private CancelReservationCommandHandler CreateHandler() =>
        new(_repo, _paymentClient, _clock, NullLogger<CancelReservationCommandHandler>.Instance, _outbox);

    private static Reservation CreateHold(
        Guid id,
        Guid userId,
        ReservationStatus status = ReservationStatus.Confirmed,
        decimal prepaidAmount = 50000m,
        DateTimeOffset? appointmentTime = null)
    {
        var appTime = appointmentTime ?? Now.AddHours(2);
        var res = Reservation.CreateTemporaryHold(
            id,
            userId,
            Guid.NewGuid(),
            "51A12345",
            Guid.NewGuid(),
            VehicleGroup.Car,
            ReservationType.Scheduled,
            appTime,
            prepaidAmount,
            Now.AddHours(-1),
            durationMinutes: 120,
            tempHoldExpiresAt: Now.AddHours(1));

        if (status == ReservationStatus.Confirmed)
        {
            res.Confirm(Now.AddMinutes(-30));
        }

        return res;
    }

    [Fact]
    public async Task CancelReservation_ConfirmedWithDeposit_BeforeAppointment_RefundsAndCancels()
    {
        var userId = Guid.NewGuid();
        var resId = Guid.NewGuid();
        var res = CreateHold(resId, userId, ReservationStatus.Confirmed, prepaidAmount: 50000m, appointmentTime: Now.AddHours(1));
        _repo.Add(res);

        var handler = CreateHandler();
        var command = new CancelReservationCommand(resId, userId, "Customer cancelled early");

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Cancelled", result.Value!.Status);
        Assert.Equal(50000m, result.Value.RefundAmount);
        Assert.Contains("hoàn về ví", result.Value.Message);

        // Verify entity state
        Assert.Equal(ReservationStatus.Cancelled, res.Status);
        Assert.Equal(CancelReason.CustomerCancelled, res.CancelReason);
        Assert.Equal(PrepaidOutcome.Refunded, res.PrepaidOutcome);
        Assert.Equal(Now, res.CancelledAt);

        // Verify payment client called
        var refundCall = Assert.Single(_paymentClient.RefundCalls);
        Assert.Equal(resId, refundCall.ReservationId);
        Assert.Equal(userId, refundCall.AccountId);
        Assert.Equal(50000L, refundCall.AmountVnd);
        Assert.Equal("CustomerCancelled", refundCall.Reason);

        // Verify outbox event
        var @event = Assert.Single(_outbox.Events);
        var cancelledEvent = Assert.IsType<ReservationCancelledIntegrationEvent>(@event);
        Assert.Equal(resId, cancelledEvent.ReservationId);
        Assert.Equal(userId, cancelledEvent.UserId);
        Assert.Equal(50000m, cancelledEvent.RefundAmount);
        Assert.Equal(CancelReason.CustomerCancelled.ToString(), cancelledEvent.CancelReason);
    }

    [Fact]
    public async Task CancelReservation_Confirmed_AfterAppointmentTime_ReturnsCannotCancel()
    {
        var userId = Guid.NewGuid();
        var resId = Guid.NewGuid();
        // Appointment was 5 minutes ago
        var res = CreateHold(resId, userId, ReservationStatus.Confirmed, prepaidAmount: 50000m, appointmentTime: Now.AddMinutes(-5));
        _repo.Add(res);

        var handler = CreateHandler();
        var command = new CancelReservationCommand(resId, userId, "Too late");

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ReservationErrors.CannotCancel, result.Error);
        Assert.Equal(ReservationStatus.Confirmed, res.Status);
        Assert.Empty(_paymentClient.RefundCalls);
        Assert.Empty(_outbox.Events);
    }

    [Fact]
    public async Task CancelReservation_TemporaryHold_CancelsWithoutRefundCall()
    {
        var userId = Guid.NewGuid();
        var resId = Guid.NewGuid();
        var res = CreateHold(resId, userId, ReservationStatus.TemporaryHold, prepaidAmount: 50000m);
        _repo.Add(res);

        var handler = CreateHandler();
        var command = new CancelReservationCommand(resId, userId, "No longer needed");

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Cancelled", result.Value!.Status);
        Assert.Equal(0m, result.Value.RefundAmount);
        Assert.Equal(ReservationStatus.Cancelled, res.Status);
        Assert.Null(res.PrepaidOutcome);
        Assert.Empty(_paymentClient.RefundCalls);

        var @event = Assert.Single(_outbox.Events);
        var cancelledEvent = Assert.IsType<ReservationCancelledIntegrationEvent>(@event);
        Assert.Equal(0m, cancelledEvent.RefundAmount);
    }

    [Fact]
    public async Task CancelReservation_WhenRefundFails_ReturnsPaymentUnconfirmed()
    {
        var userId = Guid.NewGuid();
        var resId = Guid.NewGuid();
        var res = CreateHold(resId, userId, ReservationStatus.Confirmed, prepaidAmount: 50000m, appointmentTime: Now.AddHours(1));
        _repo.Add(res);

        _paymentClient.RefundResultToReturn = Result<ReservationDebitState>.Failure(ReservationPaymentErrors.Unavailable);

        var handler = CreateHandler();
        var command = new CancelReservationCommand(resId, userId, "Customer cancelled early");

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ReservationErrors.PaymentUnconfirmed, result.Error);
        Assert.Equal(ReservationStatus.Confirmed, res.Status);
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
            Task.FromResult(_list.FirstOrDefault(r => r.LotId == lotId && r.PlateNumber == plateNumber && r.Status == ReservationStatus.Confirmed));

        public Task ReloadAsync(Reservation reservation, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public void DiscardPendingChanges() { }

        public Task<IReadOnlyList<Reservation>> ListPaymentReconciliationCandidatesAsync(DateTimeOffset now, int batchSize, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Reservation>>([]);

        public Task<IReadOnlyList<Reservation>> ListNoShowCandidatesAsync(DateTimeOffset now, int batchSize, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Reservation>>([]);

        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeReservationPaymentClient : IReservationPaymentClient
    {
        public List<(Guid ReservationId, Guid AccountId, long AmountVnd, string Reason)> RefundCalls { get; } = [];
        public Result<ReservationDebitState> RefundResultToReturn { get; set; } = Result<ReservationDebitState>.Success(ReservationDebitState.Refunded);

        public Task<Result<ReservationDebitResult>> DebitAsync(Guid reservationId, Guid accountId, Guid lotId, long amountVnd, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result<ReservationDebitResult>.Failure(ReservationPaymentErrors.Unavailable));

        public Task<Result<ReservationDebitState>> GetDebitStateAsync(Guid reservationId, Guid accountId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result<ReservationDebitState>.Failure(ReservationPaymentErrors.Unavailable));

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
        public DateTimeOffset UtcNow { get; set; } = initial;
    }
}
