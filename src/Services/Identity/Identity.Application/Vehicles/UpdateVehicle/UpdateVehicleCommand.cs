using FluentValidation;
using MediatR;
using ParkingSystem.BuildingBlocks.Results;
using ParkingSystem.Identity.Domain.Vehicles;

namespace ParkingSystem.Identity.Application.Vehicles.UpdateVehicle;

public sealed record UpdateVehicleCommand(Guid AccountId, Guid VehicleId, string? NickName, VehicleDetails Details)
    : IRequest<Result<UpdatedVehicleResult>>;

public sealed record UpdatedVehicleResult(Guid VehicleId, string? LicensePlate, string VehicleType,
    string? NickName, bool IsDefault, VehicleDetails Details);

public static class UpdateVehicleErrors
{
    public static readonly Error NotFound = new("identity.vehicle_not_found", "Vehicle not found.");
    public static readonly Error BicyclePhotoRequired = new("identity.bicycle_photo_required", "Bicycle photo is required.");
}

public sealed class UpdateVehicleCommandValidator : AbstractValidator<UpdateVehicleCommand>
{
    public UpdateVehicleCommandValidator()
    {
        RuleFor(x => x.NickName).MaximumLength(Vehicle.MaxNickNameLength);
        RuleFor(x => x.Details).NotNull();
        VehicleDetailsValidation.AddRules(this, x => x.Details);
    }
}

public sealed class UpdateVehicleCommandHandler(IVehicleRepository vehicles)
    : IRequestHandler<UpdateVehicleCommand, Result<UpdatedVehicleResult>>
{
    public async Task<Result<UpdatedVehicleResult>> Handle(UpdateVehicleCommand request, CancellationToken cancellationToken)
    {
        var vehicle = await vehicles.FindByIdAsync(request.VehicleId, cancellationToken);
        if (vehicle is null || vehicle.AccountId != request.AccountId)
            return Result<UpdatedVehicleResult>.Failure(UpdateVehicleErrors.NotFound);
        if (vehicle.VehicleType == VehicleDomainType.Bicycle && !VehicleDetailsValidation.IsValidBicycleImageUrl(request.Details.PhotoUrl))
            return Result<UpdatedVehicleResult>.Failure(UpdateVehicleErrors.BicyclePhotoRequired);

        VehicleDetailsValidation.Apply(vehicle, request.NickName, request.Details);
        await vehicles.SaveChangesAsync(cancellationToken);
        return Result<UpdatedVehicleResult>.Success(new UpdatedVehicleResult(vehicle.Id, vehicle.LicensePlate,
            vehicle.VehicleType.ToString(), vehicle.NickName, vehicle.IsDefault, request.Details));
    }
}
