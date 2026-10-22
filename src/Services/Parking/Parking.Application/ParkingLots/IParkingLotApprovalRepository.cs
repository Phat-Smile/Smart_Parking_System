using ParkingSystem.Parking.Domain.ParkingLots;

namespace ParkingSystem.Parking.Application.ParkingLots;

public interface IParkingLotApprovalRepository
{
    Task<ParkingLot?> FindByIdAsync(Guid parkingLotId, CancellationToken cancellationToken);
    Task<bool> TrySaveChangesAsync(CancellationToken cancellationToken);
}
