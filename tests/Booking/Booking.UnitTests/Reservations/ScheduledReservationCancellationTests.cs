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

public sealed class ScheduledReservationCancellationTests
{
    private static readonly DateTimeOffset AppointmentTimeA = new(2026, 11, 15, 14, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset ConfirmedAtTime = AppointmentTimeA.AddHours(-2);

    private readonly FakeReservationRepository _repo = new();
    private readonly FakeReservationPaymentClient _paymentClient = new();
    private readonly RecordingOutbox _outbox = new();
    private readonly FakeClock _clock = new(ConfirmedAtTime);

    private CancelReservationCommandHandler CreateHandler() =>
        new(_repo, _paymentClient, _clock, NullLogger<CancelReservationCommandHandler>.Instance, _outbox);

    private static Reservation CreateScheduledHold(
        Guid id,
        Guid userId,
        ReservationStatus status = ReservationStatus.Confirmed,
        decimal prepaidAmount = 60000m,
        DateTimeOffset? appointmentTime = null,
        DateTimeOffset? confirmedAt = null)
    {
        var appTime = appointmentTime ?? AppointmentTimeA;
        var res = Reservation.CreateTemporaryHold(
            id,
            userId,
            Guid.NewGuid(),
            "51A88888",
            Guid.NewGuid(),
            VehicleGroup.Car,
            ReservationType.Scheduled,
            appTime,
            prepaidAmount,
            appTime.AddHours(-3),
            durationMinutes: 120,
            tempHoldExpiresAt: appTime.AddHours(-1));

        if (status == ReservationStatus.Confirmed)
        {
            res.Confirm(confirmedAt ?? ConfirmedAtTime);
        }

        return res;
    }

    [Fact]
    public async Task C07_01_Cancel_At_A_Minus_31Min_Refunds_And_Sets_Outcome_Refunded()
    {
        var userId = Guid.NewGuid();
        var resId = Guid.NewGuid();
        var res = CreateScheduledHold(resId, userId, ReservationStatus.Confirmed, prepaidAmount: 60000m);
        _repo.Add(res);

        _clock.UtcNow = AppointmentTimeA.AddMinutes(-31);
        var handler = CreateHandler();
        var command = new CancelReservationCommand(resId, userId, "Cancel 31 min before");

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Cancelled", result.Value!.Status);
        Assert.Equal(60000m, result.Value.RefundAmount);
        Assert.Equal("Đặt chỗ đã được hủy. Tiền đã trả trước đã được hoàn về ví của bạn.", result.Value.Message);

        Assert.Equal(ReservationStatus.Cancelled, res.Status);
        Assert.Equal(CancelReason.CustomerCancelled, res.CancelReason);
        Assert.Equal(PrepaidOutcome.Refunded, res.PrepaidOutcome);
        Assert.Equal(_clock.UtcNow, res.CancelledAt);

        var refundCall = Assert.Single(_paymentClient.RefundCalls);
        Assert.Equal(resId, refundCall.ReservationId);
        Assert.Equal(userId, refundCall.AccountId);
        Assert.Equal(60000L, refundCall.AmountVnd);
        Assert.Equal("CustomerCancelled", refundCall.Reason);

        var @event = Assert.Single(_outbox.Events);
        var cancelledEvent = Assert.IsType<ReservationCancelledIntegrationEvent>(@event);
        Assert.Equal(resId, cancelledEvent.ReservationId);
        Assert.Equal(userId, cancelledEvent.UserId);
        Assert.Equal(60000m, cancelledEvent.RefundAmount);
    }

    [Fact]
    public async Task C07_02_Cancel_At_Exactly_A_Minus_30Min_Refunds()
    {
        var userId = Guid.NewGuid();
        var resId = Guid.NewGuid();
        var res = CreateScheduledHold(resId, userId, ReservationStatus.Confirmed, prepaidAmount: 60000m);
        _repo.Add(res);

        _clock.UtcNow = AppointmentTimeA.AddMinutes(-30);
        var handler = CreateHandler();
        var command = new CancelReservationCommand(resId, userId, "Cancel exactly 30 min before");

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Cancelled", result.Value!.Status);
        Assert.Equal(60000m, result.Value.RefundAmount);
        Assert.Equal("Đặt chỗ đã được hủy. Tiền đã trả trước đã được hoàn về ví của bạn.", result.Value.Message);

        Assert.Equal(ReservationStatus.Cancelled, res.Status);
        Assert.Equal(PrepaidOutcome.Refunded, res.PrepaidOutcome);
        Assert.Single(_paymentClient.RefundCalls);
    }

    [Fact]
    public async Task C07_03_Cancel_At_A_Minus_29Min59Sec_Forfeited_NoRefundCall_EventZero()
    {
        var userId = Guid.NewGuid();
        var resId = Guid.NewGuid();
        var res = CreateScheduledHold(resId, userId, ReservationStatus.Confirmed, prepaidAmount: 60000m);
        _repo.Add(res);

        // A - 29:59 is later than A - 30:00 -> Forfeit
        _clock.UtcNow = AppointmentTimeA.AddMinutes(-30).AddSeconds(1);
        var handler = CreateHandler();
        var command = new CancelReservationCommand(resId, userId, "Cancel 29:59 before");

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Cancelled", result.Value!.Status);
        Assert.Equal(0m, result.Value.RefundAmount);
        Assert.Equal("Đặt chỗ đã được hủy. Bạn hủy muộn hơn 30 phút trước giờ vào nên không được hoàn tiền đã trả trước.", result.Value.Message);

        Assert.Equal(ReservationStatus.Cancelled, res.Status);
        Assert.Equal(PrepaidOutcome.Forfeited, res.PrepaidOutcome);
        Assert.Empty(_paymentClient.RefundCalls);

        var @event = Assert.Single(_outbox.Events);
        var cancelledEvent = Assert.IsType<ReservationCancelledIntegrationEvent>(@event);
        Assert.Equal(0m, cancelledEvent.RefundAmount);
    }

    [Fact]
    public async Task C07_04_Cancel_At_A_Plus_5Min_NotUsed_Allowed_Forfeited()
    {
        var userId = Guid.NewGuid();
        var resId = Guid.NewGuid();
        var res = CreateScheduledHold(resId, userId, ReservationStatus.Confirmed, prepaidAmount: 60000m);
        _repo.Add(res);

        // Past appointment time, but vehicle not yet entered (UsedAt == null)
        _clock.UtcNow = AppointmentTimeA.AddMinutes(5);
        var handler = CreateHandler();
        var command = new CancelReservationCommand(resId, userId, "Cancel 5 min after appointment");

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Cancelled", result.Value!.Status);
        Assert.Equal(0m, result.Value.RefundAmount);
        Assert.Equal(ReservationStatus.Cancelled, res.Status);
        Assert.Equal(PrepaidOutcome.Forfeited, res.PrepaidOutcome);
        Assert.Empty(_paymentClient.RefundCalls);
    }

    [Fact]
    public async Task C07_05_Used_CannotCancel_NoPaymentCall()
    {
        var userId = Guid.NewGuid();
        var resId = Guid.NewGuid();
        var res = CreateScheduledHold(resId, userId, ReservationStatus.Confirmed, prepaidAmount: 60000m);
        res.Use(AppointmentTimeA);
        _repo.Add(res);

        _clock.UtcNow = AppointmentTimeA.AddMinutes(5);
        var handler = CreateHandler();
        var command = new CancelReservationCommand(resId, userId, "Cancel used");

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ReservationErrors.CannotCancel, result.Error);
        Assert.Empty(_paymentClient.RefundCalls);
    }

