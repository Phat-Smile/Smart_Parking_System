using MediatR;
using ParkingSystem.BuildingBlocks.Results;
using ParkingSystem.BuildingBlocks.Time;
using ParkingSystem.Parking.Application.ParkingLots;

namespace ParkingSystem.Parking.Application.ParkingLayout.ManageLayout;

// US-MAP-002: the same canonical layout can serve 2D and 3D clients.
// Public reads never expose Pending or Rejected parking lots.
public sealed record GetPublicParkingLayoutQuery(Guid LotId) : IRequest<Result<PublicParkingLayout>>;

public sealed record PublicParkingLayout(
    Guid LotId,
    int Version,
    DateTimeOffset AsOfUtc,
    IReadOnlyList<LayoutFloor> Floors);

public sealed class GetPublicParkingLayoutQueryHandler(
    IParkingLotDetailReader parkingLots,
    IManageParkingLayoutRepository layouts,
    IClock clock)
    : IRequestHandler<GetPublicParkingLayoutQuery, Result<PublicParkingLayout>>
{
    public async Task<Result<PublicParkingLayout>> Handle(
        GetPublicParkingLayoutQuery request, CancellationToken cancellationToken)
    {
        if (request.LotId == Guid.Empty ||
            await parkingLots.GetApprovedByIdAsync(request.LotId, cancellationToken) is null)
            return Result<PublicParkingLayout>.Failure(ParkingLayoutErrors.LotNotFound);

        var layout = await layouts.ReadAsync(request.LotId, cancellationToken);
        return Result<PublicParkingLayout>.Success(new PublicParkingLayout(
            layout.LotId, layout.Version, clock.UtcNow, layout.Floors));
    }
}
