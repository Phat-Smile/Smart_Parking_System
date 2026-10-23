using ParkingSystem.Parking.Domain.ParkingLots;

namespace ParkingSystem.Parking.Application.ParkingLots.OpeningHours;

public interface IParkingLotOpeningHoursRepository
{
    Task<ParkingLot?> FindForOwnerAsync(Guid lotId, Guid ownerAccountId, CancellationToken cancellationToken);
    Task<ParkingLot?> FindApprovedAsync(Guid lotId, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
