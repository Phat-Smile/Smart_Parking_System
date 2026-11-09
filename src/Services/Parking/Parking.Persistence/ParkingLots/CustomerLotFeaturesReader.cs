using Microsoft.EntityFrameworkCore;
using ParkingSystem.Parking.Application.ParkingLots.OpeningHours;
using ParkingSystem.Parking.Application.ParkingLots.SearchCustomerLots;
using ParkingSystem.Parking.Domain.ParkingSessions;

namespace ParkingSystem.Parking.Persistence.ParkingLots;

internal sealed class CustomerLotFeaturesReader(ParkingDbContext db) : ICustomerLotFeaturesReader
{
    public async Task<IReadOnlyDictionary<Guid, CustomerLotFeatures>> ReadAsync(IReadOnlyCollection<Guid> lotIds, CancellationToken ct)
    {
        if (lotIds.Count == 0) return new Dictionary<Guid, CustomerLotFeatures>();
        var lots = await db.ParkingLots.AsNoTracking().Where(x => lotIds.Contains(x.Id))
            .Select(x => new { x.Id, x.SupportsElectricVehicles, x.SupportsLargeVehicles,
                x.HasEvCharging, x.IsCovered, x.HasCctv, x.OpeningHoursJson }).ToListAsync(ct);
        var groups = await db.Zones.AsNoTracking().Join(db.Floors.AsNoTracking(),
                zone => zone.FloorId, floor => floor.Id,
                (zone, floor) => new { floor.LotId, zone.VehicleGroup })
            .Where(x => lotIds.Contains(x.LotId)).ToListAsync(ct);
        var policies = await db.ReservationAvailabilityPolicies.AsNoTracking()
            .Where(x => lotIds.Contains(x.LotId)).Select(x => new { x.LotId, x.ReservationEnabled })
            .ToDictionaryAsync(x => x.LotId, x => x.ReservationEnabled, ct);

        return lots.ToDictionary(x => x.Id, x =>
        {
            var car = groups.Any(group => group.LotId == x.Id && group.VehicleGroup == VehicleType.Car);
            var motorbike = groups.Any(group => group.LotId == x.Id && group.VehicleGroup == VehicleType.Motorbike);
            var alwaysOpen = false;
            try
            {
                var hours = OpeningHoursSchedule.Deserialize(x.OpeningHoursJson);
                alwaysOpen = OpeningHoursSchedule.IsValid(hours) && hours!.Days.All(day => day.Is24Hours);
            }
            catch (System.Text.Json.JsonException) { }
            return new CustomerLotFeatures(car, motorbike, x.SupportsElectricVehicles,
                x.SupportsLargeVehicles, x.HasEvCharging, x.IsCovered, x.HasCctv,
                alwaysOpen, (car || motorbike) && (!policies.TryGetValue(x.Id, out var enabled) || enabled));
        });
    }
}
