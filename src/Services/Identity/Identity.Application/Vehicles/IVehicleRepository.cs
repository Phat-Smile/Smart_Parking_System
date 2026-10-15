using ParkingSystem.Identity.Domain.Vehicles;

namespace ParkingSystem.Identity.Application.Vehicles;

public interface IVehicleRepository
{
    void Add(Vehicle vehicle);

    Task<Vehicle?> FindByIdAsync(Guid vehicleId, CancellationToken cancellationToken);

    Task<Vehicle?> FindByAccountAndPlateAsync(Guid accountId, string normalizedPlate, CancellationToken cancellationToken);

    Task<IReadOnlyList<Vehicle>> ListByAccountIdAsync(Guid accountId, CancellationToken cancellationToken);

    Task<bool> PlateExistsAsync(string normalizedPlate, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
