using System.Threading;
using System.Threading.Tasks;
using MediatR;
using ParkingSystem.BuildingBlocks.Results;
using ParkingSystem.Parking.Application.Common.Constants;
using ParkingSystem.Parking.Application.Common.Interfaces;

namespace ParkingSystem.Parking.Application.Features.ReservationAvailabilityPolicy.Commands.ConfigureReservationAvailabilityPolicy;

public sealed class ConfigureReservationAvailabilityPolicyCommandHandler : IRequestHandler<ConfigureReservationAvailabilityPolicyCommand, Result<ReservationAvailabilityPolicyResponse>>
{
    private readonly IReservationAvailabilityPolicyRepository _repository;
    private readonly ILotOwnershipService _lotOwnershipService;

    public ConfigureReservationAvailabilityPolicyCommandHandler(
        IReservationAvailabilityPolicyRepository repository,
        ILotOwnershipService lotOwnershipService)
    {
        _repository = repository;
        _lotOwnershipService = lotOwnershipService;
    }

    public async Task<Result<ReservationAvailabilityPolicyResponse>> Handle(ConfigureReservationAvailabilityPolicyCommand request, CancellationToken cancellationToken)
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

        var enabled = request.ReservationEnabled!.Value;
        var policy = await _repository.UpsertAsync(request.LotId, enabled, cancellationToken);

        return Result<ReservationAvailabilityPolicyResponse>.Success(
            new ReservationAvailabilityPolicyResponse(
                request.LotId,
                policy.ReservationEnabled,
                true,
                policy.UpdatedAtUtc));
    }
}
