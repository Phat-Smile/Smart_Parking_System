using Microsoft.EntityFrameworkCore;
using ParkingSystem.Parking.Application.ParkingLots;
using ParkingSystem.Parking.Application.ParkingLots.OpeningHours;
using ParkingSystem.Parking.Domain.ParkingLots;
using ParkingSystem.Parking.Domain.ParkingSessions;

namespace ParkingSystem.Parking.Persistence.ParkingLots;

internal sealed class ParkingLotCatalogReader(ParkingDbContext dbContext) : IParkingLotCatalogReader, IParkingLotArrivalSearch
{
    public async Task<IReadOnlyList<ParkingLotCatalogItem>> ListApprovedAsync(
        CancellationToken cancellationToken,
        VehicleType? vehicleType = null) =>
        await dbContext.ParkingLots
            .AsNoTracking()
            .Where(parkingLot => parkingLot.ApprovalStatus == ParkingLotApprovalStatus.Approved)
            // Use correlated EXISTS: multiple matching zones never duplicate a parking lot.
            .Where(parkingLot => !vehicleType.HasValue || dbContext.Floors.Any(floor =>
                floor.LotId == parkingLot.Id && dbContext.Zones.Any(zone =>
                    zone.FloorId == floor.Id && zone.VehicleGroup == vehicleType.Value)))
            .OrderBy(parkingLot => parkingLot.Name)
            .ThenBy(parkingLot => parkingLot.Id)
            .Select(parkingLot => new ParkingLotCatalogItem(
                parkingLot.Id,
                parkingLot.Name,
                parkingLot.Address,
                parkingLot.Latitude,
                parkingLot.Longitude,
                parkingLot.Type.ToString()))
            .ToListAsync(cancellationToken);
    // Filter only when the caller supplies an arrival timestamp; missing ETA is an open product decision.
    public async Task<IReadOnlyList<ParkingLotCatalogItem>> ListApprovedOpenAtAsync(
        DateTimeOffset arrivalAt, CancellationToken cancellationToken, VehicleType? vehicleType = null)
    {
        var candidates = await dbContext.ParkingLots.AsNoTracking()
            .Where(lot => lot.ApprovalStatus == ParkingLotApprovalStatus.Approved)
            .Where(lot => !vehicleType.HasValue || dbContext.Floors.Any(floor =>
                floor.LotId == lot.Id && dbContext.Zones.Any(zone =>
                    zone.FloorId == floor.Id && zone.VehicleGroup == vehicleType.Value)))
            .OrderBy(lot => lot.Name).ThenBy(lot => lot.Id)
            .Select(lot => new
            {
                lot.Id, lot.Name, lot.Address, lot.Latitude, lot.Longitude,
                lot.Type, lot.OpeningHoursJson
            })
            .ToListAsync(cancellationToken);

        return candidates
            .Where(lot => OpeningHoursSchedule.IsOpenAt(lot.OpeningHoursJson, arrivalAt))
            .Select(lot => new ParkingLotCatalogItem(
                lot.Id, lot.Name, lot.Address, lot.Latitude, lot.Longitude, lot.Type.ToString()))
            .ToList();
    }
}
