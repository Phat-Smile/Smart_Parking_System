using Microsoft.EntityFrameworkCore;
using ParkingSystem.Parking.Application.ParkingLayout.ProcessSlotSensorEvent;
using ParkingSystem.Parking.Domain.ParkingLayout;

namespace ParkingSystem.Parking.Persistence.ParkingLayout;

internal sealed class SlotSensorRepository(ParkingDbContext dbContext) : ISlotSensorRepository
{
    public async Task<ParkingSlot?> FindBySlotCodeAsync(Guid lotId, string slotCode, CancellationToken cancellationToken)
    {
        var normalized = ParkingSlot.NormalizeSlotCode(slotCode);
        return await dbContext.ParkingSlots
            .FirstOrDefaultAsync(s => s.LotId == lotId && s.SlotCode == normalized, cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken) => dbContext.SaveChangesAsync(cancellationToken);
}
