using MediatR;
using ParkingSystem.Parking.Application.Common.Paging;
using ParkingSystem.Parking.Domain.ParkingLots;

namespace ParkingSystem.Parking.Application.ParkingLots.AdminLots;

public sealed record GetAdminParkingLotsQuery(
    ParkingLotApprovalStatus? Status = null,
    int? Page = null,
    int? PageSize = null) : IRequest<PagedResult<AdminParkingLotItem>>;

public sealed class GetAdminParkingLotsQueryHandler(IAdminParkingLotReader reader)
    : IRequestHandler<GetAdminParkingLotsQuery, PagedResult<AdminParkingLotItem>>
{
    public Task<PagedResult<AdminParkingLotItem>> Handle(
        GetAdminParkingLotsQuery request,
        CancellationToken cancellationToken)
    {
        var (page, pageSize) = Paging.Normalize(request.Page, request.PageSize);
        return reader.ListForAdminAsync(request.Status, page, pageSize, cancellationToken);
    }
}
