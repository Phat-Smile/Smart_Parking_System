using MediatR;
using ParkingSystem.BuildingBlocks.Results;
using ParkingSystem.Parking.Application.ParkingLots;

namespace ParkingSystem.Parking.Application.ParkingLayout.GetAvailableSlotCount;

// Contract 2, Parking part (master sheet): Booking asks how many car slots of a lot are Available (BR-02.a)
// to check BR-02.c (ii). This is not the walk-in count (BR-02.d) and not the valid capacity.
public sealed record GetAvailableSlotCountQuery(Guid LotId) : IRequest<Result<int>>;

public static class GetAvailableSlotCountErrors
{
    // Unknown and not-approved lots share one error, like the public lot detail API.
    public static readonly Error LotNotFound =
        new("parking.lot.not_found", "The parking lot does not exist or is not approved.");
}

public sealed class GetAvailableSlotCountQueryHandler(
    IParkingLotDetailReader parkingLots,
    IAvailableSlotReader availableSlots) : IRequestHandler<GetAvailableSlotCountQuery, Result<int>>
{
    public async Task<Result<int>> Handle(GetAvailableSlotCountQuery request, CancellationToken cancellationToken)
    {
        if (await parkingLots.GetApprovedByIdAsync(request.LotId, cancellationToken) is null)
            return Result<int>.Failure(GetAvailableSlotCountErrors.LotNotFound);

        var count = await availableSlots.CountAvailableCarSlotsAsync(request.LotId, cancellationToken);
        return Result<int>.Success(count);
    }
}
