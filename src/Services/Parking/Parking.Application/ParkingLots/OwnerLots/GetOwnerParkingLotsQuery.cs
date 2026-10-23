using MediatR;

namespace ParkingSystem.Parking.Application.ParkingLots.OwnerLots;

public sealed record GetOwnerParkingLotsQuery(Guid OwnerUserId) : IRequest<IReadOnlyList<OwnerParkingLotItem>>;

public sealed class GetOwnerParkingLotsQueryHandler(IOwnerParkingLotReader reader)
    : IRequestHandler<GetOwnerParkingLotsQuery, IReadOnlyList<OwnerParkingLotItem>>
{
    public Task<IReadOnlyList<OwnerParkingLotItem>> Handle(
        GetOwnerParkingLotsQuery request,
        CancellationToken cancellationToken) =>
        reader.ListByOwnerAsync(request.OwnerUserId, cancellationToken);
}
