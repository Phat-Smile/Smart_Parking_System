using Grpc.Core;
using MediatR;
using ParkingSystem.Parking.Application.ParkingLayout.GetAvailableSlotCount;
using ParkingSystem.Parking.Application.ParkingLots.ReservationInfo;
using SmartParking.Contracts.Parking.Availability.V1;

namespace ParkingSystem.Parking.WebAPI.Grpc;

// Contract 2, Parking part: Booking (BE-3) asks how many car slots of a lot are Available.
public sealed class ParkingAvailabilityGrpcService(
    ISender sender,
    ILogger<ParkingAvailabilityGrpcService> logger) : ParkingAvailabilityContract.ParkingAvailabilityContractBase
{
    public override async Task<GetAvailableSlotCountResponse> GetAvailableSlotCount(
        GetAvailableSlotCountRequest request,
        ServerCallContext context)
    {
        if (!Guid.TryParseExact(request.LotId, "D", out var lotId))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "lot_id must be a UUID."));

        try
        {
            var result = await sender.Send(new GetAvailableSlotCountQuery(lotId), context.CancellationToken);
            if (result.IsFailure)
                throw new RpcException(new Status(StatusCode.NotFound, result.Error!.Message));

            return new GetAvailableSlotCountResponse { AvailableSlotCount = result.Value };
        }
        catch (OperationCanceledException) when (context.CancellationToken.IsCancellationRequested)
        {
            // A caller whose deadline passed already sees DEADLINE_EXCEEDED from its own client-side timer.
            throw new RpcException(new Status(StatusCode.Cancelled, "Request was cancelled."));
        }
        catch (Exception exception) when (exception is not RpcException)
        {
            logger.LogError(exception, "Available slot count failed unexpectedly.");
            throw new RpcException(new Status(StatusCode.Internal, "Available slot count failed."));
        }
    }

    // P0-A: lot facts Booking needs to create a reservation hold.
    public override async Task<GetLotReservationInfoResponse> GetLotReservationInfo(
        GetLotReservationInfoRequest request,
        ServerCallContext context)
    {
        if (!Guid.TryParseExact(request.LotId, "D", out var lotId))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "lot_id must be a UUID."));

        try
        {
            var result = await sender.Send(new GetLotReservationInfoQuery(lotId), context.CancellationToken);
            if (result.IsFailure)
            {
                var code = result.Error == LotReservationFactsErrors.LotNotFound
                    ? StatusCode.NotFound
                    : StatusCode.FailedPrecondition;
                throw new RpcException(new Status(code, result.Error!.Message));
            }

            var info = result.Value!;
            return new GetLotReservationInfoResponse
            {
                ValidCapacity = info.ValidCapacity,
                ReservationRatioPercent = info.ReservationRatioPercent,
                AvailableSlotCount = info.AvailableSlotCount,
                ReservationEnabled = info.ReservationEnabled,
                DepositAmount = info.DepositAmount
            };
        }
        catch (OperationCanceledException) when (context.CancellationToken.IsCancellationRequested)
        {
            throw new RpcException(new Status(StatusCode.Cancelled, "Request was cancelled."));
        }
        catch (Exception exception) when (exception is not RpcException)
        {
            logger.LogError(exception, "Lot reservation info failed unexpectedly.");
            throw new RpcException(new Status(StatusCode.Internal, "Lot reservation info failed."));
        }
    }

    // P0-A: the price list Booking uses for the scheduled prepayment.
    public override async Task<GetLotPricingResponse> GetLotPricing(
        GetLotPricingRequest request,
        ServerCallContext context)
    {
        if (!Guid.TryParseExact(request.LotId, "D", out var lotId))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "lot_id must be a UUID."));

        try
        {
            var result = await sender.Send(
                new GetLotPricingForBookingQuery(lotId, request.VehicleGroup), context.CancellationToken);
            if (result.IsFailure)
            {
                var code = result.Error == LotPricingForBookingErrors.InvalidVehicleGroup ? StatusCode.InvalidArgument
                    : result.Error == LotPricingForBookingErrors.LotNotFound ? StatusCode.NotFound
                    : StatusCode.FailedPrecondition;
                throw new RpcException(new Status(code, result.Error!.Message));
            }

            var pricing = result.Value!;
            return new GetLotPricingResponse
            {
                Price = pricing.Price,
                BillingMode = pricing.BillingMode,
                BlockMinutes = pricing.BlockMinutes,
                OvernightFee = pricing.OvernightFee,
                PolicyVersion = pricing.PolicyVersion
            };
        }
        catch (OperationCanceledException) when (context.CancellationToken.IsCancellationRequested)
        {
            throw new RpcException(new Status(StatusCode.Cancelled, "Request was cancelled."));
        }
        catch (Exception exception) when (exception is not RpcException)
        {
            logger.LogError(exception, "Lot pricing failed unexpectedly.");
            throw new RpcException(new Status(StatusCode.Internal, "Lot pricing failed."));
        }
    }
}
