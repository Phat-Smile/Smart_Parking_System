using Microsoft.EntityFrameworkCore;
using ParkingSystem.Booking.Application.Reservations;
using ParkingSystem.Booking.Domain.Reservations;

namespace ParkingSystem.Booking.Persistence.Reservations;

internal sealed class ReservationRepository(BookingDbContext dbContext) : IReservationRepository
{
    public Task<Reservation?> FindByIdAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.Reservations.FirstOrDefaultAsync(reservation => reservation.Id == id, cancellationToken);

    public void Add(Reservation reservation) =>
        dbContext.Reservations.Add(reservation);

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        dbContext.SaveChangesAsync(cancellationToken);
}
