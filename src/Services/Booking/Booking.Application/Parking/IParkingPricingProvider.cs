using ParkingSystem.Booking.Application.Reservations;
using ParkingSystem.Booking.Domain.Reservations;
using ParkingSystem.BuildingBlocks.Results;

namespace ParkingSystem.Booking.Application.Parking;

/// <summary>
/// Authoritative parking pricing information returned by the Parking service (Section 3.5, BR-10.a).
/// </summary>
public sealed record ParkingPricingInfo(
    Guid LotId,
    VehicleGroup VehicleGroup,
    decimal Price,
    string BillingMode,
    int? BlockMinutes,
    decimal OvernightFee,
    string Currency = "VND",
    TimeSpan? OvernightTimeMark = null,
    int? PolicyVersion = null);

/// <summary>
/// Port abstraction owned by Booking to query parking pricing from Parking service.
/// </summary>
public interface IParkingPricingProvider
{
    Task<Result<ParkingPricingInfo>> GetPricingInfoAsync(
        Guid lotId,
        VehicleGroup vehicleGroup,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Default fail-closed provider used when remote pricing service integration is not configured.
/// </summary>
public sealed class UnconfiguredParkingPricingProvider : IParkingPricingProvider
{
    public Task<Result<ParkingPricingInfo>> GetPricingInfoAsync(
        Guid lotId,
        VehicleGroup vehicleGroup,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Result<ParkingPricingInfo>.Failure(ReservationCapacityErrors.CapacityInfoUnavailable));
    }
}
