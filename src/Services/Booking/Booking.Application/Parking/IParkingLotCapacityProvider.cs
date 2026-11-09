using ParkingSystem.BuildingBlocks.Results;

namespace ParkingSystem.Booking.Application.Parking;

/// <summary>
/// Data transfer object conveying capacity and ratio details from Parking service.
/// </summary>
public sealed record ParkingLotCapacityInfo(
    int ValidCapacity,
    int ReservationRatioPercent,
    int AvailableSlotCount,
    bool ReservationEnabled,
    decimal DepositAmount);

/// <summary>
/// Port abstraction owned by Booking to query parking lot capacity details (valid capacity, ratio, available slots)
/// from the Parking service.
/// </summary>
public interface IParkingLotCapacityProvider
{
    Task<Result<ParkingLotCapacityInfo>> GetCapacityInfoAsync(Guid lotId, CancellationToken cancellationToken = default);
}

