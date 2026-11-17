using System.Globalization;
using Grpc.Core;
using Microsoft.Extensions.Options;
using ParkingSystem.AspNetCore.Security;
using ParkingSystem.BuildingBlocks.Results;
using ParkingSystem.Payment.Application.Common.Constants;
using ParkingSystem.Payment.Application.Common.Interfaces;
using SmartParking.Contracts.Parking.SessionFee.V1;

namespace ParkingSystem.Payment.WebAPI.Grpc;

// Reads the fee context of a parking session from the Parking service over gRPC.
// Fail-closed: every failure other than a clear "session not found" becomes
// ParkingServiceUnavailable. No default price is ever returned.
public sealed class ParkingSessionFeeGrpcGateway(
    ParkingSessionFeeContract.ParkingSessionFeeContractClient client,
    IOptions<ParkingServiceOptions> options,
    IOptions<ServiceKeyOptions> serviceKeyOptions,
    ILogger<ParkingSessionFeeGrpcGateway> logger) : IParkingSessionGateway
{
    public async Task<Result<ParkingSessionFeeContext>> GetFeeContextAsync(
        Guid sessionId,
        Guid? callerAccountId,
        string callerRole,
        CancellationToken cancellationToken = default)
    {
        if (sessionId == Guid.Empty)
        {
            return Result<ParkingSessionFeeContext>.Failure(ParkingFeeErrors.SessionIdInvalid);
        }

        var timeout = options.Value.TimeoutSeconds > 0 ? options.Value.TimeoutSeconds : 5;
        var headers = new Metadata();
        var serviceKey = serviceKeyOptions.Value.ServiceKey;
        if (!string.IsNullOrWhiteSpace(serviceKey))
        {
            headers.Add(ServiceKeyValidationMiddleware.HeaderName, serviceKey);
        }

        try
        {
            var response = await client.GetSessionFeeContextAsync(
                new GetSessionFeeContextRequest { SessionId = sessionId.ToString("D") },
                headers: headers,
                deadline: DateTime.UtcNow.AddSeconds(timeout),
                cancellationToken: cancellationToken);

            return Map(response, sessionId);
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.NotFound)
        {
            return Result<ParkingSessionFeeContext>.Failure(ParkingFeeErrors.SessionNotFound);
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.FailedPrecondition)
        {
            // No price list in force for this session: Payment must not guess a price.
            logger.LogWarning("Parking has no price list for session {SessionId}: {Detail}", sessionId, ex.Status.Detail);
            return Result<ParkingSessionFeeContext>.Failure(ParkingFeeErrors.ParkingServiceUnavailable);
        }
        catch (RpcException ex)
        {
            logger.LogError(ex, "Parking fee context call failed with {StatusCode} for session {SessionId}.", ex.StatusCode, sessionId);
            return Result<ParkingSessionFeeContext>.Failure(ParkingFeeErrors.ParkingServiceUnavailable);
        }
    }

    private Result<ParkingSessionFeeContext> Map(GetSessionFeeContextResponse r, Guid requestedSessionId)
    {
        var pricing = r.Pricing;
        var entryParsed = DateTimeOffset.TryParse(
            r.EntryTimeUtc,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out var entry);
        var lotParsed = Guid.TryParse(r.LotId, out var lotId);

        var valid =
            Guid.TryParse(r.SessionId, out var echoedId) && echoedId == requestedSessionId
            && lotParsed && lotId != Guid.Empty
            && !string.IsNullOrWhiteSpace(r.PlateNumber)
            && !string.IsNullOrWhiteSpace(r.VehicleType)
            && !string.IsNullOrWhiteSpace(r.Status)
            && entryParsed
            && pricing is not null
            && pricing.PricePerUnit > 0
            && (pricing.CalculationMethod == "PerEntry"
                || (pricing.CalculationMethod == "Block" && pricing.BlockMinutes > 0));

        if (!valid)
        {
            logger.LogError("Parking returned an invalid fee context for session {SessionId}.", requestedSessionId);
            return Result<ParkingSessionFeeContext>.Failure(ParkingFeeErrors.ParkingServiceUnavailable);
        }

        DateTimeOffset? lastPaid = null;
        if (!string.IsNullOrWhiteSpace(r.LastPaidAtUtc) && DateTimeOffset.TryParse(
            r.LastPaidAtUtc,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out var parsedLastPaid))
        {
            lastPaid = parsedLastPaid;
        }

        DateTimeOffset? arrivedAtExit = null;
        if (!string.IsNullOrWhiteSpace(r.ArrivedAtExitUtc) && DateTimeOffset.TryParse(
            r.ArrivedAtExitUtc,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out var parsedArrivedAtExit))
        {
            arrivedAtExit = parsedArrivedAtExit;
        }

        var gracePeriod = r.GracePeriodMinutes > 0 ? r.GracePeriodMinutes : 15;

        return Result<ParkingSessionFeeContext>.Success(new ParkingSessionFeeContext(
            requestedSessionId,
            lotId,
            r.PlateNumber,
            r.VehicleType,
            r.Status,
            entry,
            Guid.TryParse(r.AccountId, out var account) && account != Guid.Empty ? account : null,
            Guid.TryParse(r.BookingId, out var booking) && booking != Guid.Empty ? booking : null,
            new PricingSnapshotDto(pricing!.PolicyId, pricing.CalculationMethod, pricing.BlockMinutes, pricing.PricePerUnit),
            lastPaid,
            arrivedAtExit,
            gracePeriod));
    }
}
