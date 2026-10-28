using ParkingSystem.Booking.Domain.Reservations;

namespace ParkingSystem.Booking.Application.Reservations;

public interface IReservationRepository
{
    Task<Reservation?> FindByIdAsync(Guid id, CancellationToken cancellationToken);

    void Add(Reservation reservation);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
