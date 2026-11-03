using MediatR;
using ParkingSystem.BuildingBlocks.Results;

namespace ParkingSystem.Identity.Application.Vehicles.GetVehicles;

public sealed record GetVehiclesQuery(Guid AccountId) : IRequest<Result<GetVehiclesResult>>;

public sealed record VehicleItem(
    Guid VehicleId,
    string LicensePlate,
    string VehicleType,
    string? NickName,
    DateTimeOffset CreatedAtUtc,
    bool IsDefault);

public sealed record GetVehiclesResult(IReadOnlyList<VehicleItem> Vehicles);

public sealed class GetVehiclesQueryHandler(IVehicleRepository vehicles)
    : IRequestHandler<GetVehiclesQuery, Result<GetVehiclesResult>>
{
    public async Task<Result<GetVehiclesResult>> Handle(GetVehiclesQuery request, CancellationToken cancellationToken)
    {
        var list = await vehicles.ListByAccountIdAsync(request.AccountId, cancellationToken);

        var items = list.Select(v => new VehicleItem(
            v.Id,
            v.LicensePlate,
            v.VehicleType.ToString(),
            v.NickName,
            v.CreatedAtUtc,
            v.IsDefault)).ToList();

        return Result<GetVehiclesResult>.Success(new GetVehiclesResult(items));
    }
}
