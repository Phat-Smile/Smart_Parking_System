using Microsoft.Extensions.Logging.Abstractions;
using ParkingSystem.Booking.Application.Reservations;
using ParkingSystem.Booking.Application.Reservations.Commands.CancelReservation;
using ParkingSystem.Booking.Application.Reservations.Commands.ConfirmReservation;
using ParkingSystem.Booking.Application.Reservations.Queries.GetMyReservations;
using ParkingSystem.Booking.Domain.Reservations;
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
        var handler = new ConfirmReservationCommandHandler(_repo, _clock, NullLogger<ConfirmReservationCommandHandler>.Instance);
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

        var handler = new ConfirmReservationCommandHandler(_repo, _clock, NullLogger<ConfirmReservationCommandHandler>.Instance);
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

        var handler = new ConfirmReservationCommandHandler(_repo, _clock, NullLogger<ConfirmReservationCommandHandler>.Instance);
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
        var reservation = CreateTestReservation(Guid.NewGuid(), userId, tempHoldExpiresAt: _clock.UtcNow.AddMinutes(10));
        _repo.Add(reservation);

        var handler = new ConfirmReservationCommandHandler(_repo, _clock, NullLogger<ConfirmReservationCommandHandler>.Instance);
        var command = new ConfirmReservationCommand(reservation.Id, userId, "Wallet", 50000);

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

        var handler = new CancelReservationCommandHandler(_repo, _clock, NullLogger<CancelReservationCommandHandler>.Instance);
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
        DateTimeOffset? tempHoldExpiresAt = null)
    {
        return Reservation.CreateTemporaryHold(
            reservationId,
            userId,
            Guid.NewGuid(),
            "51H-999.88",
            Guid.NewGuid(),
            VehicleGroup.Car,
            ReservationType.Scheduled,
            DateTimeOffset.Parse("2026-10-30T14:30:00Z"),
            50000,
            DateTimeOffset.Parse("2026-10-30T10:00:00Z"),
            durationMinutes: 120,
            tempHoldExpiresAt: tempHoldExpiresAt ?? DateTimeOffset.Parse("2026-10-30T10:15:00Z"));
    }

    private sealed class FakeReservationRepository : IReservationRepository
    {
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

        public void Add(Reservation reservation) => _list.Add(reservation);

        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class FakeClock(DateTimeOffset utcNow) : IClock
    {
        public DateTimeOffset UtcNow { get; } = utcNow;
    }
}
