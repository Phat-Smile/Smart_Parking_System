using Grpc.Core;
using MediatR;
using SmartParking.Contracts.Identity.Vehicle.V1;
using ParkingSystem.Identity.Application.Vehicles.Lookup;
using ProtoVehicleType = SmartParking.Contracts.Identity.Vehicle.V1.VehicleType;
using AppVehicleType = ParkingSystem.Identity.Application.Vehicles.Lookup.VehicleType;

namespace ParkingSystem.Identity.WebAPI.Grpc;

public sealed class IdentityVehicleGrpcService : IdentityVehicleContract.IdentityVehicleContractBase
{
    private readonly ISender _sender;
    private readonly ILogger<IdentityVehicleGrpcService> _logger;

    public IdentityVehicleGrpcService(
        ISender sender,
        ILogger<IdentityVehicleGrpcService> logger)
    {
        _sender = sender ?? throw new ArgumentNullException(nameof(sender));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public override async Task<LookupVehicleResponse> LookupVehicle(
        LookupVehicleRequest request,
        ServerCallContext context)
    {
        ArgumentNullException.ThrowIfNull(request);

        _logger.LogInformation("Processing gRPC LookupVehicle for plate: {LicensePlate}", request.LicensePlate);

        var query = new LookupVehicleByPlateQuery(request.LicensePlate);
        var result = await _sender.Send(query, context.CancellationToken);

        if (result.IsFailure || result.Value is null)
        {
            var error = result.Error ?? new ParkingSystem.BuildingBlocks.Results.Error("Vehicle.LookupFailed", "Vehicle lookup failed.");
            _logger.LogWarning("Vehicle lookup failed: {ErrorCode} - {Message}", error.Code, error.Message);

            var statusCode = error.Code switch
            {
                "identity.plate_empty" or "identity.plate_invalid_format" or "Vehicle.PlateEmpty" or "Vehicle.PlateInvalid" => StatusCode.InvalidArgument,
                _ => StatusCode.Internal
            };

            throw new RpcException(new Status(statusCode, error.Message));
        }

        var vehicle = result.Value;

        var response = new LookupVehicleResponse
        {
            IsRegistered = vehicle.IsRegistered,
            AccountId = vehicle.AccountId?.ToString("D") ?? string.Empty,
            VehicleId = vehicle.VehicleId?.ToString("D") ?? string.Empty,
            OwnerFullName = vehicle.OwnerFullName ?? string.Empty,
            IsAccountActive = vehicle.IsAccountActive,
            VehicleType = vehicle.VehicleType switch
            {
                AppVehicleType.Car => ProtoVehicleType.Car,
                AppVehicleType.Motorbike => ProtoVehicleType.Motorbike,
                _ => ProtoVehicleType.Unspecified
            }
        };

        return response;
    }
}
