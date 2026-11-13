using Microsoft.EntityFrameworkCore;
using ParkingSystem.Parking.Application.ParkingLots.PublicInfo;
using ParkingSystem.Parking.Domain.ParkingLots;

namespace ParkingSystem.Parking.Persistence.ParkingLots;

internal sealed class ParkingLotPublicInfoRepository(ParkingDbContext db) : IParkingLotPublicInfoRepository
{
    public Task<ParkingLot?> FindForOwnerAsync(Guid lotId, Guid ownerId, CancellationToken ct) =>
        db.ParkingLots.Where(lot => lot.Id == lotId &&
            db.OwnerAssignments().Any(a => a.LotId == lot.Id && a.AccountId == ownerId))
            .SingleOrDefaultAsync(ct);

    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
