using ParkingSystem.Booking.Application.Parking;
using ParkingSystem.Booking.Application.Reservations;
using ParkingSystem.BuildingBlocks.Results;

namespace ParkingSystem.Booking.WebAPI.Grpc;

/// <summary>
/// Production fail-closed adapter for <see cref="IParkingLotCapacityProvider"/>.
/// Per specification Section 7.4:
/// Parking currently only provides GetAvailableSlotCount via gRPC contract #2.
/// There is no inter-service gRPC contract for ValidCapacity or ReservationRatioPercent yet.
/// Until that contract is finalized, production wiring uses this unconfigured fail-closed adapter.
/// </summary>
public sealed class UnconfiguredParkingLotCapacityProvider : IParkingLotCapacityProvider
{
    public Task<Result<ParkingLotCapacityInfo>> GetCapacityInfoAsync(
        Guid lotId,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Result<ParkingLotCapacityInfo>.Failure(ReservationCapacityErrors.CapacityInfoUnavailable));
    }
}

