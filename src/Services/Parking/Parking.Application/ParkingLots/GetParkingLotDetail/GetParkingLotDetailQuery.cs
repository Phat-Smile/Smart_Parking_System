using FluentValidation;
using MediatR;

namespace ParkingSystem.Parking.Application.ParkingLots.GetParkingLotDetail;

public sealed record GetParkingLotDetailQuery(Guid ParkingLotId) : IRequest<ParkingLotDetail?>;

public sealed class GetParkingLotDetailQueryValidator : AbstractValidator<GetParkingLotDetailQuery>
{
    public GetParkingLotDetailQueryValidator()
    {
        RuleFor(query => query.ParkingLotId).NotEmpty();
    }
}

public sealed class GetParkingLotDetailQueryHandler(IParkingLotDetailReader parkingLots)
    : IRequestHandler<GetParkingLotDetailQuery, ParkingLotDetail?>
{
    public Task<ParkingLotDetail?> Handle(
        GetParkingLotDetailQuery request,
        CancellationToken cancellationToken) =>
        parkingLots.GetApprovedByIdAsync(request.ParkingLotId, cancellationToken);
}
