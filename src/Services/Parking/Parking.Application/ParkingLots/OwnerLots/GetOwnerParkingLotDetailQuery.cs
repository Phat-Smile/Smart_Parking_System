using MediatR;
using ParkingSystem.BuildingBlocks.Results;
using ParkingSystem.Parking.Application.Common.Interfaces;
using ParkingSystem.Parking.Application.Features.Pricing.Queries.GetLotPricing;
using ParkingSystem.Parking.Application.Features.Pricing.Queries.GetPricingPolicy;
using ParkingSystem.Parking.Application.ParkingLots.AdminLots;
using ParkingSystem.Parking.Application.ParkingLots.OpeningHours;

namespace ParkingSystem.Parking.Application.ParkingLots.OwnerLots;

// API-PK-02: an owner reads one of their lots in any approval state, so a Pending or Rejected lot can be checked
// and fixed. The public detail hides such lots (MF-15).
public sealed record GetOwnerParkingLotDetailQuery(Guid LotId, Guid OwnerUserId) : IRequest<Result<OwnerParkingLotDetail>>;

public sealed record OwnerParkingLotDetail(
    Guid Id,
    string Name,
    string Address,
    decimal Latitude,
    decimal Longitude,
    string Type,
    string ApprovalStatus,
    string? RejectionReason,
    WeeklyOpeningHours? OpeningHours,
    IReadOnlyList<PricingPolicyDto> Pricing);

public static class GetOwnerParkingLotDetailErrors
{
    public static readonly Error LotNotFound =
        new("parking.lot_not_found", "The parking lot does not exist.");
}

public sealed class GetOwnerParkingLotDetailQueryHandler(
    IAdminParkingLotDetailReader lots,
    ILotOwnershipService lotOwnershipService,
    IPricingPolicyRepository pricingRepository) : IRequestHandler<GetOwnerParkingLotDetailQuery, Result<OwnerParkingLotDetail>>
{
    public async Task<Result<OwnerParkingLotDetail>> Handle(GetOwnerParkingLotDetailQuery request, CancellationToken cancellationToken)
    {
        // The admin reader returns a lot in any approval state; ownership is checked right after.
        var lot = await lots.GetByIdAsync(request.LotId, cancellationToken);
        if (lot is null)
            return Result<OwnerParkingLotDetail>.Failure(GetOwnerParkingLotDetailErrors.LotNotFound);

        if (!await lotOwnershipService.IsLotOwnerAsync(request.LotId, request.OwnerUserId))
            return Result<OwnerParkingLotDetail>.Failure(ParkingLotAccessErrors.AccessDenied);

        var pricing = await LotPricingItems.ReadAsync(pricingRepository, request.LotId, cancellationToken);
        return Result<OwnerParkingLotDetail>.Success(new OwnerParkingLotDetail(
            lot.Id, lot.Name, lot.Address, lot.Latitude, lot.Longitude, lot.Type,
            lot.ApprovalStatus, lot.RejectionReason, lot.OpeningHours, pricing));
    }
}
