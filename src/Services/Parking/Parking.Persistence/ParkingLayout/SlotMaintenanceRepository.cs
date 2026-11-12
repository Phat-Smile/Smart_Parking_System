using Microsoft.EntityFrameworkCore;
using ParkingSystem.Parking.Application.ParkingLayout.SetSlotMaintenance;
using ParkingSystem.Parking.Domain.ParkingLayout;
using ParkingSystem.Parking.Domain.ParkingLots;

namespace ParkingSystem.Parking.Persistence.ParkingLayout;

public sealed class SlotMaintenanceRepository(ParkingDbContext dbContext) : ISlotMaintenanceRepository
{
    public async Task<bool> CanManageLotAsync(Guid lotId, Guid callerAccountId, string callerRole, CancellationToken cancellationToken)
    {
        if (string.Equals(callerRole, "Admin", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return await dbContext.ParkingLotAssignments.AsNoTracking().AnyAsync(assignment =>
            assignment.LotId == lotId &&
            assignment.AccountId == callerAccountId &&
            (assignment.Role == ParkingLotMemberRole.Owner || assignment.Role == ParkingLotMemberRole.Staff),
            cancellationToken);
    }

    public async Task<ParkingSlot?> FindSlotAsync(Guid lotId, Guid slotId, CancellationToken cancellationToken)
    {
        return await dbContext.ParkingSlots
            .FirstOrDefaultAsync(s => s.LotId == lotId && s.Id == slotId, cancellationToken);
    }

    public async Task<int> GetAvailableCarSlotsAsync(Guid lotId, CancellationToken cancellationToken)
    {
        return await dbContext.ParkingSlots.AsNoTracking()
            .CountAsync(s => s.LotId == lotId && s.EffectiveStatus == SlotEffectiveStatus.Available, cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
