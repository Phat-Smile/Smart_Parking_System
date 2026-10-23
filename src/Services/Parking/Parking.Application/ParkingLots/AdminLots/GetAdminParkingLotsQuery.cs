using MediatR;
using ParkingSystem.Parking.Domain.ParkingLots;

namespace ParkingSystem.Parking.Application.ParkingLots.AdminLots;

public sealed record GetAdminParkingLotsQuery(ParkingLotApprovalStatus? Status = null)
    : IRequest<IReadOnlyList<AdminParkingLotItem>>;

public sealed class GetAdminParkingLotsQueryHandler(IAdminParkingLotReader reader)
    : IRequestHandler<GetAdminParkingLotsQuery, IReadOnlyList<AdminParkingLotItem>>
{
    public Task<IReadOnlyList<AdminParkingLotItem>> Handle(
        GetAdminParkingLotsQuery request,
        CancellationToken cancellationToken) =>
        reader.ListForAdminAsync(request.Status, cancellationToken);
}
