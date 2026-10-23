using MediatR;
using ParkingSystem.BuildingBlocks.Results;

namespace ParkingSystem.Parking.Application.Features.Pricing.Queries.GetPricingPolicy;

public sealed record GetPricingPolicyQuery(Guid LotId, string VehicleType) : IRequest<Result<PricingPolicyDto>>;

public sealed record PricingPolicyDto(
    Guid LotId,
    string VehicleType,
    int Version,
    string BillingMode,
    int? BlockMinutes,
    decimal Price,
    decimal OvernightFee,
    DateTime EffectiveFromUtc);
