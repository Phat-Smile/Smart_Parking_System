using MediatR;
using ParkingSystem.BuildingBlocks.Results;
using ParkingSystem.Parking.Application.Common.Interfaces;
using ParkingSystem.Parking.Application.ParkingLayout;
using ParkingSystem.Parking.Application.ParkingLayout.ZoneCapacity;
using ParkingSystem.Parking.Domain.Entities;

namespace ParkingSystem.Parking.Application.ParkingLots.ReservationInfo;

// P0-A, Parking part of the Booking contract: the lot facts Booking needs to create a reservation hold.
// Internal (service to service). Unlike the owner policy routes it carries no user identity.
public sealed record GetLotReservationInfoQuery(Guid LotId) : IRequest<Result<LotReservationInfo>>;

/// <param name="ValidCapacity">BR-02.a: car slots not in Maintenance or Unavailable.</param>
/// <param name="DepositAmount">VND, whole amount.</param>
public sealed record LotReservationInfo(
    int ValidCapacity,
    int ReservationRatioPercent,
    int AvailableSlotCount,
    bool ReservationEnabled,
    long DepositAmount);

public static class LotReservationFactsErrors
{
    // Unknown and not-approved lots share one error, like the public lot detail API.
    public static readonly Error LotNotFound =
        new("parking.lot.not_found", "The parking lot does not exist or is not approved.");

    public static readonly Error NotWholeAmount =
        new("parking.reservation_info.not_whole_amount", "The configured amount is not a whole number of VND.");
}

public sealed class GetLotReservationInfoQueryHandler(
    IParkingLotDetailReader parkingLots,
    IZoneCapacityReader zones,
    IAvailableSlotReader availableSlots,
    IReservationRatioPolicyRepository ratioPolicies,
    IReservationDepositPolicyRepository depositPolicies,
    IReservationAvailabilityPolicyRepository availabilityPolicies)
    : IRequestHandler<GetLotReservationInfoQuery, Result<LotReservationInfo>>
{
    public async Task<Result<LotReservationInfo>> Handle(
        GetLotReservationInfoQuery request,
        CancellationToken cancellationToken)
    {
        if (await parkingLots.GetApprovedByIdAsync(request.LotId, cancellationToken) is null)
            return Result<LotReservationInfo>.Failure(LotReservationFactsErrors.LotNotFound);

        var floors = await zones.ReadAsync(request.LotId, cancellationToken);
        var validCapacity = floors
            .SelectMany(floor => floor.Zones)
            .Where(zone => zone.VehicleGroup == "Car")
            .Sum(zone => zone.ValidCapacity ?? 0);

        var ratio = await ratioPolicies.GetByLotIdAsync(request.LotId, cancellationToken);
        var deposit = await depositPolicies.GetByLotIdAsync(request.LotId, cancellationToken);
        var availability = await availabilityPolicies.GetByLotIdAsync(request.LotId, cancellationToken);
        var available = await availableSlots.CountAvailableCarSlotsAsync(request.LotId, cancellationToken);

        var depositVnd = deposit?.HoldDepositVnd ?? ReservationDepositPolicy.DefaultHoldDepositVnd;
        if (depositVnd % 1m != 0m)
            return Result<LotReservationInfo>.Failure(LotReservationFactsErrors.NotWholeAmount);

        return Result<LotReservationInfo>.Success(new LotReservationInfo(
            validCapacity,
            ratio?.ReservationRatioPercent ?? ReservationRatioPolicy.DefaultReservationRatioPercent,
            available,
            availability?.ReservationEnabled ?? ReservationAvailabilityPolicy.DefaultReservationEnabled,
            (long)depositVnd));
    }
}
