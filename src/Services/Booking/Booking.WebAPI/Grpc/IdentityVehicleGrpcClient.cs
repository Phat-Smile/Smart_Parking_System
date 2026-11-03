using Grpc.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ParkingSystem.AspNetCore.Security;
using ParkingSystem.Booking.Application.Vehicles;
using ParkingSystem.Booking.Domain.Reservations;
using ParkingSystem.BuildingBlocks.Results;
using SmartParking.Contracts.Identity.Vehicle.V1;
using ProtoVehicleType = SmartParking.Contracts.Identity.Vehicle.V1.VehicleType;

namespace ParkingSystem.Booking.WebAPI.Grpc;

/// <summary>
/// gRPC adapter implementing <see cref="IVehicleOwnershipClient"/> to verify vehicle registration,
/// user ownership, and vehicle eligibility against Identity service.
/// </summary>
public sealed class IdentityVehicleGrpcClient(
    IdentityVehicleContract.IdentityVehicleContractClient client,
    IOptions<IdentityServiceOptions> options,
    IOptions<ServiceKeyOptions>? serviceKeyOptions,
    ILogger<IdentityVehicleGrpcClient> logger) : IVehicleOwnershipClient
{
    public IdentityVehicleGrpcClient(
        IdentityVehicleContract.IdentityVehicleContractClient client,
        IOptions<IdentityServiceOptions> options,
        ILogger<IdentityVehicleGrpcClient> logger)
        : this(client, options, null, logger)
    {
    }

    public async Task<Result<VehicleOwnershipInfo>> VerifyOwnershipAsync(
        string rawOrCanonicalPlate,
        Guid expectedUserId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(rawOrCanonicalPlate))
        {
            return Result<VehicleOwnershipInfo>.Failure(VehicleOwnershipErrors.EmptyPlate);
        }

        if (!VietnameseLicensePlateValidator.TryNormalizeAndValidate(rawOrCanonicalPlate, out var canonicalPlate))
        {
            return Result<VehicleOwnershipInfo>.Failure(VehicleOwnershipErrors.InvalidPlateFormat);
        }

        if (expectedUserId == Guid.Empty)
        {
            return Result<VehicleOwnershipInfo>.Failure(VehicleOwnershipErrors.VehicleOwnershipMismatch);
        }

        var timeout = options.Value.TimeoutSeconds > 0 ? options.Value.TimeoutSeconds : 5;
        var deadline = DateTime.UtcNow.AddSeconds(timeout);

        var headers = new Metadata();
        var serviceKey = serviceKeyOptions?.Value?.ServiceKey;
        if (!string.IsNullOrWhiteSpace(serviceKey))
        {
            headers.Add(ServiceKeyValidationMiddleware.HeaderName, serviceKey);
        }

        LookupVehicleResponse response;
        try
        {
            var request = new LookupVehicleRequest { LicensePlate = canonicalPlate };
            response = await client.LookupVehicleAsync(
                request,
                headers: headers,
                deadline: deadline,
                cancellationToken: cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.Cancelled && cancellationToken.IsCancellationRequested)
        {
            throw new OperationCanceledException(cancellationToken);
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.Cancelled)
        {
            logger.LogInformation("Call to Identity service was cancelled by peer/network.");
            return Result<VehicleOwnershipInfo>.Failure(VehicleOwnershipErrors.RequestCancelled);
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.InvalidArgument)
        {
            return Result<VehicleOwnershipInfo>.Failure(VehicleOwnershipErrors.InvalidPlateFormat);
        }
        catch (RpcException ex) when (ex.StatusCode is StatusCode.Unauthenticated or StatusCode.PermissionDenied)
        {
            logger.LogError(ex, "Service authentication failed with Identity service (StatusCode: {StatusCode}).", ex.StatusCode);
            return Result<VehicleOwnershipInfo>.Failure(VehicleOwnershipErrors.ServiceAuthFailed);
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.DeadlineExceeded)
        {
            logger.LogWarning(ex, "Call to Identity service timed out after {TimeoutSeconds}s.", timeout);
            return Result<VehicleOwnershipInfo>.Failure(VehicleOwnershipErrors.DeadlineExceeded);
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.Unavailable)
        {
            logger.LogError(ex, "Identity service is currently unavailable.");
            return Result<VehicleOwnershipInfo>.Failure(VehicleOwnershipErrors.ServiceUnavailable);
        }
        catch (RpcException ex)
        {
            logger.LogError(ex, "gRPC call to Identity service failed with StatusCode {StatusCode}.", ex.StatusCode);
            return Result<VehicleOwnershipInfo>.Failure(VehicleOwnershipErrors.CommunicationFailed);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error occurred when communicating with Identity service.");
            return Result<VehicleOwnershipInfo>.Failure(VehicleOwnershipErrors.CommunicationFailed);
        }

        return MapResponseToResult(response, expectedUserId, canonicalPlate);
    }

    private Result<VehicleOwnershipInfo> MapResponseToResult(
        LookupVehicleResponse response,
        Guid expectedUserId,
        string canonicalPlate)
    {
        if (!response.IsRegistered)
        {
            return Result<VehicleOwnershipInfo>.Failure(VehicleOwnershipErrors.VehicleNotRegistered);
        }

        if (!Guid.TryParseExact(response.AccountId, "D", out var accountId) || accountId == Guid.Empty ||
            !Guid.TryParseExact(response.VehicleId, "D", out var vehicleId) || vehicleId == Guid.Empty)
        {
            logger.LogError("Identity returned registered vehicle without valid GUID identifiers.");
            return Result<VehicleOwnershipInfo>.Failure(VehicleOwnershipErrors.CommunicationFailed);
        }

        if (accountId != expectedUserId)
        {
            return Result<VehicleOwnershipInfo>.Failure(VehicleOwnershipErrors.VehicleOwnershipMismatch);
        }

        var appVehicleType = MapProtoVehicleType(response.VehicleType);
        if (appVehicleType != SupportedVehicleType.Car)
        {
            return Result<VehicleOwnershipInfo>.Failure(VehicleOwnershipErrors.UnsupportedVehicleType);
        }

        if (!response.IsAccountActive)
        {
            return Result<VehicleOwnershipInfo>.Failure(VehicleOwnershipErrors.AccountNotEligible);
        }

        return Result<VehicleOwnershipInfo>.Success(
            new VehicleOwnershipInfo(
                accountId,
                vehicleId,
                canonicalPlate,
                appVehicleType,
                response.IsAccountActive));
    }

    private static SupportedVehicleType MapProtoVehicleType(ProtoVehicleType protoType) => protoType switch
    {
        ProtoVehicleType.Car => SupportedVehicleType.Car,
        ProtoVehicleType.Motorbike => SupportedVehicleType.Motorbike,
        _ => SupportedVehicleType.Unspecified
    };
}

