using Microsoft.Extensions.Logging.Abstractions;
using ParkingSystem.Booking.Application.Common.Messaging.Outbox;
using ParkingSystem.Booking.Application.Payments;
using ParkingSystem.Booking.Application.Reservations;
using ParkingSystem.Booking.Application.Reservations.Cancellation;
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
        DateTimeOffset? appointmentTime = null,
        ReservationType reservationType = ReservationType.HoldWhileDriving,
        DateTimeOffset? confirmedAt = null)
    {
        var appTime = appointmentTime ?? Now.AddHours(1);
        var res = Reservation.CreateTemporaryHold(
            id,
            userId,
            Guid.NewGuid(),
            "51A12345",
            Guid.NewGuid(),
            VehicleGroup.Car,
            reservationType,
            appTime,
            prepaidAmount,
            Now.AddHours(-1),
            durationMinutes: reservationType == ReservationType.Scheduled ? 120 : null,
            tempHoldExpiresAt: Now.AddHours(1));

        if (status == ReservationStatus.Confirmed)
        {
            res.Confirm(confirmedAt ?? Now);
        }

        return res;
    }

    [Fact]
    public async Task C06_01_HWD_Confirmed_Cancel_At_4Min59Sec_Refunds_And_Sets_Outcome_Refunded()
    {
        var userId = Guid.NewGuid();
        var resId = Guid.NewGuid();
        var res = CreateHold(resId, userId, ReservationStatus.Confirmed, prepaidAmount: 50000m, confirmedAt: Now);
        _repo.Add(res);

        _clock.UtcNow = Now.AddMinutes(4).AddSeconds(59);
        var handler = CreateHandler();
        var command = new CancelReservationCommand(resId, userId, "HWD cancel within 5 minutes");

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Cancelled", result.Value!.Status);
        Assert.Equal(50000m, result.Value.RefundAmount);
        Assert.Equal("Đặt chỗ đã được hủy. Tiền cọc đã được hoàn về ví của bạn.", result.Value.Message);

        Assert.Equal(ReservationStatus.Cancelled, res.Status);
        Assert.Equal(CancelReason.CustomerCancelled, res.CancelReason);
        Assert.Equal(PrepaidOutcome.Refunded, res.PrepaidOutcome);
        Assert.Equal(_clock.UtcNow, res.CancelledAt);

        var refundCall = Assert.Single(_paymentClient.RefundCalls);
        Assert.Equal(resId, refundCall.ReservationId);
        Assert.Equal(userId, refundCall.AccountId);
        Assert.Equal(50000L, refundCall.AmountVnd);
        Assert.Equal("CustomerCancelled", refundCall.Reason);

        var @event = Assert.Single(_outbox.Events);
        var cancelledEvent = Assert.IsType<ReservationCancelledIntegrationEvent>(@event);
        Assert.Equal(resId, cancelledEvent.ReservationId);
        Assert.Equal(userId, cancelledEvent.UserId);
        Assert.Equal(50000m, cancelledEvent.RefundAmount);
        Assert.Equal(CancelReason.CustomerCancelled.ToString(), cancelledEvent.CancelReason);
    }

    [Fact]
    public async Task C06_02_HWD_Cancel_At_Exactly_5Min00Sec_Forfeited_NoRefundCall_EventZero()
    {
        var userId = Guid.NewGuid();
        var resId = Guid.NewGuid();
        var res = CreateHold(resId, userId, ReservationStatus.Confirmed, prepaidAmount: 50000m, confirmedAt: Now);
        _repo.Add(res);

        _clock.UtcNow = Now.AddMinutes(5);
        var handler = CreateHandler();
        var command = new CancelReservationCommand(resId, userId, "HWD cancel at 5:00");

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Cancelled", result.Value!.Status);
        Assert.Equal(0m, result.Value.RefundAmount);
        Assert.Equal("Đặt chỗ đã được hủy. Bạn hủy sau 5 phút kể từ lúc xác nhận nên không được hoàn cọc.", result.Value.Message);

        Assert.Equal(ReservationStatus.Cancelled, res.Status);
        Assert.Equal(CancelReason.CustomerCancelled, res.CancelReason);
        Assert.Equal(PrepaidOutcome.Forfeited, res.PrepaidOutcome);

        Assert.Empty(_paymentClient.RefundCalls);

        var @event = Assert.Single(_outbox.Events);
        var cancelledEvent = Assert.IsType<ReservationCancelledIntegrationEvent>(@event);
        Assert.Equal(resId, cancelledEvent.ReservationId);
        Assert.Equal(0m, cancelledEvent.RefundAmount);
    }

    [Fact]
    public async Task C06_03_HWD_Cancel_At_20Minutes_Forfeited_NoRefundCall()
    {
        var userId = Guid.NewGuid();
        var resId = Guid.NewGuid();
        var res = CreateHold(resId, userId, ReservationStatus.Confirmed, prepaidAmount: 50000m, confirmedAt: Now);
        _repo.Add(res);

        _clock.UtcNow = Now.AddMinutes(20);
        var handler = CreateHandler();
        var command = new CancelReservationCommand(resId, userId, "HWD cancel after 20 minutes");

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Cancelled", result.Value!.Status);
        Assert.Equal(0m, result.Value.RefundAmount);
        Assert.Equal(PrepaidOutcome.Forfeited, res.PrepaidOutcome);
        Assert.Empty(_paymentClient.RefundCalls);
    }

    [Fact]
    public async Task C06_04_HWD_Cancel_After_AppointmentTime_NotUsed_Allowed_Forfeited()
    {
        var userId = Guid.NewGuid();
        var resId = Guid.NewGuid();
        // AppointmentTime was 10 minutes after confirmation, now is 15 minutes after confirmation
        var appointmentTime = Now.AddMinutes(10);
        var res = CreateHold(resId, userId, ReservationStatus.Confirmed, prepaidAmount: 50000m, appointmentTime: appointmentTime, confirmedAt: Now);
        _repo.Add(res);

        _clock.UtcNow = Now.AddMinutes(15);
        var handler = CreateHandler();
        var command = new CancelReservationCommand(resId, userId, "Cancel after appointment time");

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Cancelled", result.Value!.Status);
        Assert.Equal(ReservationStatus.Cancelled, res.Status);
        Assert.Equal(PrepaidOutcome.Forfeited, res.PrepaidOutcome);
        Assert.Empty(_paymentClient.RefundCalls);
    }

    [Fact]
    public async Task C06_05_Used_CannotCancel_NoPaymentCall_NoEvent()
    {
        var userId = Guid.NewGuid();
        var resId = Guid.NewGuid();
        var res = CreateHold(resId, userId, ReservationStatus.Confirmed, prepaidAmount: 50000m, confirmedAt: Now);
        res.Use(Now.AddMinutes(3));
        _repo.Add(res);

        var handler = CreateHandler();
        var command = new CancelReservationCommand(resId, userId, "Cancel used");

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ReservationErrors.CannotCancel, result.Error);
        Assert.Equal(ReservationStatus.Used, res.Status);
        Assert.Empty(_paymentClient.RefundCalls);
        Assert.Empty(_outbox.Events);
    }

    [Fact]
    public async Task C06_06_Cancelled_Or_Expired_CannotCancel()
    {
        var userId = Guid.NewGuid();
        var res1 = CreateHold(Guid.NewGuid(), userId, ReservationStatus.Confirmed, prepaidAmount: 50000m, confirmedAt: Now);
        res1.Cancel(Now.AddMinutes(2), CancelReason.CustomerCancelled, PrepaidOutcome.Forfeited);
        _repo.Add(res1);

        var res2 = CreateHold(Guid.NewGuid(), userId, ReservationStatus.TemporaryHold);
        res2.Expire();
        _repo.Add(res2);

        var handler = CreateHandler();

        var result1 = await handler.Handle(new CancelReservationCommand(res1.Id, userId, "Cancel already cancelled"), CancellationToken.None);
        Assert.True(result1.IsFailure);
        Assert.Equal(ReservationErrors.CannotCancel, result1.Error);

        var result2 = await handler.Handle(new CancelReservationCommand(res2.Id, userId, "Cancel expired"), CancellationToken.None);
        Assert.True(result2.IsFailure);
        Assert.Equal(ReservationErrors.CannotCancel, result2.Error);
    }

    [Fact]
    public async Task C06_07_TemporaryHold_Cancelled_NoPaymentCall()
    {
        var userId = Guid.NewGuid();
        var resId = Guid.NewGuid();
        var res = CreateHold(resId, userId, ReservationStatus.TemporaryHold, prepaidAmount: 50000m);
        _repo.Add(res);

        var handler = CreateHandler();
        var command = new CancelReservationCommand(resId, userId, "Cancel temp hold");

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
    public async Task C06_08_HWD_PrepaidAmountZero_Cancelled_NoPaymentCall_OutcomeNull()
    {
        var userId = Guid.NewGuid();
        var resId = Guid.NewGuid();
        var res = CreateHold(resId, userId, ReservationStatus.Confirmed, prepaidAmount: 0m, confirmedAt: Now);
        _repo.Add(res);

        var handler = CreateHandler();
        var command = new CancelReservationCommand(resId, userId, "HWD 0 prepaid");

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Cancelled", result.Value!.Status);
        Assert.Equal(0m, result.Value.RefundAmount);
        Assert.Equal(ReservationStatus.Cancelled, res.Status);
        Assert.Null(res.PrepaidOutcome);
        Assert.Empty(_paymentClient.RefundCalls);
    }

    [Fact]
    public async Task C06_09_Save_Throws_DbUpdateConcurrencyException_ReloadedStatusUsed_CannotCancel_NoPaymentCall()
    {
        var userId = Guid.NewGuid();
        var resId = Guid.NewGuid();
        var res = CreateHold(resId, userId, ReservationStatus.Confirmed, prepaidAmount: 50000m, confirmedAt: Now);
        _repo.Add(res);

        var usedRes = CreateHold(resId, userId, ReservationStatus.Confirmed, prepaidAmount: 50000m, confirmedAt: Now);
        usedRes.Use(Now);
        _repo.FindByIdOverride = _ => Task.FromResult<Reservation?>(usedRes);
        _repo.OnSaveChanges = () => throw new DbUpdateConcurrencyException();

        var handler = CreateHandler();
        var command = new CancelReservationCommand(resId, userId, "Concurrent cancel vs check-in");

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ReservationErrors.CannotCancel, result.Error);
        Assert.Empty(_paymentClient.RefundCalls);
    }

    [Fact]
    public async Task C06_10_Refund_Fails_After_Save_Result_Success_StatusCancelled_OutcomeNull_MessagePending()
    {
        var userId = Guid.NewGuid();
        var resId = Guid.NewGuid();
        var res = CreateHold(resId, userId, ReservationStatus.Confirmed, prepaidAmount: 50000m, confirmedAt: Now);
        _repo.Add(res);

        // Payment refund fails
        _paymentClient.RefundResultToReturn = Result<ReservationDebitState>.Failure(ReservationPaymentErrors.Unavailable);

        var handler = CreateHandler();
        var command = new CancelReservationCommand(resId, userId, "Cancel with failing payment");

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Cancelled", result.Value!.Status);
        Assert.Equal(50000m, result.Value.RefundAmount);
        Assert.Equal("Đặt chỗ đã được hủy. Tiền cọc đang được hoàn về ví.", result.Value.Message);
        Assert.Equal(ReservationStatus.Cancelled, res.Status);
        Assert.Null(res.PrepaidOutcome); // null = refund pending for worker
    }

    [Fact]
    public void C06_11_RecordCancellationRefunded_OnForfeitedOrNonCancelled_Throws()
    {
        var userId = Guid.NewGuid();

        // 1. Non-cancelled reservation (Confirmed)
        var confirmedRes = CreateHold(Guid.NewGuid(), userId, ReservationStatus.Confirmed, confirmedAt: Now);
        Assert.Throws<ReservationDomainException>(() => confirmedRes.RecordCancellationRefunded());

        // 2. Forfeited reservation
        var forfeitedRes = CreateHold(Guid.NewGuid(), userId, ReservationStatus.Confirmed, confirmedAt: Now);
        forfeitedRes.Cancel(Now.AddMinutes(10), CancelReason.CustomerCancelled, PrepaidOutcome.Forfeited);
        Assert.Throws<ReservationDomainException>(() => forfeitedRes.RecordCancellationRefunded());

        // 3. Cancelled by NoShow (not CustomerCancelled)
        var noShowRes = CreateHold(Guid.NewGuid(), userId, ReservationStatus.Confirmed, appointmentTime: Now.AddMinutes(-20), confirmedAt: Now.AddMinutes(-30));
        noShowRes.Cancel(Now, CancelReason.NoShow, PrepaidOutcome.Forfeited);
        Assert.Throws<ReservationDomainException>(() => noShowRes.RecordCancellationRefunded());

        // 4. Valid cancelled reservation transitions to Refunded
        var validRes = CreateHold(Guid.NewGuid(), userId, ReservationStatus.Confirmed, confirmedAt: Now);
        validRes.Cancel(Now.AddMinutes(2), CancelReason.CustomerCancelled, null);
        validRes.RecordCancellationRefunded();
        Assert.Equal(PrepaidOutcome.Refunded, validRes.PrepaidOutcome);

        // 5. Calling again when already Refunded throws
        Assert.Throws<ReservationDomainException>(() => validRes.RecordCancellationRefunded());
    }

    [Fact]
    public async Task C06_12_Regression_Scheduled_Cancel_BehavesAsBefore()
    {
        var userId = Guid.NewGuid();
        var resId1 = Guid.NewGuid();
        var scheduledBeforeAppt = CreateHold(
            resId1, userId, ReservationStatus.Confirmed, prepaidAmount: 50000m,
            appointmentTime: Now.AddHours(2), reservationType: ReservationType.Scheduled, confirmedAt: Now.AddHours(-1));
        _repo.Add(scheduledBeforeAppt);

        var handler = CreateHandler();

        // Before appointment: cancel allowed, refund 100%
        var result1 = await handler.Handle(new CancelReservationCommand(resId1, userId, "Cancel scheduled"), CancellationToken.None);
        Assert.True(result1.IsSuccess);
        Assert.Equal("Cancelled", result1.Value!.Status);
        Assert.Equal(50000m, result1.Value.RefundAmount);
        Assert.Equal(PrepaidOutcome.Refunded, scheduledBeforeAppt.PrepaidOutcome);

        // After appointment: cancel blocked
        var resId2 = Guid.NewGuid();
        var scheduledAfterAppt = CreateHold(
            resId2, userId, ReservationStatus.Confirmed, prepaidAmount: 50000m,
            appointmentTime: Now.AddMinutes(-5), reservationType: ReservationType.Scheduled, confirmedAt: Now.AddHours(-1));
        _repo.Add(scheduledAfterAppt);

        var result2 = await handler.Handle(new CancelReservationCommand(resId2, userId, "Cancel too late"), CancellationToken.None);
        Assert.True(result2.IsFailure);
        Assert.Equal(ReservationErrors.CannotCancel, result2.Error);
    }

    [Theory]
    [InlineData(0, 0, true)]   // ConfirmedAt + 0:00 -> Refund
    [InlineData(4, 59, true)]  // ConfirmedAt + 4:59 -> Refund
    [InlineData(5, 0, false)]  // ConfirmedAt + 5:00 -> Forfeit
    [InlineData(5, 1, false)]  // ConfirmedAt + 5:01 -> Forfeit
    public void C06_13_CancellationRefundPolicy_Table(int minutes, int seconds, bool expectedRefund)
    {
        var confirmedAt = new DateTimeOffset(2026, 11, 10, 10, 0, 0, TimeSpan.Zero);
        var now = confirmedAt.AddMinutes(minutes).AddSeconds(seconds);
        var res = Reservation.CreateTemporaryHold(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "51A12345", Guid.NewGuid(),
            VehicleGroup.Car, ReservationType.HoldWhileDriving, confirmedAt.AddMinutes(15),
            50000m, confirmedAt.AddMinutes(-5));
        res.Confirm(confirmedAt);

        var decision = CancellationRefundPolicy.Decide(res, now);

        if (expectedRefund)
        {
            var refund = Assert.IsType<CancellationRefundDecision.Refund>(decision);
            Assert.Equal(50000m, refund.Amount);
        }
        else
        {
            Assert.IsType<CancellationRefundDecision.Forfeit>(decision);
        }
    }

    // -------------------------------------------------------------------------
    // Doubles
    // -------------------------------------------------------------------------
    private sealed class DbUpdateConcurrencyException : Exception { }

    private sealed class FakeReservationRepository : IReservationRepository
    {
        private readonly List<Reservation> _list = [];
        public Action? OnSaveChanges { get; set; }
        public Func<Guid, Task<Reservation?>>? FindByIdOverride { get; set; }

        public void Add(Reservation r) => _list.Add(r);

        public Task<Reservation?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            if (FindByIdOverride != null)
            {
                return FindByIdOverride(id);
            }
            return Task.FromResult(_list.FirstOrDefault(r => r.Id == id));
        }

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

        public Task<Result<ReservationDebitResult>> DebitAsync(Guid reservationId, Guid accountId, Guid lotId, long amountVnd, PrepaymentPurpose purpose = PrepaymentPurpose.Deposit, CancellationToken cancellationToken = default) =>
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
