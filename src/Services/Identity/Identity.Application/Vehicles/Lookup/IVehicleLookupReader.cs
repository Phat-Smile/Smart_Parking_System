namespace ParkingSystem.Identity.Application.Vehicles.Lookup;

public interface IVehicleLookupReader
{
    Task<VehicleLookupResult> FindByPlateAsync(
        string normalizedLicensePlate,
        CancellationToken cancellationToken);
}