    [Fact]
    public async Task C07_06_Scheduled_TemporaryHold_Cancelled_NoPaymentCall()
    {
        var userId = Guid.NewGuid();
        var resId = Guid.NewGuid();
        var res = CreateScheduledHold(resId, userId, ReservationStatus.TemporaryHold, prepaidAmount: 60000m);
        _repo.Add(res);

        var handler = CreateHandler();
        var command = new CancelReservationCommand(resId, userId, "Cancel temp hold");

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Cancelled", result.Value!.Status);
        Assert.Equal(0m, result.Value.RefundAmount);
        Assert.Equal("Đặt chỗ đã được hủy.", result.Value.Message);
        Assert.Equal(ReservationStatus.Cancelled, res.Status);
        Assert.Null(res.PrepaidOutcome);
        Assert.Empty(_paymentClient.RefundCalls);
    }

    [Fact]
    public async Task C07_07_Refund_Fails_After_Save_Result_Success_StatusCancelled_OutcomeNull_Worker_Refunds()
    {
        var userId = Guid.NewGuid();
        var resId = Guid.NewGuid();
        var res = CreateScheduledHold(resId, userId, ReservationStatus.Confirmed, prepaidAmount: 60000m);
        _repo.Add(res);

        // Cancel 45 min before appointment -> eligible for refund
        _clock.UtcNow = AppointmentTimeA.AddMinutes(-45);
        _paymentClient.RefundResultToReturn = Result<ReservationDebitState>.Failure(ReservationPaymentErrors.Unavailable);

        var handler = CreateHandler();
        var command = new CancelReservationCommand(resId, userId, "Cancel with transient payment failure");

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Cancelled", result.Value!.Status);
        Assert.Equal(60000m, result.Value.RefundAmount);
        Assert.Equal("Đặt chỗ đã được hủy. Tiền đã trả trước đang được hoàn về ví.", result.Value.Message);
        Assert.Equal(ReservationStatus.Cancelled, res.Status);
        Assert.Null(res.PrepaidOutcome); // null = pending refund

        // Worker pass: payment is now healthy
        _paymentClient.RefundResultToReturn = Result<ReservationDebitState>.Success(ReservationDebitState.Refunded);
        var refundCallResult = await _paymentClient.RefundAsync(res.Id, res.UserId, (long)res.PrepaidAmount, "CustomerCancelled");
        Assert.True(refundCallResult.IsSuccess);
        res.RecordCancellationRefunded();
        Assert.Equal(PrepaidOutcome.Refunded, res.PrepaidOutcome);
    }

