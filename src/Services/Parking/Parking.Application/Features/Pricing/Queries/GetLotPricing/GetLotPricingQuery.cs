using MediatR;
using ParkingSystem.BuildingBlocks.Results;
using ParkingSystem.Parking.Application.Common.Constants;
using ParkingSystem.Parking.Application.Common.Interfaces;
using ParkingSystem.Parking.Application.Features.Pricing.Queries.GetPricingPolicy;

namespace ParkingSystem.Parking.Application.Features.Pricing.Queries.GetLotPricing;

// API-PK-05: every vehicle type's current price of a lot in one call. Visibility follows GetPricingPolicyQuery:
// an approved lot for everyone, a Pending or Rejected lot only for its owners (MF-15).
public sealed record GetLotPricingQuery(Guid LotId, Guid? CallerId = null) : IRequest<Result<LotPricing>>;

public sealed record LotPricing(Guid LotId, IReadOnlyList<PricingPolicyDto> Items);

public sealed class GetLotPricingQueryHandler(
    IPricingPolicyRepository pricingRepository,
    ILotOwnershipService lotOwnershipService) : IRequestHandler<GetLotPricingQuery, Result<LotPricing>>
{
    public async Task<Result<LotPricing>> Handle(GetLotPricingQuery request, CancellationToken cancellationToken)
    {
        if (request.LotId == Guid.Empty)
            return Result<LotPricing>.Failure(ParkingErrors.Pricing.LotIdEmpty);

        if (!await lotOwnershipService.LotExistsAsync(request.LotId))
            return Result<LotPricing>.Failure(ParkingErrors.Pricing.LotNotFound);

        var visible = await lotOwnershipService.IsLotApprovedAsync(request.LotId)
            || (request.CallerId is { } callerId && await lotOwnershipService.IsLotOwnerAsync(request.LotId, callerId));
        if (!visible)
            return Result<LotPricing>.Failure(ParkingErrors.Pricing.LotNotFound);

        var items = await LotPricingItems.ReadAsync(pricingRepository, request.LotId, cancellationToken);
        return Result<LotPricing>.Success(new LotPricing(request.LotId, items));
    }
}

// Shared by the pricing route and the owner lot detail, so both list prices the same way.
public static class LotPricingItems
{
    private static readonly string[] VehicleTypes = ["CAR", "MOTORBIKE"];

    public static async Task<IReadOnlyList<PricingPolicyDto>> ReadAsync(
        IPricingPolicyRepository pricingRepository, Guid lotId, CancellationToken cancellationToken)
    {
        var items = new List<PricingPolicyDto>(VehicleTypes.Length);
        foreach (var vehicleType in VehicleTypes)
        {
            var policy = await pricingRepository.GetLatestAsync(lotId, vehicleType, cancellationToken);
            if (policy is not null)
                items.Add(new PricingPolicyDto(
                    policy.LotId, policy.VehicleType, policy.Version, policy.BillingMode,
                    policy.BlockMinutes, policy.Price, policy.OvernightFee, policy.EffectiveFromUtc));
        }

        return items;
    }
}
