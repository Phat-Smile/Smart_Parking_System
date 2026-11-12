using System.Threading;
using System.Threading.Tasks;
using MediatR;
using ParkingSystem.BuildingBlocks.Results;
using ParkingSystem.Parking.Application.Common.Constants;
using ParkingSystem.Parking.Application.Common.Interfaces;
using DomainEntity = ParkingSystem.Parking.Domain.Entities.ReservationAvailabilityPolicy;

namespace ParkingSystem.Parking.Application.Features.ReservationAvailabilityPolicy.Queries.GetReservationAvailabilityPolicy;

public sealed class GetReservationAvailabilityPolicyQueryHandler : IRequestHandler<GetReservationAvailabilityPolicyQuery, Result<ReservationAvailabilityPolicyResponse>>
{
    private readonly IReservationAvailabilityPolicyRepository _repository;
    private readonly ILotOwnershipService _lotOwnershipService;

    public GetReservationAvailabilityPolicyQueryHandler(
        IReservationAvailabilityPolicyRepository repository,
        ILotOwnershipService lotOwnershipService)
    {
        _repository = repository;
        _lotOwnershipService = lotOwnershipService;
    }

    public async Task<Result<ReservationAvailabilityPolicyResponse>> Handle(GetReservationAvailabilityPolicyQuery request, CancellationToken cancellationToken)
    {
        bool lotExists = await _lotOwnershipService.LotExistsAsync(request.LotId);
        if (!lotExists)
        {
            return Result<ReservationAvailabilityPolicyResponse>.Failure(ParkingErrors.ReservationAvailabilityPolicy.LotNotFound);
        }

        bool isOwner = await _lotOwnershipService.IsLotOwnerAsync(request.LotId, request.CallerId);
        if (!isOwner)
        {
            return Result<ReservationAvailabilityPolicyResponse>.Failure(ParkingErrors.ReservationAvailabilityPolicy.NotLotOwner);
        }

        var policy = await _repository.GetByLotIdAsync(request.LotId, cancellationToken);
        if (policy == null)
        {
            return Result<ReservationAvailabilityPolicyResponse>.Success(
                new ReservationAvailabilityPolicyResponse(
                    request.LotId,
                    DomainEntity.DefaultReservationEnabled,
                    false,
                    null));
        }

        return Result<ReservationAvailabilityPolicyResponse>.Success(
            new ReservationAvailabilityPolicyResponse(
                request.LotId,
                policy.ReservationEnabled,
                true,
                policy.UpdatedAtUtc));
    }
}
