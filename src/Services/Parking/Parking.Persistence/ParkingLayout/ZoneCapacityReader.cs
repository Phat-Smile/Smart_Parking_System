using Microsoft.EntityFrameworkCore;
using ParkingSystem.Parking.Application.ParkingLayout.ZoneCapacity;
using ParkingSystem.Parking.Domain.ParkingLayout;
using ParkingSystem.Parking.Domain.ParkingLots;
using ParkingSystem.Parking.Domain.ParkingSessions;

namespace ParkingSystem.Parking.Persistence.ParkingLayout;

internal sealed class ZoneCapacityReader(ParkingDbContext dbContext) : IZoneCapacityReader
{
    public Task<bool> IsOwnerAsync(Guid lotId, Guid ownerAccountId, CancellationToken cancellationToken) =>
        dbContext.ParkingLotAssignments.AsNoTracking().AnyAsync(assignment =>
            assignment.LotId == lotId && assignment.AccountId == ownerAccountId &&
            assignment.Role == ParkingLotMemberRole.Owner, cancellationToken);

    public async Task<IReadOnlyList<FloorCapacity>> ReadAsync(Guid lotId, CancellationToken cancellationToken)
    {
        var floors = await dbContext.Floors.AsNoTracking()
            .Where(floor => floor.LotId == lotId)
            .OrderBy(floor => floor.Name)
            .ToListAsync(cancellationToken);
        var floorIds = floors.Select(floor => floor.Id).ToList();
        var zones = await dbContext.Zones.AsNoTracking()
            .Where(zone => floorIds.Contains(zone.FloorId))
            .OrderBy(zone => zone.Code)
            .ToListAsync(cancellationToken);
        var zoneIds = zones.Select(zone => zone.Id).ToList();

        // Counted in SQL per zone; no slot is loaded.
        var slots = await dbContext.ParkingSlots.AsNoTracking()
            .Where(slot => zoneIds.Contains(slot.ZoneId))
            .GroupBy(slot => slot.ZoneId)
            .Select(group => new
            {
                ZoneId = group.Key,
                Total = group.Count(),
                // BR-02.a: valid capacity leaves out Maintenance and Unavailable slots.
                Valid = group.Count(slot => slot.EffectiveStatus != SlotEffectiveStatus.Maintenance
                                            && slot.EffectiveStatus != SlotEffectiveStatus.Unavailable),
                Inside = group.Count(slot => slot.EffectiveStatus == SlotEffectiveStatus.Assigned
                                             || slot.EffectiveStatus == SlotEffectiveStatus.Occupied
                                             || slot.EffectiveStatus == SlotEffectiveStatus.NeedsVerification)
            })
            .ToDictionaryAsync(row => row.ZoneId, cancellationToken);

        return floors.Select(floor => new FloorCapacity(
            floor.Id,
            floor.Name,
            zones.Where(zone => zone.FloorId == floor.Id).Select(zone =>
            {
                if (zone.VehicleGroup == VehicleType.Motorbike)
                    return new ZoneCapacityItem(zone.Id, zone.Code, zone.VehicleGroup.ToString(), zone.Capacity, null, null, zone.CurrentVehicleCount);

                var counts = slots.GetValueOrDefault(zone.Id);
                return new ZoneCapacityItem(zone.Id, zone.Code, zone.VehicleGroup.ToString(), zone.Capacity,
                    counts?.Total ?? 0, counts?.Valid ?? 0, counts?.Inside ?? 0);
            }).ToList())).ToList();
    }
}
