using ParkingSystem.Parking.Domain.ParkingLots;

namespace ParkingSystem.Parking.Application.ParkingLots;

public interface IRegisterParkingLotRepository
{
    Task<bool> ExistsForOwnerAsync(Guid ownerUserId, string name, CancellationToken cancellationToken);

    Task AddAsync(ParkingLot parkingLot, CancellationToken cancellationToken);
}
