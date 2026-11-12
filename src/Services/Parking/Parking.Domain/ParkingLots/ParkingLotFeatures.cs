namespace ParkingSystem.Parking.Domain.ParkingLots;

// Owner-declared features become public only after the lot is approved.
public sealed record ParkingLotFeatures(bool SupportsElectricVehicles = false,
    bool SupportsLargeVehicles = false, bool HasEvCharging = false,
    bool IsCovered = false, bool HasCctv = false);
