using Microsoft.EntityFrameworkCore;
using Npgsql;
using ParkingSystem.Identity.Application.Vehicles;
using ParkingSystem.Identity.Domain.Vehicles;

namespace ParkingSystem.Identity.Persistence.Vehicles;

internal sealed class VehicleRepository(IdentityDbContext dbContext) : IVehicleRepository
{
    public void Add(Vehicle vehicle) => dbContext.Vehicles.Add(vehicle);

    public void Remove(Vehicle vehicle) => dbContext.Vehicles.Remove(vehicle);

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

    public async Task<SaveVehicleResult> SaveNewVehicleAsync(CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return SaveVehicleResult.Success;
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException pg
            && pg.SqlState == PostgresErrorCodes.UniqueViolation
            && pg.ConstraintName == VehicleConfiguration.PlateIndexName)
        {
            dbContext.ChangeTracker.Clear();
            return SaveVehicleResult.DuplicatePlateConflict;
        }
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        dbContext.SaveChangesAsync(cancellationToken);
}