    [Theory]
    [InlineData(-31, 0, true)]   // A - 31:00 -> Refund
    [InlineData(-30, 0, true)]   // A - 30:00 -> Refund
    [InlineData(-29, -59, false)] // A - 29:59 -> Forfeit
    [InlineData(0, 0, false)]    // A -> Forfeit
    [InlineData(14, 59, false)]  // A + 14:59 -> Forfeit
    public void C07_08_Scheduled_CancellationRefundPolicy_Table(int minutesOffset, int secondsOffset, bool expectedRefund)
    {
        var apptTime = AppointmentTimeA;
        var now = apptTime.AddMinutes(minutesOffset).AddSeconds(secondsOffset);
        var res = CreateScheduledHold(Guid.NewGuid(), Guid.NewGuid(), ReservationStatus.Confirmed, prepaidAmount: 60000m, appointmentTime: apptTime);

        var decision = CancellationRefundPolicy.Decide(res, now);

        if (expectedRefund)
        {
            var refund = Assert.IsType<CancellationRefundDecision.Refund>(decision);
            Assert.Equal(60000m, refund.Amount);
        }
        else
        {
            Assert.IsType<CancellationRefundDecision.Forfeit>(decision);
        }
    }

    [Fact]
    public async Task C07_09_Regression_HWD_Rules_From_US_RES_006_Pass_Unchanged()
    {
        var userId = Guid.NewGuid();
        var hwdConfirmedAt = new DateTimeOffset(2026, 11, 10, 10, 0, 0, TimeSpan.Zero);

        // HWD 1: cancel within 5 min -> refunded
        var resId1 = Guid.NewGuid();
        var hwdRefund = Reservation.CreateTemporaryHold(
            resId1, userId, Guid.NewGuid(), "51A11111", Guid.NewGuid(),
            VehicleGroup.Car, ReservationType.HoldWhileDriving, hwdConfirmedAt.AddMinutes(20),
            50000m, hwdConfirmedAt.AddMinutes(-5));
        hwdRefund.Confirm(hwdConfirmedAt);
        _repo.Add(hwdRefund);

        _clock.UtcNow = hwdConfirmedAt.AddMinutes(4);
        var handler = CreateHandler();
        var result1 = await handler.Handle(new CancelReservationCommand(resId1, userId, "HWD refund"), CancellationToken.None);
        Assert.True(result1.IsSuccess);
        Assert.Equal(50000m, result1.Value!.RefundAmount);
        Assert.Equal("Đặt chỗ đã được hủy. Tiền cọc đã được hoàn về ví của bạn.", result1.Value.Message);
        Assert.Equal(PrepaidOutcome.Refunded, hwdRefund.PrepaidOutcome);

        // HWD 2: cancel after 5 min -> forfeited
        var resId2 = Guid.NewGuid();
        var hwdForfeit = Reservation.CreateTemporaryHold(
            resId2, userId, Guid.NewGuid(), "51A22222", Guid.NewGuid(),
            VehicleGroup.Car, ReservationType.HoldWhileDriving, hwdConfirmedAt.AddMinutes(20),
            50000m, hwdConfirmedAt.AddMinutes(-5));
        hwdForfeit.Confirm(hwdConfirmedAt);
        _repo.Add(hwdForfeit);

        _clock.UtcNow = hwdConfirmedAt.AddMinutes(6);
        var result2 = await handler.Handle(new CancelReservationCommand(resId2, userId, "HWD forfeit"), CancellationToken.None);
        Assert.True(result2.IsSuccess);
        Assert.Equal(0m, result2.Value!.RefundAmount);
        Assert.Equal("Đặt chỗ đã được hủy. Bạn hủy sau 5 phút kể từ lúc xác nhận nên không được hoàn cọc.", result2.Value.Message);
        Assert.Equal(PrepaidOutcome.Forfeited, hwdForfeit.PrepaidOutcome);
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

        public Task<Reservation?> FindCancelledNoSuitableSlotByLotAndPlateAsync(Guid lotId, string plateNumber, DateTimeOffset arrivalTime, CancellationToken cancellationToken = default) =>
            Task.FromResult<Reservation?>(null);

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
