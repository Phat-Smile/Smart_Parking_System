using Grpc.Core;
using Microsoft.Extensions.Options;
using ParkingSystem.AspNetCore.Security;
using ParkingSystem.Booking.Application.Parking;
using ParkingSystem.Booking.Application.Reservations;
using ParkingSystem.Booking.Domain.Reservations;
using ParkingSystem.BuildingBlocks.Results;
using SmartParking.Contracts.Parking.Availability.V1;

namespace ParkingSystem.Booking.WebAPI.Grpc;

/// <summary>
/// gRPC adapter for <see cref="IParkingPricingProvider"/>. Fail-closed: no price list, a failed call or an abnormal
/// figure becomes <see cref="ReservationCapacityErrors.CapacityInfoUnavailable"/>; a price is never guessed.
/// </summary>
public sealed class ParkingPricingGrpcProvider(
    ParkingAvailabilityContract.ParkingAvailabilityContractClient client,
    IOptions<ParkingServiceOptions> options,
    IOptions<ServiceKeyOptions> serviceKeyOptions,
    ILogger<ParkingPricingGrpcProvider> logger) : IParkingPricingProvider
{
    public async Task<Result<ParkingPricingInfo>> GetPricingInfoAsync(
        Guid lotId,
        VehicleGroup vehicleGroup,
        CancellationToken cancellationToken = default)
    {
        if (lotId == Guid.Empty)
            return Result<ParkingPricingInfo>.Failure(ReservationCapacityErrors.CapacityInfoUnavailable);

        try
        {
            var response = await client.GetLotPricingAsync(
                new GetLotPricingRequest { LotId = lotId.ToString("D"), VehicleGroup = vehicleGroup.ToString() },
                headers: ParkingGrpcCall.Headers(serviceKeyOptions.Value),
                deadline: ParkingGrpcCall.Deadline(options.Value),
                cancellationToken: cancellationToken);

            var knownMode = response.BillingMode is ScheduledPrepaymentCalculator.BillingModeBlock
                or ScheduledPrepaymentCalculator.BillingModePerEntry;
            if (response.Price <= 0 || response.OvernightFee < 0 || response.BlockMinutes < 0 || !knownMode)
            {
                logger.LogError(
                    "Parking returned an abnormal price list for lot {LotId}: price {Price}, mode {Mode}, block {Block}, overnight {Overnight}.",
                    lotId, response.Price, response.BillingMode, response.BlockMinutes, response.OvernightFee);
                return Result<ParkingPricingInfo>.Failure(ReservationCapacityErrors.CapacityInfoUnavailable);
            }

            return Result<ParkingPricingInfo>.Success(new ParkingPricingInfo(
                lotId,
                vehicleGroup,
                response.Price,
                response.BillingMode,
                response.BlockMinutes > 0 ? response.BlockMinutes : null,
                response.OvernightFee,
                "VND",
                OvernightTimeMark: null,
                PolicyVersion: response.PolicyVersion));
        }
        catch (RpcException ex)
        {
            ParkingGrpcCall.LogFailure(logger, ex, lotId);
            return Result<ParkingPricingInfo>.Failure(ReservationCapacityErrors.CapacityInfoUnavailable);
        }
    }
}
