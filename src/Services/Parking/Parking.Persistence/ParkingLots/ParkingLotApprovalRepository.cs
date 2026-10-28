using Microsoft.EntityFrameworkCore;
using ParkingSystem.Parking.Application.ParkingLots;
using ParkingSystem.Parking.Domain.ParkingLots;

namespace ParkingSystem.Parking.Persistence.ParkingLots;

internal sealed class ParkingLotApprovalRepository(ParkingDbContext dbContext) : IParkingLotApprovalRepository
{
    public Task<ParkingLot?> FindByIdAsync(Guid parkingLotId, CancellationToken cancellationToken) =>
        dbContext.ParkingLots.SingleOrDefaultAsync(lot => lot.Id == parkingLotId, cancellationToken);

    public async Task<bool> TrySaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            // EF Core commits the lot update and its audit row atomically in SaveChanges.
            await dbContext.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateConcurrencyException exception) when (
            exception.Entries.Any(entry => entry.Entity is ParkingLot))
        {
            // ApprovalStatus is configured as a concurrency token, so stale decisions lose.
            return false;
        }
    }
}
