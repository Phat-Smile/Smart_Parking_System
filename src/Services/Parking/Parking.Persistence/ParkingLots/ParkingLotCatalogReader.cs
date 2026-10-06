using Microsoft.EntityFrameworkCore;
using ParkingSystem.Parking.Application.ParkingLots;
using ParkingSystem.Parking.Domain.ParkingLots;

namespace ParkingSystem.Parking.Persistence.ParkingLots;

internal sealed class ParkingLotCatalogReader(ParkingDbContext dbContext) : IParkingLotCatalogReader
{
    public async Task<IReadOnlyList<ParkingLotCatalogItem>> ListApprovedAsync(CancellationToken cancellationToken) =>
        await dbContext.ParkingLots
            .AsNoTracking()
            .Where(parkingLot => parkingLot.ApprovalStatus == ParkingLotApprovalStatus.Approved)
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
}
