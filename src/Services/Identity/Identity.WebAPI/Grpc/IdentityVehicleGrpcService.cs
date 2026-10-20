using Grpc.Core;
using MediatR;
using ParkingSystem.BuildingBlocks.Results;
using ParkingSystem.Identity.Application.Vehicles.Lookup;
using SmartParking.Contracts.Identity.Vehicle.V1;
using AppVehicleType = ParkingSystem.Identity.Application.Vehicles.Lookup.VehicleType;
using ProtoVehicleType = SmartParking.Contracts.Identity.Vehicle.V1.VehicleType;

namespace ParkingSystem.Identity.WebAPI.Grpc;

public sealed class IdentityVehicleGrpcService(
    ISender sender,
    ILogger<IdentityVehicleGrpcService> logger) : IdentityVehicleContract.IdentityVehicleContractBase
{
    public override async Task<LookupVehicleResponse> LookupVehicle(
        LookupVehicleRequest request,
        ServerCallContext context)
    {
        Result<VehicleLookupResult> result;
        LookupVehicleResponse? response = null;
        try
        {
            result = await sender.Send(new LookupVehicleByPlateQuery(request.LicensePlate), context.CancellationToken);
            if (result.IsSuccess)
                response = ToResponse(result.Value!);
        }
        catch (OperationCanceledException) when (context.CancellationToken.IsCancellationRequested)
        {
            // A caller whose deadline passed already sees DEADLINE_EXCEEDED from its own client-side timer.
            throw new RpcException(new Status(StatusCode.Cancelled, "Request was cancelled."));
        }
        catch (Exception exception)
        {
            // The plate is personal data: log the failure, not the request payload.
            logger.LogError(exception, "Vehicle lookup failed unexpectedly.");
            throw new RpcException(new Status(StatusCode.Internal, "Vehicle lookup failed."));
        }

        if (result.IsFailure)
        {
            var error = result.Error!;
            logger.LogDebug("Vehicle lookup rejected: {ErrorCode}", error.Code);
            throw new RpcException(new Status(MapStatus(error), error.Message));
        }

        return response!;
    }

    private static StatusCode MapStatus(Error error) =>
        error.Code == LookupVehicleErrors.EmptyPlate.Code || error.Code == LookupVehicleErrors.InvalidPlateFormat.Code
            ? StatusCode.InvalidArgument
            : StatusCode.Internal;

    private static LookupVehicleResponse ToResponse(VehicleLookupResult vehicle)
    {
        if (!vehicle.IsRegistered)
            return new LookupVehicleResponse { VehicleType = ProtoVehicleType.Unspecified };

        // A registered answer must always carry both ids and a known type; never send a half-filled "registered".
        if (vehicle.AccountId is not { } accountId || accountId == Guid.Empty
            || vehicle.VehicleId is not { } vehicleId || vehicleId == Guid.Empty
            || vehicle.VehicleType is null)
            throw new InvalidOperationException("Registered vehicle lookup result is missing its identifiers or type.");

        return new LookupVehicleResponse
        {
            IsRegistered = true,
            // Guid "D" format is already lowercase, 36 chars.
            AccountId = accountId.ToString("D"),
            VehicleId = vehicleId.ToString("D"),
            VehicleType = MapVehicleType(vehicle.VehicleType),
            IsAccountActive = vehicle.IsAccountActive
        };
    }

    // Explicit switch instead of a numeric cast between bounded-context enums.
    private static ProtoVehicleType MapVehicleType(AppVehicleType? vehicleType) => vehicleType switch
    {
        AppVehicleType.Car => ProtoVehicleType.Car,
        AppVehicleType.Motorbike => ProtoVehicleType.Motorbike,
        _ => ProtoVehicleType.Unspecified
    };
}
