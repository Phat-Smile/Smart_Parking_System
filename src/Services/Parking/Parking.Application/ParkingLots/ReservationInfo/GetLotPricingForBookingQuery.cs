using MediatR;
using ParkingSystem.BuildingBlocks.Results;
using ParkingSystem.Parking.Application.Common.Interfaces;

namespace ParkingSystem.Parking.Application.ParkingLots.ReservationInfo;

// P0-A, Parking part of the Booking contract: the price list Booking uses for the scheduled prepayment (BR-10.a).
// Internal (service to service). Booking computes the amount; Parking only states the price list.
public sealed record GetLotPricingForBookingQuery(Guid LotId, string VehicleGroup)
    : IRequest<Result<LotPricingForBooking>>;

/// <param name="BillingMode">"PerEntry" or "Block".</param>
/// <param name="BlockMinutes">0 for PerEntry.</param>
public sealed record LotPricingForBooking(
    long Price,
    string BillingMode,
    int BlockMinutes,
    long OvernightFee,
    int PolicyVersion);

public static class LotPricingForBookingErrors
{
    public static readonly Error LotNotFound =
        new("parking.lot.not_found", "The parking lot does not exist or is not approved.");

    public static readonly Error InvalidVehicleGroup =
        new("parking.pricing.vehicle_group_invalid", "Vehicle group must be Car or Motorbike.");

    public static readonly Error PolicyNotFound =
        new("parking.pricing.policy_not_found", "The lot has no price list for this vehicle group.");

    public static readonly Error NotWholeAmount =
        new("parking.pricing.not_whole_amount", "The price list holds an amount that is not a whole number of VND.");
}

public sealed class GetLotPricingForBookingQueryHandler(
    IParkingLotDetailReader parkingLots,
    IPricingPolicyRepository pricingPolicies)
    : IRequestHandler<GetLotPricingForBookingQuery, Result<LotPricingForBooking>>
{
    public async Task<Result<LotPricingForBooking>> Handle(
        GetLotPricingForBookingQuery request,
        CancellationToken cancellationToken)
    {
        var group = request.VehicleGroup?.Trim().ToUpperInvariant();
        if (group is not ("CAR" or "MOTORBIKE"))
            return Result<LotPricingForBooking>.Failure(LotPricingForBookingErrors.InvalidVehicleGroup);

        if (await parkingLots.GetApprovedByIdAsync(request.LotId, cancellationToken) is null)
            return Result<LotPricingForBooking>.Failure(LotPricingForBookingErrors.LotNotFound);

        var policy = await pricingPolicies.GetLatestAsync(request.LotId, group, cancellationToken);
        if (policy is null)
            return Result<LotPricingForBooking>.Failure(LotPricingForBookingErrors.PolicyNotFound);

        // Money is never rounded: a fractional price would silently change what the customer prepays.
        if (policy.Price % 1m != 0m || policy.OvernightFee % 1m != 0m)
            return Result<LotPricingForBooking>.Failure(LotPricingForBookingErrors.NotWholeAmount);

        return Result<LotPricingForBooking>.Success(new LotPricingForBooking(
            (long)policy.Price,
            policy.BillingMode,
            policy.BlockMinutes ?? 0,
            (long)policy.OvernightFee,
            policy.Version));
    }
}
