using ParkingSystem.BuildingBlocks.Results;

namespace ParkingSystem.Booking.Application.Vehicles;

/// <summary>
/// Application port for verifying vehicle registration and user ownership against Identity service.
/// </summary>
public interface IVehicleOwnershipClient
{
    /// <summary>
    /// Verifies vehicle registration and ownership for the specified user.
    /// </summary>
    /// <param name="rawOrCanonicalPlate">Vehicle license plate (raw or normalized).</param>
    /// <param name="expectedUserId">The authenticated user's ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// Success with <see cref="VehicleOwnershipInfo"/> when registered, owned by user, active, and supported type;
    /// otherwise failure with a specific <see cref="VehicleOwnershipErrors"/>.
    /// </returns>
    Task<Result<VehicleOwnershipInfo>> VerifyOwnershipAsync(
        string rawOrCanonicalPlate,
        Guid expectedUserId,
        CancellationToken cancellationToken = default);
}

