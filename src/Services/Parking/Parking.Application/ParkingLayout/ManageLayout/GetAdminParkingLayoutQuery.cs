using MediatR;
using ParkingSystem.BuildingBlocks.Results;

namespace ParkingSystem.Parking.Application.ParkingLayout.ManageLayout;

// Authorization is enforced by SystemAdminPolicy on the dedicated HTTP endpoint.
// Do not weaken the existing owner-scoped layout query or write command.
public sealed record GetAdminParkingLayoutQuery(Guid LotId) : IRequest<Result<ParkingLotLayout>>;

public sealed class GetAdminParkingLayoutQueryHandler(IManageParkingLayoutRepository repository)
    : IRequestHandler<GetAdminParkingLayoutQuery, Result<ParkingLotLayout>>
{
    public async Task<Result<ParkingLotLayout>> Handle(GetAdminParkingLayoutQuery request, CancellationToken cancellationToken)
    {
        if (request.LotId == Guid.Empty || !await repository.LotExistsAsync(request.LotId, cancellationToken))
            return Result<ParkingLotLayout>.Failure(ParkingLayoutErrors.LotNotFound);

        return Result<ParkingLotLayout>.Success(await repository.ReadAsync(request.LotId, cancellationToken));
    }
}
