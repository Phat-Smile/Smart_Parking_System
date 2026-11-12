using Microsoft.EntityFrameworkCore;
using ParkingSystem.Parking.Application.Common.Interfaces;
using ParkingSystem.Parking.Domain.ParkingLots;
using ParkingSystem.Parking.Persistence.ParkingLots;

namespace ParkingSystem.Parking.Persistence.Repositories;

internal sealed class LotOwnershipService(ParkingDbContext dbContext) : ILotOwnershipService
{
    public async Task<bool> LotExistsAsync(Guid lotId) =>
        await dbContext.ParkingLots.AsNoTracking().AnyAsync(lot => lot.Id == lotId);

    public Task<bool> IsLotOwnerAsync(Guid lotId, Guid callerId) => dbContext.IsOwnerAsync(lotId, callerId);

    public async Task<bool> IsLotApprovedAsync(Guid lotId) =>
        await dbContext.ParkingLots.AsNoTracking()
            .AnyAsync(lot => lot.Id == lotId && lot.ApprovalStatus == ParkingLotApprovalStatus.Approved);
}
