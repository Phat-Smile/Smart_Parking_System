using Microsoft.EntityFrameworkCore;
using ParkingSystem.Parking.Application.ParkingLayout;

namespace ParkingSystem.Parking.Persistence.ParkingLayout;

internal sealed class ZoneLabelReader(ParkingDbContext dbContext) : IZoneLabelReader
{
    public async Task<IReadOnlyDictionary<Guid, ZoneLabel>> GetLabelsAsync(IReadOnlyCollection<Guid> zoneIds, CancellationToken cancellationToken)
    {
        if (zoneIds.Count == 0)
            return new Dictionary<Guid, ZoneLabel>();

        return await dbContext.Zones.AsNoTracking()
            .Where(zone => zoneIds.Contains(zone.Id))
            .Join(dbContext.Floors, zone => zone.FloorId, floor => floor.Id, (zone, floor) => new { zone.Id, zone.Code, floor.Name })
            .ToDictionaryAsync(row => row.Id, row => new ZoneLabel(row.Code, row.Name), cancellationToken);
    }
}
