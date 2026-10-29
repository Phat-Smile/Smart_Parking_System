using MediatR;
using ParkingSystem.Parking.Domain.ParkingSessions;

namespace ParkingSystem.Parking.Application.ParkingLots.OpeningHours;

public interface IParkingLotArrivalSearch
{
    Task<IReadOnlyList<ParkingLotCatalogItem>> ListApprovedOpenAtAsync(
        DateTimeOffset arrivalAt, CancellationToken cancellationToken, VehicleType? vehicleType = null);
}

public sealed record ListParkingLotsAtArrivalQuery(DateTimeOffset ArrivalAt, VehicleType? VehicleType = null)
    : IRequest<IReadOnlyList<ParkingLotCatalogItem>>;

public sealed class ListParkingLotsAtArrivalQueryHandler(IParkingLotArrivalSearch reader)
    : IRequestHandler<ListParkingLotsAtArrivalQuery, IReadOnlyList<ParkingLotCatalogItem>>
{
    public Task<IReadOnlyList<ParkingLotCatalogItem>> Handle(
        ListParkingLotsAtArrivalQuery query, CancellationToken cancellationToken) =>
        reader.ListApprovedOpenAtAsync(query.ArrivalAt, cancellationToken, query.VehicleType);
}
