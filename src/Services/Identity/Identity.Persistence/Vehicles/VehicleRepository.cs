using Microsoft.EntityFrameworkCore;
using ParkingSystem.Identity.Application.Vehicles;
using ParkingSystem.Identity.Domain.Vehicles;

namespace ParkingSystem.Identity.Persistence.Vehicles;

internal sealed class VehicleRepository(IdentityDbContext dbContext) : IVehicleRepository
{
    public void Add(Vehicle vehicle) => dbContext.Vehicles.Add(vehicle);

    public Task<Vehicle?> FindByIdAsync(Guid vehicleId, CancellationToken cancellationToken) =>
        dbContext.Vehicles.FirstOrDefaultAsync(v => v.Id == vehicleId, cancellationToken);

    public Task<Vehicle?> FindByAccountAndPlateAsync(Guid accountId, string normalizedPlate, CancellationToken cancellationToken) =>
        dbContext.Vehicles.FirstOrDefaultAsync(
            v => v.AccountId == accountId && v.LicensePlate == normalizedPlate,
            cancellationToken);

    public async Task<IReadOnlyList<Vehicle>> ListByAccountIdAsync(Guid accountId, CancellationToken cancellationToken) =>
        await dbContext.Vehicles
            .Where(v => v.AccountId == accountId)
            .OrderBy(v => v.CreatedAtUtc)
            .ToListAsync(cancellationToken);

    public Task<bool> PlateExistsAsync(string normalizedPlate, CancellationToken cancellationToken) =>
        dbContext.Vehicles.AnyAsync(v => v.LicensePlate == normalizedPlate, cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        dbContext.SaveChangesAsync(cancellationToken);
}
