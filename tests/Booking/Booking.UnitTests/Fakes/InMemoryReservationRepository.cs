using ParkingSystem.Booking.Application.Reservations;
using ParkingSystem.Booking.Domain.Enums;
using ParkingSystem.Booking.Domain.Reservations;

namespace ParkingSystem.Tests.Booking.UnitTests.Fakes;

public sealed class InMemoryReservationRepository : IReservationRepository
{
    private readonly List<Reservation> _reservations = new();
    private readonly object _lock = new();

    public Task<Reservation?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            return Task.FromResult(_reservations.FirstOrDefault(r => r.Id == id));
        }
    }

    public Task<IReadOnlyList<Reservation>> GetActiveReservationsByUserIdAsync(
        Guid userId,
        DateTimeOffset utcNow,
        CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            var list = _reservations.Where(r => r.UserId == userId && r.IsActive(utcNow)).ToList();
            return Task.FromResult<IReadOnlyList<Reservation>>(list);
        }
    }

    public Task<IReadOnlyList<Reservation>> GetActiveReservationsByPlateAsync(
        string plateNumber,
        DateTimeOffset utcNow,
        CancellationToken cancellationToken)
    {
        var normalized = Reservation.NormalizePlateNumber(plateNumber);
        lock (_lock)
        {
            var list = _reservations.Where(r => r.PlateNumber == normalized && r.IsActive(utcNow)).ToList();
            return Task.FromResult<IReadOnlyList<Reservation>>(list);
        }
    }

    public Task<int> CountActiveOverlappingReservationsAsync(
        Guid lotId,
        VehicleGroup vehicleGroup,
        ReservationTimeWindow window,
        DateTimeOffset utcNow,
        CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            var count = _reservations.Count(r =>
                r.LotId == lotId &&
                r.VehicleGroup == vehicleGroup &&
                r.IsActive(utcNow) &&
                r.GetReservationWindow().Overlaps(window));

            return Task.FromResult(count);
        }
    }

    public Task<int> CountCurrentlyHeldReservationsAsync(
        Guid lotId,
        VehicleGroup vehicleGroup,
        DateTimeOffset evaluationTime,
        CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            var count = _reservations.Count(r =>
                r.LotId == lotId &&
                r.VehicleGroup == vehicleGroup &&
                r.IsActive(evaluationTime) &&
                r.GetReservationWindow().Contains(evaluationTime));

            return Task.FromResult(count);
        }
    }

    public void Add(Reservation reservation)
    {
        lock (_lock)
        {
            _reservations.Add(reservation);
        }
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public IReadOnlyList<Reservation> GetAll()
    {
        lock (_lock)
        {
            return _reservations.ToList();
        }
    }
}
