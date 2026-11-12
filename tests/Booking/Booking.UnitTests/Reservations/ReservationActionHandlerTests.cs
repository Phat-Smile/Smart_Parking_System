using Microsoft.Extensions.Logging.Abstractions;
using ParkingSystem.Booking.Application.Payments;
using ParkingSystem.Booking.Application.Reservations;
using ParkingSystem.Booking.Application.Reservations.Commands.CancelReservation;
using ParkingSystem.Booking.Application.Reservations.Commands.ConfirmReservation;
using ParkingSystem.Booking.Application.Reservations.Queries.GetMyReservations;
using ParkingSystem.Booking.Domain.Reservations;
using ParkingSystem.BuildingBlocks.Results;
using ParkingSystem.BuildingBlocks.Time;
using Xunit;

namespace ParkingSystem.Booking.UnitTests.Reservations;

public sealed class ReservationActionHandlerTests
{
    private readonly FakeReservationRepository _repo = new();
    private readonly FakeClock _clock = new(DateTimeOffset.Parse("2026-10-30T10:00:00Z"));

    [Fact]
    public async Task ConfirmReservation_WhenNotFound_ReturnsNotFoundFailure()
    {
        var handler = new ConfirmReservationCommandHandler(_repo, new FakeReservationPaymentClient(), _clock, NullLogger<ConfirmReservationCommandHandler>.Instance, new FakeOutboxWriter());
        var command = new ConfirmReservationCommand(Guid.NewGuid(), Guid.NewGuid(), "Wallet", 50000);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ReservationErrors.NotFound.Code, result.Error!.Code);
    }

    [Fact]
    public async Task ConfirmReservation_WhenBelongsToDifferentUser_ReturnsAccessDenied()
    {
        var reservation = CreateTestReservation(Guid.NewGuid(), Guid.NewGuid());
        _repo.Add(reservation);

        var handler = new ConfirmReservationCommandHandler(_repo, new FakeReservationPaymentClient(), _clock, NullLogger<ConfirmReservationCommandHandler>.Instance, new FakeOutboxWriter());
        var command = new ConfirmReservationCommand(reservation.Id, Guid.NewGuid(), "Wallet", 50000);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ReservationErrors.AccessDenied.Code, result.Error!.Code);
    }

    [Fact]
    public async Task ConfirmReservation_WhenHoldExpired_ReturnsHoldExpiredFailure()
    {
        var userId = Guid.NewGuid();
        var expiredTime = _clock.UtcNow.AddMinutes(-5);
        var reservation = CreateTestReservation(Guid.NewGuid(), userId, tempHoldExpiresAt: expiredTime);
        _repo.Add(reservation);

        var handler = new ConfirmReservationCommandHandler(_repo, new FakeReservationPaymentClient(), _clock, NullLogger<ConfirmReservationCommandHandler>.Instance, new FakeOutboxWriter());
        var command = new ConfirmReservationCommand(reservation.Id, userId, "Wallet", 50000);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ReservationErrors.HoldExpired.Code, result.Error!.Code);
        Assert.Equal(ReservationStatus.Expired, reservation.Status);
    }

    [Fact]
    public async Task ConfirmReservation_WhenValid_ConfirmsSuccessfullyAndGeneratesQrToken()
    {
        var userId = Guid.NewGuid();
        var reservation = CreateTestReservation(
            Guid.NewGuid(),
            userId,
            tempHoldExpiresAt: _clock.UtcNow.AddMinutes(10),
            reservationType: ReservationType.HoldWhileDriving,
            prepaidAmount: 0m);
        _repo.Add(reservation);

        var handler = new ConfirmReservationCommandHandler(_repo, new FakeReservationPaymentClient(), _clock, NullLogger<ConfirmReservationCommandHandler>.Instance, new FakeOutboxWriter());
        var command = new ConfirmReservationCommand(reservation.Id, userId, "Wallet", 0m);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(ReservationStatus.Confirmed.ToString(), result.Value!.Status);
        Assert.StartsWith("RES-QR-", result.Value.QrCodeToken);
        Assert.Equal(reservation.Id, result.Value.ReservationId);
        Assert.Equal(ReservationStatus.Confirmed, reservation.Status);
    }

    [Fact]
    public async Task CancelReservation_WhenValid_CancelsSuccessfully()
    {
        var userId = Guid.NewGuid();
        var reservation = CreateTestReservation(Guid.NewGuid(), userId);
        _repo.Add(reservation);

        var handler = new CancelReservationCommandHandler(_repo, new FakeReservationPaymentClient(), _clock, NullLogger<CancelReservationCommandHandler>.Instance, new FakeOutboxWriter());
        var command = new CancelReservationCommand(reservation.Id, userId, "Changed plans");

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Cancelled", result.Value!.Status);
        Assert.Equal(ReservationStatus.Cancelled, reservation.Status);
    }

    [Fact]
    public async Task GetMyReservations_ReturnsUserReservations()
    {
        var userId = Guid.NewGuid();
        var res1 = CreateTestReservation(Guid.NewGuid(), userId);
        var res2 = CreateTestReservation(Guid.NewGuid(), userId);
        var otherRes = CreateTestReservation(Guid.NewGuid(), Guid.NewGuid());

        _repo.Add(res1);
        _repo.Add(res2);
        _repo.Add(otherRes);

        var handler = new GetMyReservationsQueryHandler(_repo);
        var query = new GetMyReservationsQuery(userId);

        var result = await handler.Handle(query, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value!.Count);
        Assert.All(result.Value, r => Assert.Equal(userId, (r.ReservationId == res1.Id || r.ReservationId == res2.Id) ? userId : Guid.Empty));
    }

    private static Reservation CreateTestReservation(
        Guid reservationId,
        Guid userId,
        DateTimeOffset? tempHoldExpiresAt = null,
        ReservationType reservationType = ReservationType.HoldWhileDriving,
        decimal prepaidAmount = 0m)
    {
        return Reservation.CreateTemporaryHold(
            reservationId,
            userId,
            Guid.NewGuid(),
            "51H-999.88",
            Guid.NewGuid(),
            VehicleGroup.Car,
            reservationType,
            DateTimeOffset.Parse("2026-10-30T14:30:00Z"),
            prepaidAmount,
            DateTimeOffset.Parse("2026-10-30T10:00:00Z"),
            durationMinutes: reservationType == ReservationType.Scheduled ? 120 : null,
            tempHoldExpiresAt: tempHoldExpiresAt ?? DateTimeOffset.Parse("2026-10-30T10:15:00Z"));
    }

    private sealed class FakeReservationRepository : IReservationRepository
    {
        public Task ReloadAsync(Reservation reservation, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void DiscardPendingChanges() { }
        public Task<IReadOnlyList<Reservation>> ListPaymentReconciliationCandidatesAsync(DateTimeOffset now, int batchSize, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<Reservation>>([]);
        public Task<IReadOnlyList<Reservation>> ListNoShowCandidatesAsync(DateTimeOffset now, int batchSize, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<Reservation>>([]);

        private readonly List<Reservation> _list = [];

        public Task<Reservation?> FindByIdAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult(_list.FirstOrDefault(r => r.Id == id));

        public Task<IReadOnlyList<Reservation>> ListByUserIdAsync(Guid userId, ReservationStatus? status, CancellationToken cancellationToken)
        {
            var query = _list.Where(r => r.UserId == userId);
            if (status.HasValue)
            {
                query = query.Where(r => r.Status == status.Value);
            }
            return Task.FromResult<IReadOnlyList<Reservation>>(query.ToList());
        }

        public Task<IReadOnlyList<Reservation>> ListActiveByLotIdAsync(Guid lotId, CancellationToken cancellationToken)
        {
            var active = _list.Where(r => r.LotId == lotId &&
                (r.Status == ReservationStatus.TemporaryHold || r.Status == ReservationStatus.Confirmed)).ToList();
            return Task.FromResult<IReadOnlyList<Reservation>>(active);
        }

        public Task<Reservation?> FindActiveConfirmedByLotAndPlateAsync(
            Guid lotId,
            string plateNumber,
            DateTimeOffset arrivalTime,
            CancellationToken cancellationToken)
        {
            var normalized = plateNumber.Trim().ToUpperInvariant();
            var res = _list.FirstOrDefault(r =>
                r.LotId == lotId &&
                r.PlateNumber.Trim().ToUpperInvariant() == normalized &&
                r.Status == ReservationStatus.Confirmed &&
                arrivalTime >= r.WindowStartAt.AddMinutes(-15) &&
                arrivalTime <= r.AppointmentTime.AddMinutes(Reservation.NoShowGracePeriodMinutes));
            return Task.FromResult(res);
        }

        public void Add(Reservation reservation) => _list.Add(reservation);

        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class FakeClock(DateTimeOffset utcNow) : IClock
    {
        public DateTimeOffset UtcNow { get; } = utcNow;
    }

    private sealed class FakeOutboxWriter : ParkingSystem.Booking.Application.Common.Messaging.Outbox.IOutboxWriter
    {
        public void Enqueue<TEvent>(TEvent integrationEvent, string eventType, int eventVersion, Guid? correlationId = null, Guid? causationId = null) where TEvent : class { }
    }

    private sealed class FakeReservationPaymentClient : ParkingSystem.Booking.Application.Payments.IReservationPaymentClient
    {
        public Task<Result<ParkingSystem.Booking.Application.Payments.ReservationDebitResult>> DebitAsync(Guid reservationId, Guid accountId, Guid lotId, long amountVnd, ParkingSystem.Booking.Application.Payments.PrepaymentPurpose purpose, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result<ParkingSystem.Booking.Application.Payments.ReservationDebitResult>.Failure(ParkingSystem.Booking.Application.Payments.ReservationPaymentErrors.Unavailable));

        public Task<Result<ParkingSystem.Booking.Application.Payments.ReservationDebitState>> GetDebitStateAsync(Guid reservationId, Guid accountId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result<ParkingSystem.Booking.Application.Payments.ReservationDebitState>.Failure(ParkingSystem.Booking.Application.Payments.ReservationPaymentErrors.Unavailable));

        public Task<Result<ParkingSystem.Booking.Application.Payments.ReservationDebitState>> RefundAsync(Guid reservationId, Guid accountId, long amountVnd, string reason, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result<ParkingSystem.Booking.Application.Payments.ReservationDebitState>.Failure(ParkingSystem.Booking.Application.Payments.ReservationPaymentErrors.Unavailable));
    }
}
