using MediatR;
using ParkingSystem.Parking.Application.Common.Paging;
using ParkingSystem.Parking.Domain.ParkingLots;

namespace ParkingSystem.Parking.Application.ParkingLots.OwnerLots;

public sealed record GetOwnerParkingLotsQuery(
    Guid OwnerUserId,
    ParkingLotApprovalStatus? Status = null,
    int? Page = null,
    int? PageSize = null) : IRequest<PagedResult<OwnerParkingLotItem>>;

public sealed class GetOwnerParkingLotsQueryHandler(IOwnerParkingLotReader reader)
    : IRequestHandler<GetOwnerParkingLotsQuery, PagedResult<OwnerParkingLotItem>>
{
    public Task<PagedResult<OwnerParkingLotItem>> Handle(
        GetOwnerParkingLotsQuery request,
        CancellationToken cancellationToken)
    {
        var (page, pageSize) = Paging.Normalize(request.Page, request.PageSize);
        return reader.ListByOwnerAsync(request.OwnerUserId, request.Status, page, pageSize, cancellationToken);
    }
}
