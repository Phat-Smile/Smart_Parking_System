using Microsoft.EntityFrameworkCore;
using ParkingSystem.Parking.Application.ParkingLots.OwnerLots;

namespace ParkingSystem.Parking.Persistence.ParkingLots;

internal sealed class OwnerParkingLotReader(ParkingDbContext dbContext) : IOwnerParkingLotReader
{
    public async Task<IReadOnlyList<OwnerParkingLotItem>> ListByOwnerAsync(
        Guid ownerUserId,
        CancellationToken cancellationToken)
    {
        // "Owner of a lot" is an Owner row in parking_lot_assignments (LotOwnership, MOD-PK-05). owner_user_id only records
        // who registered the lot: a co-owner added by an admin must see the lot, a removed owner must not.
        return await dbContext.OwnerAssignments()
            .AsNoTracking()
            .Where(assignment => assignment.AccountId == ownerUserId)
            .Join(dbContext.ParkingLots.AsNoTracking(), assignment => assignment.LotId, lot => lot.Id, (_, lot) => lot)
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
