using MediatR;
using ParkingSystem.BuildingBlocks.Results;

namespace ParkingSystem.Parking.Application.Features.Pricing.Queries.GetPricingPolicy;

// CallerId is the signed-in account, when there is one: the route is public, but a lot that is not approved yet is only
// visible to its owners.
public sealed record GetPricingPolicyQuery(Guid LotId, string VehicleType, Guid? CallerId = null) : IRequest<Result<PricingPolicyDto>>;

public sealed record PricingPolicyDto(
    Guid LotId,
    string VehicleType,
    int Version,
    string BillingMode,
    int? BlockMinutes,
    decimal Price,
    decimal OvernightFee,
    DateTime EffectiveFromUtc);
