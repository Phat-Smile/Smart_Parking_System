using System.Globalization;
using Grpc.Core;
using MediatR;
using ParkingSystem.Parking.Application.ParkingSessions.GetSessionFeeContext;
using SmartParking.Contracts.Parking.SessionFee.V1;
using SessionPricingMessage = SmartParking.Contracts.Parking.SessionFee.V1.SessionPricing;

namespace ParkingSystem.Parking.WebAPI.Grpc;

// Contract 6, Parking part: Payment (BE-5) asks for what it needs to compute the fee of a session.
public sealed class ParkingSessionFeeGrpcService(
    ISender sender,
    ILogger<ParkingSessionFeeGrpcService> logger) : ParkingSessionFeeContract.ParkingSessionFeeContractBase
{
    public override async Task<GetSessionFeeContextResponse> GetSessionFeeContext(
        GetSessionFeeContextRequest request,
        ServerCallContext context)
    {
        if (!Guid.TryParseExact(request.SessionId, "D", out var sessionId))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "session_id must be a UUID."));

        try
        {
            var result = await sender.Send(new GetSessionFeeContextQuery(sessionId), context.CancellationToken);
            if (result.IsFailure)
            {
                var statusCode = result.Error == GetSessionFeeContextErrors.SessionNotFound
                    ? StatusCode.NotFound
                    : StatusCode.FailedPrecondition;
                throw new RpcException(new Status(statusCode, result.Error!.Message));
            }

            var fee = result.Value!;
            return new GetSessionFeeContextResponse
            {
                SessionId = fee.SessionId.ToString("D"),
                LotId = fee.LotId.ToString("D"),
                PlateNumber = fee.PlateNumber,
                VehicleType = fee.VehicleType,
                Status = fee.Status,
                EntryTimeUtc = fee.EntryTimeUtc.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
                CustomerType = fee.CustomerType,
                AccountId = fee.AccountId?.ToString("D") ?? string.Empty,
                BookingId = fee.BookingId?.ToString("D") ?? string.Empty,
                Pricing = new SessionPricingMessage
                {
                    PolicyId = fee.Pricing.PolicyId.ToString("D"),
                    CalculationMethod = fee.Pricing.CalculationMethod,
                    BlockMinutes = fee.Pricing.BlockMinutes,
                    PricePerUnit = fee.Pricing.PricePerUnit
                }
            };
        }
        catch (OperationCanceledException) when (context.CancellationToken.IsCancellationRequested)
        {
            // A caller whose deadline passed already sees DEADLINE_EXCEEDED from its own client-side timer.
            throw new RpcException(new Status(StatusCode.Cancelled, "Request was cancelled."));
        }
        catch (Exception exception) when (exception is not RpcException)
        {
            logger.LogError(exception, "Session fee context failed unexpectedly.");
            throw new RpcException(new Status(StatusCode.Internal, "Session fee context failed."));
        }
    }
}
