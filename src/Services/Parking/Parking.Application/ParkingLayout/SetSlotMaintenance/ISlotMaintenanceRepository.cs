using ParkingSystem.Parking.Domain.ParkingLayout;

namespace ParkingSystem.Parking.Application.ParkingLayout.SetSlotMaintenance;

public interface ISlotMaintenanceRepository
{
    Task<bool> CanManageLotAsync(Guid lotId, Guid callerAccountId, string callerRole, CancellationToken cancellationToken);
    Task<ParkingSlot?> FindSlotAsync(Guid lotId, Guid slotId, CancellationToken cancellationToken);
    Task<int> GetAvailableCarSlotsAsync(Guid lotId, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
