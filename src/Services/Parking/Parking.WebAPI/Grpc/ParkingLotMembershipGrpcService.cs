using Grpc.Core;
using MediatR;
using ParkingSystem.Parking.Application.ParkingLots.GetLotMembership;
using SmartParking.Contracts.Parking.Membership.V1;

namespace ParkingSystem.Parking.WebAPI.Grpc;

public sealed class ParkingLotMembershipGrpcService(
    ISender sender,
    ILogger<ParkingLotMembershipGrpcService> logger) : ParkingLotMembershipContract.ParkingLotMembershipContractBase
{
    public override async Task<GetLotMembershipResponse> GetLotMembership(
        GetLotMembershipRequest request,
        ServerCallContext context)
    {
        if (!Guid.TryParseExact(request.LotId, "D", out var lotId) ||
            !Guid.TryParseExact(request.AccountId, "D", out var accountId))
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "lot_id and account_id must be canonical UUIDs."));
        }

        try
        {
            var result = await sender.Send(new GetLotMembershipQuery(lotId, accountId), context.CancellationToken);
            if (result.IsFailure)
            {
                throw new RpcException(new Status(StatusCode.InvalidArgument, result.Error!.Message));
            }

            var membership = result.Value!;
            return new GetLotMembershipResponse
            {
                IsMember = membership.IsMember,
                Role = membership.Role
            };
        }
        catch (OperationCanceledException) when (context.CancellationToken.IsCancellationRequested)
        {
            throw new RpcException(new Status(StatusCode.Cancelled, "Request was cancelled."));
        }
        catch (Exception exception) when (exception is not RpcException)
        {
            logger.LogError(exception, "GetLotMembership failed unexpectedly for LotId {LotId}, AccountId {AccountId}.", request.LotId, request.AccountId);
            throw new RpcException(new Status(StatusCode.Internal, "GetLotMembership failed."));
        }
    }
}
