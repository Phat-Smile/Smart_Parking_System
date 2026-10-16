using Grpc.Core;
using MediatR;
using ParkingSystem.Parking.Application.ParkingLayout.GetAvailableSlotCount;
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
}
