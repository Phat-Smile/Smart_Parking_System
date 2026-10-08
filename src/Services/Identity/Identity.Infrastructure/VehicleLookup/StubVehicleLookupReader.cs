using ParkingSystem.Identity.Application.Vehicles.Lookup;

namespace ParkingSystem.Identity.Infrastructure.VehicleLookup;

public sealed class StubVehicleLookupReader : IVehicleLookupReader
{
    private static readonly Dictionary<string, VehicleLookupResult> DeterministicRegistry = new(StringComparer.OrdinalIgnoreCase)
    {
        ["51A12345"] = new VehicleLookupResult(
            IsRegistered: true,
            AccountId: Guid.Parse("10000000-0000-0000-0000-000000000001"),
            VehicleId: Guid.Parse("20000000-0000-0000-0000-000000000001"),
            OwnerFullName: "Nguyễn Văn An",
            VehicleType: VehicleType.Car,
            IsAccountActive: true),

        ["59X112345"] = new VehicleLookupResult(
            IsRegistered: true,
            AccountId: Guid.Parse("10000000-0000-0000-0000-000000000002"),
            VehicleId: Guid.Parse("20000000-0000-0000-0000-000000000002"),
            OwnerFullName: "Trần Thị Bình",
            VehicleType: VehicleType.Motorbike,
            IsAccountActive: true)
    };

    public Task<VehicleLookupResult> FindByPlateAsync(
        string normalizedLicensePlate,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (DeterministicRegistry.TryGetValue(normalizedLicensePlate, out var registeredVehicle))
        {
            return Task.FromResult(registeredVehicle);
        }

        return Task.FromResult(new VehicleLookupResult(
            IsRegistered: false,
            AccountId: null,
            VehicleId: null,
            OwnerFullName: null,
            VehicleType: null,
            IsAccountActive: false));
    }
}
