using ParkingSystem.BuildingBlocks.Results;

namespace ParkingSystem.Booking.Application.Parking;

/// <summary>
/// Port abstraction for querying parking lot availability from the Parking service.
/// Defined in Booking.Application to keep the application layer free of gRPC transport types.
/// </summary>
public interface IParkingAvailabilityClient
{
    /// <summary>
    /// Queries the number of available car slots for the specified parking lot.
    /// </summary>
    /// <param name="lotId">The unique identifier of the parking lot.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A Result containing the available slot count if successful, or an Error.</returns>
    Task<Result<int>> GetAvailableSlotCountAsync(Guid lotId, CancellationToken cancellationToken = default);
}
