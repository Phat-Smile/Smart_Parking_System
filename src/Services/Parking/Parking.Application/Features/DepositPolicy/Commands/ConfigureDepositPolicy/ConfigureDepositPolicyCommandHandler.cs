using System.Threading;
using System.Threading.Tasks;
using MediatR;
using ParkingSystem.BuildingBlocks.Results;
using ParkingSystem.Parking.Application.Common.Constants;
using ParkingSystem.Parking.Application.Common.Interfaces;
using ParkingSystem.Parking.Domain.Entities;

namespace ParkingSystem.Parking.Application.Features.DepositPolicy.Commands.ConfigureDepositPolicy;

public sealed class ConfigureDepositPolicyCommandHandler : IRequestHandler<ConfigureDepositPolicyCommand, Result<DepositPolicyResponse>>
{
    private readonly IReservationDepositPolicyRepository _repository;
    private readonly ILotOwnershipService _lotOwnershipService;

    public ConfigureDepositPolicyCommandHandler(
        IReservationDepositPolicyRepository repository,
        ILotOwnershipService lotOwnershipService)
    {
        _repository = repository;
        _lotOwnershipService = lotOwnershipService;
    }

    public async Task<Result<DepositPolicyResponse>> Handle(ConfigureDepositPolicyCommand request, CancellationToken cancellationToken)
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

        var existingPolicy = await _repository.GetByLotIdAsync(request.LotId, cancellationToken);
        if (existingPolicy != null)
        {
            existingPolicy.Update(request.HoldDepositVnd);
        }
        else
        {
            var newPolicy = ReservationDepositPolicy.Create(request.LotId, request.HoldDepositVnd);
            await _repository.AddAsync(newPolicy, cancellationToken);
        }

        await _repository.SaveChangesAsync(cancellationToken);

        return Result<DepositPolicyResponse>.Success(
            new DepositPolicyResponse(request.LotId, request.HoldDepositVnd, true));
    }
}
