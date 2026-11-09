using ParkingSystem.BuildingBlocks.Results;

namespace ParkingSystem.Booking.Application.Vehicles;

/// <summary>
/// Domain and transport error definitions for vehicle ownership verification operations.
/// </summary>
public static class VehicleOwnershipErrors
{
    public static readonly Error EmptyPlate =
        new("booking.vehicle.empty_plate", "The license plate must not be empty.");

    public static readonly Error InvalidPlateFormat =
        new("booking.vehicle.invalid_plate_format", "The license plate format is invalid.");

    public static readonly Error VehicleNotRegistered =
        new("booking.vehicle.not_registered", "Vehicle is not registered in the system.");

    public static readonly Error VehicleOwnershipMismatch =
        new("booking.vehicle.ownership_mismatch", "The vehicle does not belong to the authenticated user.");

    public static readonly Error UnsupportedVehicleType =
        new("booking.vehicle.unsupported_type", "Only cars are supported for parking reservations.");

    public static readonly Error AccountNotEligible =
        new("booking.vehicle.account_not_eligible", "The vehicle owner account is not active or verified.");

    public static readonly Error ServiceUnavailable =
        new("booking.vehicle.service_unavailable", "Identity service is currently unavailable.");

    public static readonly Error DeadlineExceeded =
        new("booking.vehicle.deadline_exceeded", "The request to Identity service timed out.");

    public static readonly Error RequestCancelled =
        new("booking.vehicle.cancelled", "The request to Identity service was cancelled.");

    public static readonly Error CommunicationFailed =
        new("booking.vehicle.communication_failed", "Failed to communicate with Identity service.");

    public static readonly Error ServiceAuthFailed =
        new("booking.vehicle.service_auth_failed", "Service authentication failed with Identity service.");
}

