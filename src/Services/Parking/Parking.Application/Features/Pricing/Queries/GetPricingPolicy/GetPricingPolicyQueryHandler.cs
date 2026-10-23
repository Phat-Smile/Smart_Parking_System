using MediatR;
using ParkingSystem.BuildingBlocks.Results;
using ParkingSystem.Parking.Application.Common.Constants;
using ParkingSystem.Parking.Application.Common.Interfaces;

namespace ParkingSystem.Parking.Application.Features.Pricing.Queries.GetPricingPolicy;

public sealed class GetPricingPolicyQueryHandler(
    IPricingPolicyRepository pricingRepository,
    ILotOwnershipService lotOwnershipService)
    : IRequestHandler<GetPricingPolicyQuery, Result<PricingPolicyDto>>
{
    public async Task<Result<PricingPolicyDto>> Handle(
        GetPricingPolicyQuery request,
        CancellationToken cancellationToken)
    {
        if (request.LotId == Guid.Empty)
        {
            return Result<PricingPolicyDto>.Failure(ParkingErrors.Pricing.LotIdEmpty);
        }

        if (string.IsNullOrWhiteSpace(request.VehicleType))
        {
            return Result<PricingPolicyDto>.Failure(ParkingErrors.Pricing.VehicleTypeEmpty);
        }

        string normalizedVehicleType = request.VehicleType.Trim().ToUpperInvariant();
        if (normalizedVehicleType != "CAR" && normalizedVehicleType != "MOTORBIKE")
        {
            return Result<PricingPolicyDto>.Failure(ParkingErrors.Pricing.VehicleTypeInvalid);
        }

        bool lotExists = await lotOwnershipService.LotExistsAsync(request.LotId);
        if (!lotExists)
        {
            return Result<PricingPolicyDto>.Failure(ParkingErrors.Pricing.LotNotFound);
        }

        var policy = await pricingRepository.GetLatestAsync(request.LotId, normalizedVehicleType, cancellationToken);
        if (policy is null)
        {
            return Result<PricingPolicyDto>.Failure(ParkingErrors.Pricing.PolicyNotFound);
        }

        var dto = new PricingPolicyDto(
            policy.LotId,
            policy.VehicleType,
            policy.Version,
            policy.BillingMode,
            policy.BlockMinutes,
            policy.Price,
            policy.OvernightFee,
            policy.EffectiveFromUtc);

        return Result<PricingPolicyDto>.Success(dto);
    }
}
