namespace ParkingSystem.Booking.Application.Reservations;

// Returns currently effective Car reservations whose booking windows include the requested arrival time.
// Booking remains the owner of reservation state; callers must not query its database directly.
public interface IArrivalHeldQuotaReader
{
    Task<int> CountAsync(Guid lotId, DateTimeOffset arrivalAt, CancellationToken cancellationToken);
}
