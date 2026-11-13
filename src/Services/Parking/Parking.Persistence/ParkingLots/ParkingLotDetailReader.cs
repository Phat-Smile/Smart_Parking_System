using Microsoft.EntityFrameworkCore;
using ParkingSystem.Parking.Application.ParkingLots;
using ParkingSystem.Parking.Application.ParkingLots.OpeningHours;
using ParkingSystem.Parking.Domain.ParkingLots;
using ParkingSystem.Parking.Domain.ParkingSessions;
using ParkingSystem.Parking.Application.ParkingLots.SearchCustomerLots;
using System.Text.Json;

namespace ParkingSystem.Parking.Persistence.ParkingLots;

internal sealed class ParkingLotDetailReader(ParkingDbContext dbContext, ICustomerLotFeaturesReader featureReader) : IParkingLotDetailReader
{
    public async Task<ParkingLotDetail?> GetApprovedByIdAsync(Guid parkingLotId, CancellationToken cancellationToken)
    {
        var lot = await dbContext.ParkingLots.AsNoTracking()
            .Where(lot => lot.Id == parkingLotId && lot.ApprovalStatus == ParkingLotApprovalStatus.Approved)
            .Select(lot => new
            {
                lot.Id, lot.Name, lot.Address, lot.Latitude, lot.Longitude, lot.Type, lot.OpeningHoursJson, lot.PublicInfoJson
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (lot is null) return null;

        var featureMap = await featureReader.ReadAsync([lot.Id], cancellationToken);
        featureMap.TryGetValue(lot.Id, out var features);
        var floorCount = await dbContext.Floors.AsNoTracking().CountAsync(x => x.LotId == lot.Id, cancellationToken);
        var zones = await (from zone in dbContext.Zones.AsNoTracking()
            join floor in dbContext.Floors.AsNoTracking() on zone.FloorId equals floor.Id
            where floor.LotId == lot.Id
            select new { zone.VehicleGroup, zone.Capacity }).ToListAsync(cancellationToken);
        var carCapacity = await dbContext.ParkingSlots.AsNoTracking()
            .CountAsync(x => x.LotId == lot.Id, cancellationToken);
        var motorbikeCapacity = zones.Where(x => x.VehicleGroup == VehicleType.Motorbike).Sum(x => x.Capacity);

        ParkingLotPublicInfo? info = null;
        if (!string.IsNullOrWhiteSpace(lot.PublicInfoJson))
        {
            try { info = JsonSerializer.Deserialize<ParkingLotPublicInfo>(lot.PublicInfoJson,
                new JsonSerializerOptions(JsonSerializerDefaults.Web)); }
            catch (JsonException) { /* bad legacy metadata must not break the detail route */ }
        }
        return new ParkingLotDetail(lot.Id, lot.Name, lot.Address, lot.Latitude, lot.Longitude,
            lot.Type.ToString(), OpeningHoursSchedule.Deserialize(lot.OpeningHoursJson),
            features, floorCount > 0 ? floorCount : null, floorCount > 0 ? carCapacity : null,
            floorCount > 0 ? motorbikeCapacity : null, info);
    }
}
