using Microsoft.EntityFrameworkCore;
using ParkingSystem.Parking.Application.ParkingLots.SearchCustomerLots;
using ParkingSystem.Parking.Domain.ParkingLayout;
using ParkingSystem.Parking.Domain.ParkingSessions;

namespace ParkingSystem.Parking.Persistence.ParkingLots;

internal sealed class CustomerLotMetricsReader(ParkingDbContext db) : ICustomerLotMetricsReader
{
    public async Task<IReadOnlyDictionary<Guid, CustomerLotMetrics>> ReadAsync(
        IReadOnlyCollection<Guid> lotIds, CancellationToken ct)
    {
        if (lotIds.Count == 0) return new Dictionary<Guid, CustomerLotMetrics>();

        var carCounts = await (from slot in db.ParkingSlots.AsNoTracking()
            join zone in db.Zones.AsNoTracking() on slot.ZoneId equals zone.Id
            where lotIds.Contains(slot.LotId)
                && slot.EffectiveStatus == SlotEffectiveStatus.Available
                && zone.VehicleGroup == VehicleType.Car
            group slot by slot.LotId into lots
            select new { LotId = lots.Key, Count = lots.Count() }).ToListAsync(ct);

        var motorbikeZones = await (from zone in db.Zones.AsNoTracking()
            join floor in db.Floors.AsNoTracking() on zone.FloorId equals floor.Id
            where lotIds.Contains(floor.LotId) && zone.VehicleGroup == VehicleType.Motorbike
            select new { floor.LotId, zone.Capacity, zone.CurrentVehicleCount }).ToListAsync(ct);

        var policies = await db.PricingPolicies.AsNoTracking()
            .Where(x => lotIds.Contains(x.LotId) &&
                (x.VehicleType == "CAR" || x.VehicleType == "MOTORBIKE"))
            .Select(x => new { x.LotId, x.VehicleType, x.Version, x.Price, x.BillingMode, x.BlockMinutes })
            .ToListAsync(ct);

        var carsByLot = carCounts.ToDictionary(x => x.LotId, x => x.Count);
        var bikesByLot = motorbikeZones.GroupBy(x => x.LotId)
            .ToDictionary(x => x.Key, x => x.Sum(zone => Math.Max(0, zone.Capacity - zone.CurrentVehicleCount)));
        var pricesByLot = policies.GroupBy(x => x.LotId).ToDictionary(x => x.Key,
            x => (IReadOnlyList<CustomerLotPrice>)x.GroupBy(policy => policy.VehicleType)
                .Select(type => type.OrderByDescending(policy => policy.Version).First())
                .Select(policy => new CustomerLotPrice(policy.VehicleType, policy.Price, policy.BillingMode,
                    policy.BlockMinutes))
                .ToList());

        return lotIds.Distinct().ToDictionary(id => id, id => new CustomerLotMetrics(
            carsByLot.GetValueOrDefault(id), bikesByLot.GetValueOrDefault(id),
            pricesByLot.GetValueOrDefault(id) ?? []));
    }
}
