using Microsoft.EntityFrameworkCore;
using ParkingSystem.Parking.Application.ParkingLots.AdminLots;
using ParkingSystem.Parking.Domain.ParkingLots;

namespace ParkingSystem.Parking.Persistence.ParkingLots;

internal sealed class AdminParkingLotReader(ParkingDbContext dbContext) : IAdminParkingLotReader
{
    public async Task<IReadOnlyList<AdminParkingLotItem>> ListForAdminAsync(
        ParkingLotApprovalStatus? status,
        CancellationToken cancellationToken)
    {
        return await dbContext.ParkingLots
            .AsNoTracking()
            .Where(lot => !status.HasValue || lot.ApprovalStatus == status.Value)
            .OrderBy(lot => lot.ApprovalStatus == ParkingLotApprovalStatus.Pending ? 0 : 1)
            .ThenBy(lot => lot.Name)
            .ThenBy(lot => lot.Id)
            .Select(lot => new AdminParkingLotItem(
                lot.Id,
                lot.OwnerUserId,
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
