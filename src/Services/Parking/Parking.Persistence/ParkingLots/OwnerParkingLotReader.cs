using Microsoft.EntityFrameworkCore;
using ParkingSystem.Parking.Application.ParkingLots.OwnerLots;

namespace ParkingSystem.Parking.Persistence.ParkingLots;

internal sealed class OwnerParkingLotReader(ParkingDbContext dbContext) : IOwnerParkingLotReader
{
    public async Task<IReadOnlyList<OwnerParkingLotItem>> ListByOwnerAsync(
        Guid ownerUserId,
        CancellationToken cancellationToken)
    {
        return await dbContext.ParkingLots
            .AsNoTracking()
            .Where(lot => lot.OwnerUserId == ownerUserId)
            .OrderByDescending(lot => lot.Id)
            .Select(lot => new OwnerParkingLotItem(
                lot.Id,
                lot.Name,
                lot.Address,
                lot.Latitude,
                lot.Longitude,
                lot.Type.ToString(),
                lot.ApprovalStatus.ToString(),
                lot.RejectionReason))
            .ToListAsync(cancellationToken);
    }
}
