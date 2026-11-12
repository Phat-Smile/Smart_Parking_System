using Grpc.Core;
using Microsoft.Extensions.Options;
using ParkingSystem.AspNetCore.Security;
using ParkingSystem.Booking.Application.Parking;
using ParkingSystem.Booking.Application.Reservations;
using ParkingSystem.BuildingBlocks.Results;
using SmartParking.Contracts.Parking.Availability.V1;

namespace ParkingSystem.Booking.WebAPI.Grpc;

/// <summary>
/// gRPC adapter for <see cref="IParkingLotCapacityProvider"/>. Fail-closed: any failure, timeout or abnormal figure
/// becomes <see cref="ReservationCapacityErrors.CapacityInfoUnavailable"/>; no default capacity, ratio or deposit is ever invented.
/// </summary>
public sealed class ParkingLotCapacityGrpcProvider(
    ParkingAvailabilityContract.ParkingAvailabilityContractClient client,
    IOptions<ParkingServiceOptions> options,
    IOptions<ServiceKeyOptions> serviceKeyOptions,
    ILogger<ParkingLotCapacityGrpcProvider> logger) : IParkingLotCapacityProvider
{
    public async Task<Result<ParkingLotCapacityInfo>> GetCapacityInfoAsync(
        Guid lotId,
        CancellationToken cancellationToken = default)
    {
        if (lotId == Guid.Empty)
            return Result<ParkingLotCapacityInfo>.Failure(ReservationCapacityErrors.CapacityInfoUnavailable);

        try
        {
            var response = await client.GetLotReservationInfoAsync(
                new GetLotReservationInfoRequest { LotId = lotId.ToString("D") },
                headers: ParkingGrpcCall.Headers(serviceKeyOptions.Value),
                deadline: ParkingGrpcCall.Deadline(options.Value),
                cancellationToken: cancellationToken);

            if (response.ValidCapacity < 0 ||
                response.ReservationRatioPercent is < 0 or > 100 ||
                response.AvailableSlotCount < 0 ||
                response.DepositAmount < 0)
            {
                logger.LogError(
                    "Parking returned abnormal reservation facts for lot {LotId}: capacity {ValidCapacity}, ratio {Ratio}, available {Available}, deposit {Deposit}.",
                    lotId, response.ValidCapacity, response.ReservationRatioPercent, response.AvailableSlotCount, response.DepositAmount);
                return Result<ParkingLotCapacityInfo>.Failure(ReservationCapacityErrors.CapacityInfoUnavailable);
            }

            return Result<ParkingLotCapacityInfo>.Success(new ParkingLotCapacityInfo(
                response.ValidCapacity,
                response.ReservationRatioPercent,
                response.AvailableSlotCount,
                response.ReservationEnabled,
                response.DepositAmount));
        }
        catch (RpcException ex)
        {
            ParkingGrpcCall.LogFailure(logger, ex, lotId);
            return Result<ParkingLotCapacityInfo>.Failure(ReservationCapacityErrors.CapacityInfoUnavailable);
        }
    }
}
