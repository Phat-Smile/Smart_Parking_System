using ParkingSystem.Parking.Domain.ParkingLayout;

namespace ParkingSystem.Parking.Application.ParkingLayout.ProcessSlotSensorEvent;

public interface ISlotSensorRepository
{
    Task<ParkingSlot?> FindBySlotCodeAsync(Guid lotId, string slotCode, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
