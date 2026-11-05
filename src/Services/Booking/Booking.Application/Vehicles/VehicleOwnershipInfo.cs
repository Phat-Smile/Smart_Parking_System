namespace ParkingSystem.Booking.Application.Vehicles;

/// <summary>
/// Verified vehicle ownership details returned by <see cref="IVehicleOwnershipClient"/>.
/// </summary>
public sealed record VehicleOwnershipInfo(
    Guid AccountId,
    Guid VehicleId,
    string CanonicalPlate,
    SupportedVehicleType VehicleType,
    bool IsAccountActive);

