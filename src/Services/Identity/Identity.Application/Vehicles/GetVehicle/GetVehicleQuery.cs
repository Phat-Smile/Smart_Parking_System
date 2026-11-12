using MediatR;
using ParkingSystem.BuildingBlocks.Results;
using ParkingSystem.Identity.Application.Vehicles.GetVehicles;

namespace ParkingSystem.Identity.Application.Vehicles.GetVehicle;

public sealed record GetVehicleQuery(Guid AccountId, Guid VehicleId) : IRequest<Result<VehicleItem>>;

public sealed class GetVehicleQueryHandler(IVehicleRepository vehicles) : IRequestHandler<GetVehicleQuery, Result<VehicleItem>>
{
    public async Task<Result<VehicleItem>> Handle(GetVehicleQuery request, CancellationToken cancellationToken)
    {
        var v = await vehicles.FindByIdAsync(request.VehicleId, cancellationToken);
        if (v is null || v.AccountId != request.AccountId)
            return Result<VehicleItem>.Failure(UpdateVehicle.UpdateVehicleErrors.NotFound);
        return Result<VehicleItem>.Success(new VehicleItem(v.Id, v.LicensePlate, v.VehicleType.ToString(),
            v.NickName, v.CreatedAtUtc, v.IsDefault,
            new VehicleDetails(v.Brand, v.Model, v.MotorbikeKind, v.Seats, v.FuelType, v.Color, v.ManufacturingYear, v.Size, v.PhotoUrl)));
    }
}
