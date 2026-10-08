namespace ParkingSystem.Identity.Application.Vehicles.Lookup;

public sealed record VehicleLookupResult(
    bool IsRegistered,
    Guid? AccountId,
    Guid? VehicleId,
    string? OwnerFullName,
    VehicleType? VehicleType,
    bool IsAccountActive);
