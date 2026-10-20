using Microsoft.EntityFrameworkCore;
using ParkingSystem.BuildingBlocks.Identity;
using ParkingSystem.Parking.Application.ParkingLots;
using ParkingSystem.Parking.Domain.ParkingLots;

namespace ParkingSystem.Parking.Persistence.ParkingLots;

internal sealed class ParkingLotAssignmentRepository(ParkingDbContext dbContext) : IParkingLotAssignmentRepository, IParkingLotAccess
{
    // Tra lại DB mỗi request nên gỡ phân công có hiệu lực ngay, không phải chờ token hết hạn.
    public Task<bool> CanOperateAsync(Guid accountId, Guid lotId, CancellationToken cancellationToken) =>
        dbContext.ParkingLotAssignments.AsNoTracking()
            .AnyAsync(assignment => assignment.LotId == lotId && assignment.AccountId == accountId, cancellationToken);

    public Task<bool> LotExistsAsync(Guid lotId, CancellationToken cancellationToken) =>
        dbContext.ParkingLots.AsNoTracking().AnyAsync(lot => lot.Id == lotId, cancellationToken);

    public Task<ParkingLotAssignment?> FindAsync(Guid lotId, Guid accountId, CancellationToken cancellationToken) =>
        dbContext.ParkingLotAssignments.SingleOrDefaultAsync(
            assignment => assignment.LotId == lotId && assignment.AccountId == accountId, cancellationToken);

    public void Add(ParkingLotAssignment assignment) => dbContext.ParkingLotAssignments.Add(assignment);

    public void Remove(ParkingLotAssignment assignment) => dbContext.ParkingLotAssignments.Remove(assignment);

    public Task SaveChangesAsync(CancellationToken cancellationToken) => dbContext.SaveChangesAsync(cancellationToken);
}
