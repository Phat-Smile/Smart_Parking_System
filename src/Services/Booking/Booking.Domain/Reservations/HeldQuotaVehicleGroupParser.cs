namespace ParkingSystem.Booking.Domain.Reservations;

/// <summary>
/// Validates and resolves the vehicleGroup query parameter for the internal held-quota endpoint.
/// Per US-RES-009 / BR-01:
/// - Defaults to "Car" when omitted, null, or whitespace.
/// - Case-insensitive match for "Car" and "Motorbike".
/// - Any other value (e.g. "Truck", "Van") is rejected as invalid.
/// </summary>
public static class HeldQuotaVehicleGroupParser
{
    public const string DefaultVehicleGroup = "Car";
    public const string MotorbikeVehicleGroup = "Motorbike";

    public static bool TryParse(string? input, out string resolvedGroup)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            resolvedGroup = DefaultVehicleGroup;
            return true;
        }

        if (string.Equals(input, nameof(VehicleGroup.Car), StringComparison.OrdinalIgnoreCase))
        {
            resolvedGroup = nameof(VehicleGroup.Car);
            return true;
        }

        if (string.Equals(input, nameof(VehicleGroup.Motorbike), StringComparison.OrdinalIgnoreCase))
        {
            resolvedGroup = nameof(VehicleGroup.Motorbike);
            return true;
        }

        resolvedGroup = string.Empty;
        return false;
    }
}

