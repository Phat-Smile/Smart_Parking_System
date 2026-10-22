using Microsoft.EntityFrameworkCore;
using ParkingSystem.Parking.Application.Common.Interfaces;

namespace ParkingSystem.Parking.Persistence.Repositories;

internal sealed class LotOwnershipService(ParkingDbContext dbContext) : ILotOwnershipService
{
    public async Task<bool> LotExistsAsync(Guid lotId) =>
        await dbContext.ParkingLots.AsNoTracking().AnyAsync(lot => lot.Id == lotId);

    public async Task<bool> IsLotOwnerAsync(Guid lotId, Guid callerId) =>
        await dbContext.ParkingLots.AsNoTracking().AnyAsync(lot =>
            lot.Id == lotId && (lot.OwnerUserId == callerId ||
                                dbContext.ParkingLotAssignments.Any(a => a.LotId == lotId && a.AccountId == callerId)));
}
