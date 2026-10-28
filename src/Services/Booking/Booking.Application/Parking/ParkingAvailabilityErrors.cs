using ParkingSystem.BuildingBlocks.Results;

namespace ParkingSystem.Booking.Application.Parking;

/// <summary>
/// Domain and transport error constants for parking availability operations.
/// </summary>
public static class ParkingAvailabilityErrors
{
    public static readonly Error EmptyLotId =
        new("booking.parking.empty_lot_id", "Lot ID must not be empty.");

    public static readonly Error LotNotFound =
        new("booking.parking.lot_not_found", "The parking lot does not exist or is not approved.");

    public static readonly Error ServiceUnavailable =
        new("booking.parking.service_unavailable", "Parking service is currently unavailable.");

    public static readonly Error DeadlineExceeded =
        new("booking.parking.deadline_exceeded", "The request to Parking service timed out.");

    public static readonly Error RequestCancelled =
        new("booking.parking.cancelled", "The request to Parking service was cancelled.");

    public static readonly Error InvalidResponse =
        new("booking.parking.invalid_response", "Parking service returned an invalid available slot count.");

    public static readonly Error CommunicationFailed =
        new("booking.parking.communication_failed", "Failed to communicate with Parking service.");
}
