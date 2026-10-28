using MediatR;
using ParkingSystem.Parking.Domain.ParkingSessions;

namespace ParkingSystem.Parking.Application.ParkingLots.ListParkingLots;

public sealed record ListParkingLotsQuery(VehicleType? VehicleType = null)
    : IRequest<IReadOnlyList<ParkingLotCatalogItem>>;

public sealed class ListParkingLotsQueryHandler(IParkingLotCatalogReader parkingLots)
    : IRequestHandler<ListParkingLotsQuery, IReadOnlyList<ParkingLotCatalogItem>>
{
    public Task<IReadOnlyList<ParkingLotCatalogItem>> Handle(
        ListParkingLotsQuery request,
        CancellationToken cancellationToken) =>
        parkingLots.ListApprovedAsync(cancellationToken, request.VehicleType);
}
