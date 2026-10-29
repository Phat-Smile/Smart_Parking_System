using ParkingSystem.Identity.Domain.Vehicles;

namespace ParkingSystem.Identity.Application.Vehicles;

public enum SaveVehicleResult
{
    Success,
    DuplicatePlateConflict
}

public interface IVehicleRepository
{
    void Add(Vehicle vehicle);

    void Remove(Vehicle vehicle);

    Task<Vehicle?> FindByIdAsync(Guid vehicleId, CancellationToken cancellationToken);

    Task<Vehicle?> FindByAccountAndPlateAsync(Guid accountId, string normalizedPlate, CancellationToken cancellationToken);

    Task<IReadOnlyList<Vehicle>> ListByAccountIdAsync(Guid accountId, CancellationToken cancellationToken);

    Task<bool> PlateExistsAsync(string normalizedPlate, CancellationToken cancellationToken);

    /// <summary>Saves a new vehicle. Returns DuplicatePlateConflict when the plate index rejects it (two accounts racing for one plate).</summary>
    Task<SaveVehicleResult> SaveNewVehicleAsync(CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
