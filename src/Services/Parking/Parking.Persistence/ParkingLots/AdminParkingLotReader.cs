using Microsoft.EntityFrameworkCore;
using ParkingSystem.Parking.Application.Common.Paging;
using ParkingSystem.Parking.Application.ParkingLots.AdminLots;
using ParkingSystem.Parking.Domain.ParkingLots;

namespace ParkingSystem.Parking.Persistence.ParkingLots;

internal sealed class AdminParkingLotReader(ParkingDbContext dbContext) : IAdminParkingLotReader
{
    public async Task<PagedResult<AdminParkingLotItem>> ListForAdminAsync(
        ParkingLotApprovalStatus? status,
        DateTimeOffset? fromDate,
        DateTimeOffset? toDate,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var lots = dbContext.ParkingLots
            .AsNoTracking()
            .Where(lot => !status.HasValue || lot.ApprovalStatus == status.Value);

        if (fromDate.HasValue)
            lots = lots.Where(lot => lot.CreatedAt >= fromDate.Value.UtcDateTime);

        if (toDate.HasValue)
            lots = lots.Where(lot => lot.CreatedAt <= toDate.Value.UtcDateTime);

        var totalCount = await lots.CountAsync(cancellationToken);

        // The review queue first (Pending), then by name; Id makes the order total so pages never overlap.
        var items = await lots
            .OrderBy(lot => lot.ApprovalStatus == ParkingLotApprovalStatus.Pending ? 0 : 1)
            .ThenBy(lot => lot.Name)
            .ThenBy(lot => lot.Id)
            .Skip(Paging.Skip(page, pageSize))
            .Take(pageSize)
            .Select(lot => new AdminParkingLotItem(
                lot.Id,
                lot.OwnerUserId,
                lot.Name,
                lot.Address,
                lot.Latitude,
                lot.Longitude,
                lot.Type.ToString(),
                lot.ApprovalStatus.ToString(),
                lot.RejectionReason,
                lot.CreatedAt))
            .ToListAsync(cancellationToken);

        return new PagedResult<AdminParkingLotItem>(items, totalCount, page, pageSize);
    }
}
