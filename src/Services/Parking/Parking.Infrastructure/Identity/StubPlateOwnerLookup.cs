using ParkingSystem.Parking.Application.Common.Identity;

namespace ParkingSystem.Parking.Infrastructure.Identity;

// Temporary until contract 1 (plate -> account, owner BE-1) exists. Every plate is treated as not linked (BR-05.e).
internal sealed class StubPlateOwnerLookup : IPlateOwnerLookup
{
    public Task<PlateOwner?> FindByPlateAsync(string normalizedPlate, CancellationToken cancellationToken) =>
        Task.FromResult<PlateOwner?>(null);
}
