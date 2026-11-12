using System.Threading;
using System.Threading.Tasks;
using MediatR;
using ParkingSystem.BuildingBlocks.Results;
using ParkingSystem.Parking.Application.Common.Constants;
using ParkingSystem.Parking.Application.Common.Interfaces;
using ParkingSystem.Parking.Domain.Entities;

namespace ParkingSystem.Parking.Application.Features.ReservationRatioPolicy.Queries.GetReservationRatioPolicy;

public sealed class GetReservationRatioPolicyQueryHandler : IRequestHandler<GetReservationRatioPolicyQuery, Result<ReservationRatioPolicyResponse>>
{
    private readonly IReservationRatioPolicyRepository _repository;
    private readonly ILotOwnershipService _lotOwnershipService;

    public GetReservationRatioPolicyQueryHandler(
        IReservationRatioPolicyRepository repository,
        ILotOwnershipService lotOwnershipService)
    {
        _repository = repository;
        _lotOwnershipService = lotOwnershipService;
    }

    public async Task<Result<ReservationRatioPolicyResponse>> Handle(GetReservationRatioPolicyQuery request, CancellationToken cancellationToken)
    {
        bool lotExists = await _lotOwnershipService.LotExistsAsync(request.LotId);
        if (!lotExists)
        {
            return Result<ReservationRatioPolicyResponse>.Failure(ParkingErrors.ReservationRatioPolicy.LotNotFound);
        }

        bool isOwner = await _lotOwnershipService.IsLotOwnerAsync(request.LotId, request.CallerId);
        if (!isOwner)
        {
            return Result<ReservationRatioPolicyResponse>.Failure(ParkingErrors.ReservationRatioPolicy.NotLotOwner);
        }

        var policy = await _repository.GetByLotIdAsync(request.LotId, cancellationToken);
        if (policy == null)
        {
            return Result<ReservationRatioPolicyResponse>.Success(
                new ReservationRatioPolicyResponse(
                    request.LotId,
                    ParkingSystem.Parking.Domain.Entities.ReservationRatioPolicy.DefaultReservationRatioPercent,
                    false,
                    null));
        }

        return Result<ReservationRatioPolicyResponse>.Success(
            new ReservationRatioPolicyResponse(
                request.LotId,
                policy.ReservationRatioPercent,
                true,
                policy.UpdatedAtUtc));
    }
}
