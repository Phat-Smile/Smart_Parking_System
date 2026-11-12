using MediatR;
using ParkingSystem.BuildingBlocks.Results;
using ParkingSystem.Parking.Application.Common.Constants;
using ParkingSystem.Parking.Application.Common.Interfaces;

namespace ParkingSystem.Parking.Application.Features.Pricing.Queries.GetPricingPolicy;

// Dedicated admin path: the public query intentionally hides prices of non-approved lots.
// SystemAdminPolicy is mandatory on the only HTTP endpoint dispatching this query.
public sealed record GetAdminPricingPolicyQuery(Guid LotId, string VehicleType) : IRequest<Result<PricingPolicyDto>>;

public sealed class GetAdminPricingPolicyQueryHandler(
    IPricingPolicyRepository pricingRepository,
    ILotOwnershipService lotOwnershipService)
    : IRequestHandler<GetAdminPricingPolicyQuery, Result<PricingPolicyDto>>
{
    public async Task<Result<PricingPolicyDto>> Handle(GetAdminPricingPolicyQuery request, CancellationToken cancellationToken)
    {
        if (request.LotId == Guid.Empty)
            return Result<PricingPolicyDto>.Failure(ParkingErrors.Pricing.LotIdEmpty);
        if (string.IsNullOrWhiteSpace(request.VehicleType))
            return Result<PricingPolicyDto>.Failure(ParkingErrors.Pricing.VehicleTypeEmpty);

        var vehicleType = request.VehicleType.Trim().ToUpperInvariant();
        if (vehicleType is not ("CAR" or "MOTORBIKE"))
            return Result<PricingPolicyDto>.Failure(ParkingErrors.Pricing.VehicleTypeInvalid);

        if (!await lotOwnershipService.LotExistsAsync(request.LotId))
            return Result<PricingPolicyDto>.Failure(ParkingErrors.Pricing.LotNotFound);

        var policy = await pricingRepository.GetLatestAsync(request.LotId, vehicleType, cancellationToken);
        if (policy is null)
            return Result<PricingPolicyDto>.Failure(ParkingErrors.Pricing.PolicyNotFound);

        return Result<PricingPolicyDto>.Success(new PricingPolicyDto(
            policy.LotId, policy.VehicleType, policy.Version, policy.BillingMode,
            policy.BlockMinutes, policy.Price, policy.OvernightFee, policy.EffectiveFromUtc));
    }
}
