using ParkingSystem.Booking.Domain.Enums;
using ParkingSystem.Booking.Domain.Reservations;

namespace ParkingSystem.Booking.Application.Reservations;

public interface IReservationRepository
{
    Task<Reservation?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<Reservation>> GetActiveReservationsByUserIdAsync(
        Guid userId,
        DateTimeOffset utcNow,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<Reservation>> GetActiveReservationsByPlateAsync(
        string plateNumber,
        DateTimeOffset utcNow,
        CancellationToken cancellationToken);

    Task<int> CountActiveOverlappingReservationsAsync(
        Guid lotId,
        VehicleGroup vehicleGroup,
        ReservationTimeWindow window,
        DateTimeOffset utcNow,
        CancellationToken cancellationToken);

    Task<int> CountCurrentlyHeldReservationsAsync(
        Guid lotId,
        VehicleGroup vehicleGroup,
        DateTimeOffset evaluationTime,
        CancellationToken cancellationToken);

    void Add(Reservation reservation);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
