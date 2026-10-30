using Microsoft.EntityFrameworkCore;
using ParkingSystem.Booking.Application.Reservations;
using ParkingSystem.Booking.Domain.Reservations;

namespace ParkingSystem.Booking.Persistence.Reservations;

internal sealed class ReservationRepository(BookingDbContext dbContext) : IReservationRepository
{
    public Task<Reservation?> FindByIdAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.Reservations.FirstOrDefaultAsync(reservation => reservation.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Reservation>> ListByUserIdAsync(Guid userId, ReservationStatus? status, CancellationToken cancellationToken)
    {
        var query = dbContext.Reservations.AsNoTracking().Where(reservation => reservation.UserId == userId);
        if (status.HasValue)
        {
            query = query.Where(reservation => reservation.Status == status.Value);
        }

        return await query.OrderByDescending(reservation => reservation.CreatedAt).ToListAsync(cancellationToken);
    }

    public void Add(Reservation reservation) =>
        dbContext.Reservations.Add(reservation);

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        dbContext.SaveChangesAsync(cancellationToken);
}
