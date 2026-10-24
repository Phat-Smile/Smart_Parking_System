using MediatR;
using ParkingSystem.BuildingBlocks.Results;
using ParkingSystem.Parking.Application.ParkingLayout.HeldQuotaShortage;

namespace ParkingSystem.Parking.Application.ParkingLayout.ManageLayout;

public sealed record GetParkingLayoutQuery(Guid LotId, Guid ActorId) : IRequest<Result<ParkingLotLayout>>;

public sealed record SaveParkingLayoutCommand(Guid LotId, Guid ActorId, int ExpectedVersion, IReadOnlyList<LayoutFloorInput>? Floors)
    : IRequest<Result<ParkingLotLayout>>;

public sealed class GetParkingLayoutQueryHandler(IManageParkingLayoutRepository repository)
    : IRequestHandler<GetParkingLayoutQuery, Result<ParkingLotLayout>>
{
    public async Task<Result<ParkingLotLayout>> Handle(GetParkingLayoutQuery request, CancellationToken cancellationToken)
    {
        if (!await repository.LotExistsAsync(request.LotId, cancellationToken))
            return Result<ParkingLotLayout>.Failure(ParkingLayoutErrors.LotNotFound);
        if (!await repository.IsOwnerAsync(request.LotId, request.ActorId, cancellationToken))
            return Result<ParkingLotLayout>.Failure(ParkingLayoutErrors.OwnerRequired);
        return Result<ParkingLotLayout>.Success(await repository.ReadAsync(request.LotId, cancellationToken));
    }
}

public sealed class SaveParkingLayoutCommandHandler(
    IManageParkingLayoutRepository repository, IHeldQuotaShortageCheck shortageCheck)
    : IRequestHandler<SaveParkingLayoutCommand, Result<ParkingLotLayout>>
{
    public async Task<Result<ParkingLotLayout>> Handle(SaveParkingLayoutCommand request, CancellationToken cancellationToken)
    {
        if (!await repository.LotExistsAsync(request.LotId, cancellationToken))
            return Result<ParkingLotLayout>.Failure(ParkingLayoutErrors.LotNotFound);
        if (!await repository.IsOwnerAsync(request.LotId, request.ActorId, cancellationToken))
            return Result<ParkingLotLayout>.Failure(ParkingLayoutErrors.OwnerRequired);
        var invalid = ParkingLayoutInputValidator.Validate(request.Floors);
        if (invalid is not null)
            return Result<ParkingLotLayout>.Failure(invalid);
        var conflict = await repository.SaveAsync(request.LotId, request.ActorId, request.ExpectedVersion, request.Floors!, cancellationToken);
        if (conflict is not null)
            return Result<ParkingLotLayout>.Failure(conflict);
        var layout = await repository.ReadAsync(request.LotId, cancellationToken);
        var warning = await shortageCheck.CheckAsync(request.LotId, cancellationToken);
        return Result<ParkingLotLayout>.Success(layout with
        {
            Warnings = warning is null ? [] : [warning]
        });
    }
}
