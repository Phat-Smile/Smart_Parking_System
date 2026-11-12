using Microsoft.EntityFrameworkCore;
using ParkingSystem.Booking.Application.Reservations;
using ParkingSystem.Booking.Domain.Reservations;
using ParkingSystem.BuildingBlocks.Time;

namespace ParkingSystem.Booking.Persistence.Reservations;

internal sealed class ArrivalHeldQuotaReader(BookingDbContext db, IClock clock) : IArrivalHeldQuotaReader
{
    public Task<int> CountAsync(Guid lotId, DateTimeOffset arrivalAt, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var at = arrivalAt.ToUniversalTime();
        // Same active status and half-open interval semantics as ReservationCapacityCoordinator.
        // Holds currently valid are counted conservatively even if they may expire before arrival.
        return db.Reservations.AsNoTracking()
            .Where(r => r.LotId == lotId && r.VehicleGroup == VehicleGroup.Car
                && r.WindowStartAt <= at && r.HoldEndAt > at
                && (r.Status == ReservationStatus.Confirmed
                    || (r.Status == ReservationStatus.TemporaryHold && r.TempHoldExpiresAt > now)))
            .CountAsync(cancellationToken);
    }
}
