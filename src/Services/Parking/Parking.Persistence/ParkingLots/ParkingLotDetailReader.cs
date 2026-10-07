using Microsoft.EntityFrameworkCore;
using ParkingSystem.Parking.Application.ParkingLots;
using ParkingSystem.Parking.Domain.ParkingLots;

namespace ParkingSystem.Parking.Persistence.ParkingLots;

internal sealed class ParkingLotDetailReader(ParkingDbContext dbContext) : IParkingLotDetailReader
{
    public Task<ParkingLotDetail?> GetApprovedByIdAsync(Guid parkingLotId, CancellationToken cancellationToken) =>
        dbContext.ParkingLots
            .AsNoTracking()
            .Where(parkingLot =>
                parkingLot.Id == parkingLotId &&
                parkingLot.ApprovalStatus == ParkingLotApprovalStatus.Approved)
            .Select(parkingLot => new ParkingLotDetail(
                parkingLot.Id,
                parkingLot.Name,
                parkingLot.Address,
                parkingLot.Latitude,
                parkingLot.Longitude,
                parkingLot.Type.ToString()))
            .SingleOrDefaultAsync(cancellationToken);
}
