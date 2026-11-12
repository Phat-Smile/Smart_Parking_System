using Grpc.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ParkingSystem.AspNetCore.Security;
using ParkingSystem.Parking.Application.Common.Identity;
using SmartParking.Contracts.Identity.Vehicle.V1;

namespace ParkingSystem.Parking.WebAPI.Grpc;

/// <summary>
/// Contract 1 (plate -> account and vehicle): asks Identity who owns a plate.
/// A match only links the session for history and fees (BR-05.f) and no match leaves it unlinked (BR-05.e), so every
/// failure answers "not linked" instead of holding up the gate. The plate is personal data and is never logged.
/// </summary>
public sealed class IdentityVehicleGrpcClient(
    IdentityVehicleContract.IdentityVehicleContractClient client,
    IOptions<IdentityServiceOptions> options,
    IOptions<ServiceKeyOptions> serviceKeyOptions,
    ILogger<IdentityVehicleGrpcClient> logger) : IPlateOwnerLookup
{
    public async Task<PlateOwner?> FindByPlateAsync(string normalizedPlate, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(normalizedPlate))
            return null;

        LookupVehicleResponse response;
        try
        {
            response = await client.LookupVehicleAsync(
                new LookupVehicleRequest { LicensePlate = normalizedPlate },
                headers: BuildHeaders(),
                deadline: DateTime.UtcNow.AddSeconds(options.Value.TimeoutSeconds),
                cancellationToken: cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (RpcException) when (cancellationToken.IsCancellationRequested)
        {
            // The caller gave up (request aborted): stop the use case instead of opening a session nobody waits for.
            throw new OperationCanceledException(cancellationToken);
        }
        catch (RpcException exception)
        {
            LogRpcFailure(exception.StatusCode);
            return null;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Plate lookup in Identity failed unexpectedly; the session is opened without an account link.");
            return null;
        }

        return ToOwner(response);
    }

    private PlateOwner? ToOwner(LookupVehicleResponse response)
    {
        if (!response.IsRegistered)
            return null;

        // Identity only reports the owner's state and Parking decides. Following docs/api/Identity-gRPC-Vehicle-Lookup.md,
        // a match needs a registered vehicle of an active account; a suspended, disabled or unverified owner counts as unknown.
        if (!response.IsAccountActive)
            return null;

        if (!TryParseId(response.AccountId, out var accountId) || !TryParseId(response.VehicleId, out var vehicleId))
        {
            logger.LogError("Identity answered a registered plate without usable identifiers; the session is opened without an account link.");
            return null;
        }

        return new PlateOwner(accountId, vehicleId);
    }

    private Metadata BuildHeaders()
    {
        var headers = new Metadata();
        var serviceKey = serviceKeyOptions.Value.ServiceKey;
        if (!string.IsNullOrWhiteSpace(serviceKey))
            headers.Add(ServiceKeyValidationMiddleware.HeaderName, serviceKey);

        return headers;
    }

    // Only the status code is logged: the detail text comes from another service and could carry the plate.
    private void LogRpcFailure(StatusCode status)
    {
        var level = status switch
        {
            StatusCode.InvalidArgument => LogLevel.Information, // the gate read an unusable plate
            StatusCode.Unavailable or StatusCode.DeadlineExceeded => LogLevel.Warning,
            _ => LogLevel.Error // for example PermissionDenied: the service key is wrong
        };
        logger.Log(level, "Plate lookup in Identity failed with {StatusCode}; the session is opened without an account link.", status);
    }

    private static bool TryParseId(string value, out Guid id) =>
        Guid.TryParseExact(value, "D", out id) && id != Guid.Empty;
}
