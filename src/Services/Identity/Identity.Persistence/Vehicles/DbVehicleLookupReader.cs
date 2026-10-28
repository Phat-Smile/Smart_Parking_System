using Microsoft.EntityFrameworkCore;
using ParkingSystem.Identity.Application.Vehicles.Lookup;
using ParkingSystem.Identity.Domain.Accounts;

namespace ParkingSystem.Identity.Persistence.Vehicles;

internal sealed class DbVehicleLookupReader(IdentityDbContext dbContext) : IVehicleLookupReader
{
    public async Task<VehicleLookupResult> FindByPlateAsync(
        string normalizedLicensePlate,
        CancellationToken cancellationToken)
    {
        var plate = normalizedLicensePlate.Trim().ToUpperInvariant();

        var row = await dbContext.Vehicles
            .Where(v => v.LicensePlate == plate)
            .Join(
                dbContext.Accounts,
                v => v.AccountId,
                a => a.Id,
                (v, a) => new
                {
                    v.Id,
                    v.AccountId,
                    v.VehicleType,
                    IsAccountActive = a.Status == AccountStatus.EmailVerified
                })
            .FirstOrDefaultAsync(cancellationToken);

        if (row is null)
            return new VehicleLookupResult(false, null, null, null, false);

        var lookupType = (VehicleType)(int)row.VehicleType;

        return new VehicleLookupResult(
            IsRegistered: true,
            AccountId: row.AccountId,
            VehicleId: row.Id,
            VehicleType: lookupType,
            IsAccountActive: row.IsAccountActive);
    }
}
