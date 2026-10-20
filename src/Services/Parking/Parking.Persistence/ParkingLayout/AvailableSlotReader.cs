using Microsoft.EntityFrameworkCore;
using ParkingSystem.Parking.Application.ParkingLayout;
using ParkingSystem.Parking.Domain.ParkingLayout;
using ParkingSystem.Parking.Domain.ParkingSessions;

namespace ParkingSystem.Parking.Persistence.ParkingLayout;

internal sealed class AvailableSlotReader(ParkingDbContext dbContext) : IAvailableSlotReader
{
    // BR-02.a: car slots whose effective status is Available. Counted in SQL, no slot is loaded.
    public Task<int> CountAvailableCarSlotsAsync(Guid lotId, CancellationToken cancellationToken) =>
        (from slot in dbContext.ParkingSlots
         join zone in dbContext.Zones on slot.ZoneId equals zone.Id
         where slot.LotId == lotId
               && slot.EffectiveStatus == SlotEffectiveStatus.Available
               && zone.VehicleGroup == VehicleType.Car
         select slot.Id)
        .CountAsync(cancellationToken);
}
