using System.Threading;
using System.Threading.Tasks;
using MediatR;
using ParkingSystem.BuildingBlocks.Results;
using ParkingSystem.Parking.Application.Common.Constants;
using ParkingSystem.Parking.Application.Common.Interfaces;

namespace ParkingSystem.Parking.Application.Features.ReservationRatioPolicy.Commands.ConfigureReservationRatioPolicy;

public sealed class ConfigureReservationRatioPolicyCommandHandler : IRequestHandler<ConfigureReservationRatioPolicyCommand, Result<ReservationRatioPolicyResponse>>
{
    private readonly IReservationRatioPolicyRepository _repository;
    private readonly ILotOwnershipService _lotOwnershipService;

    public ConfigureReservationRatioPolicyCommandHandler(
        IReservationRatioPolicyRepository repository,
        ILotOwnershipService lotOwnershipService)
    {
        _repository = repository;
        _lotOwnershipService = lotOwnershipService;
    }

    public async Task<Result<ReservationRatioPolicyResponse>> Handle(ConfigureReservationRatioPolicyCommand request, CancellationToken cancellationToken)
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

        var ratio = request.ReservationRatioPercent!.Value;
        var policy = await _repository.UpsertAsync(request.LotId, ratio, cancellationToken);

        return Result<ReservationRatioPolicyResponse>.Success(
            new ReservationRatioPolicyResponse(
                request.LotId,
                policy.ReservationRatioPercent,
                true,
                policy.UpdatedAtUtc));
    }
}
