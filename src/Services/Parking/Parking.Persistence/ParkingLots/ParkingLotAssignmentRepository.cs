using Microsoft.EntityFrameworkCore;
using ParkingSystem.BuildingBlocks.Identity;
using ParkingSystem.Parking.Application.ParkingLots;
using ParkingSystem.Parking.Application.ParkingLots.ListStaffLots;
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

    public async Task<IReadOnlyList<StaffLot>> ListApprovedLotsOfMemberAsync(Guid accountId, CancellationToken cancellationToken) =>
        await dbContext.ParkingLotAssignments.AsNoTracking()
            .Where(assignment => assignment.AccountId == accountId)
            .Join(
                dbContext.ParkingLots.Where(lot => lot.ApprovalStatus == ParkingLotApprovalStatus.Approved),
                assignment => assignment.LotId,
                lot => lot.Id,
                (_, lot) => lot)
            .OrderBy(lot => lot.Name)
            .Select(lot => new StaffLot(lot.Id, lot.Name, lot.Address, lot.Type.ToString()))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<ParkingLotAssignment>> ListByLotAsync(Guid lotId, CancellationToken cancellationToken) =>
        await dbContext.ParkingLotAssignments.AsNoTracking()
            .Where(assignment => assignment.LotId == lotId)
            .OrderBy(assignment => assignment.AssignedAtUtc)
            .ToListAsync(cancellationToken);

    public void Add(ParkingLotAssignment assignment) => dbContext.ParkingLotAssignments.Add(assignment);

    public void Remove(ParkingLotAssignment assignment) => dbContext.ParkingLotAssignments.Remove(assignment);

    public Task SaveChangesAsync(CancellationToken cancellationToken) => dbContext.SaveChangesAsync(cancellationToken);
}
