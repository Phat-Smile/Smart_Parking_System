using ParkingSystem.Parking.Domain.ParkingLots;

namespace ParkingSystem.Parking.Application.ParkingLots;

public interface IRegisterParkingLotRepository
{
    Task<bool> ExistsForOwnerAsync(Guid ownerUserId, string name, CancellationToken cancellationToken);

    // The lot and the registrant's Owner assignment are saved together or not at all.
    Task AddAsync(ParkingLot parkingLot, ParkingLotAssignment ownerAssignment, CancellationToken cancellationToken);
}
