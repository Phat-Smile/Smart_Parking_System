using Microsoft.EntityFrameworkCore;
using ParkingSystem.Parking.Application.ParkingLots.OpeningHours;
using ParkingSystem.Parking.Domain.ParkingLots;

namespace ParkingSystem.Parking.Persistence.ParkingLots;

internal sealed class ParkingLotOpeningHoursRepository(ParkingDbContext dbContext) : IParkingLotOpeningHoursRepository
{
    public Task<ParkingLot?> FindForOwnerAsync(Guid lotId, Guid ownerAccountId, CancellationToken cancellationToken) =>
        dbContext.ParkingLots
            .Where(lot => lot.Id == lotId && dbContext.OwnerAssignments().Any(assignment =>
                assignment.LotId == lot.Id && assignment.AccountId == ownerAccountId))
            .SingleOrDefaultAsync(cancellationToken);

    public Task<ParkingLot?> FindApprovedAsync(Guid lotId, CancellationToken cancellationToken) =>
        dbContext.ParkingLots.AsNoTracking()
            .SingleOrDefaultAsync(lot => lot.Id == lotId && lot.ApprovalStatus == ParkingLotApprovalStatus.Approved,
                cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken) => dbContext.SaveChangesAsync(cancellationToken);
}
