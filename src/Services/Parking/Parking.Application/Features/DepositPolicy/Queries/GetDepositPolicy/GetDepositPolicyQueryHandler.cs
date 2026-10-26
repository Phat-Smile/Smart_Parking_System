using System.Threading;
using System.Threading.Tasks;
using MediatR;
using ParkingSystem.BuildingBlocks.Results;
using ParkingSystem.Parking.Application.Common.Constants;
using ParkingSystem.Parking.Application.Common.Interfaces;
using ParkingSystem.Parking.Domain.Entities;

namespace ParkingSystem.Parking.Application.Features.DepositPolicy.Queries.GetDepositPolicy;

public sealed class GetDepositPolicyQueryHandler : IRequestHandler<GetDepositPolicyQuery, Result<DepositPolicyResponse>>
{
    private readonly IReservationDepositPolicyRepository _repository;
    private readonly ILotOwnershipService _lotOwnershipService;

    public GetDepositPolicyQueryHandler(
        IReservationDepositPolicyRepository repository,
        ILotOwnershipService lotOwnershipService)
    {
        _repository = repository;
        _lotOwnershipService = lotOwnershipService;
    }

    public async Task<Result<DepositPolicyResponse>> Handle(GetDepositPolicyQuery request, CancellationToken cancellationToken)
    {
        bool lotExists = await _lotOwnershipService.LotExistsAsync(request.LotId);
        if (!lotExists)
        {
            return Result<DepositPolicyResponse>.Failure(ParkingErrors.DepositPolicy.LotNotFound);
        }

        bool isOwner = await _lotOwnershipService.IsLotOwnerAsync(request.LotId, request.CallerId);
        if (!isOwner)
        {
            return Result<DepositPolicyResponse>.Failure(ParkingErrors.DepositPolicy.NotLotOwner);
        }

        var policy = await _repository.GetByLotIdAsync(request.LotId, cancellationToken);
        if (policy == null)
        {
            return Result<DepositPolicyResponse>.Success(
                new DepositPolicyResponse(
                    request.LotId,
                    ReservationDepositPolicy.DefaultHoldDepositVnd,
                    false,
                    null));
        }

        return Result<DepositPolicyResponse>.Success(
            new DepositPolicyResponse(
                request.LotId,
                policy.HoldDepositVnd,
                true,
                policy.UpdatedAtUtc));
    }
}
