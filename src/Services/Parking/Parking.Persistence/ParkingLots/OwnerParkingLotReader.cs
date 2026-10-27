using Microsoft.EntityFrameworkCore;
using ParkingSystem.Parking.Application.Common.Paging;
using ParkingSystem.Parking.Application.ParkingLots.OwnerLots;
using ParkingSystem.Parking.Domain.ParkingLots;

namespace ParkingSystem.Parking.Persistence.ParkingLots;

internal sealed class OwnerParkingLotReader(ParkingDbContext dbContext) : IOwnerParkingLotReader
{
    public async Task<PagedResult<OwnerParkingLotItem>> ListByOwnerAsync(
        Guid ownerUserId,
        ParkingLotApprovalStatus? status,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        // "Owner of a lot" is an Owner row in parking_lot_assignments (LotOwnership, MOD-PK-05). owner_user_id only records
        // who registered the lot: a co-owner added by an admin must see the lot, a removed owner must not.
        var lots = dbContext.OwnerAssignments()
            .AsNoTracking()
            .Where(assignment => assignment.AccountId == ownerUserId)
            .Join(dbContext.ParkingLots.AsNoTracking(), assignment => assignment.LotId, lot => lot.Id, (_, lot) => lot)
            .Where(lot => !status.HasValue || lot.ApprovalStatus == status.Value);

        var totalCount = await lots.CountAsync(cancellationToken);

        // Name then Id is a total order, so a lot is never repeated or skipped between two pages.
        var items = await lots
            .OrderBy(lot => lot.Name)
            .ThenBy(lot => lot.Id)
            .Skip(Paging.Skip(page, pageSize))
            .Take(pageSize)
            .Select(lot => new OwnerParkingLotItem(
                lot.Id,
                lot.Name,
                lot.Address,
                lot.Latitude,
                lot.Longitude,
                lot.Type.ToString(),
                lot.ApprovalStatus.ToString(),
                lot.RejectionReason,
                lot.OpeningHoursJson != null,
                dbContext.PricingPolicies.Any(policy => policy.LotId == lot.Id)))
            .ToListAsync(cancellationToken);

        return new PagedResult<OwnerParkingLotItem>(items, totalCount, page, pageSize);
    }
}
