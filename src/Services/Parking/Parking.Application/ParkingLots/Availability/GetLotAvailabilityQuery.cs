using FluentValidation;
using MediatR;
using ParkingSystem.BuildingBlocks.Results;
using ParkingSystem.BuildingBlocks.Time;
using ParkingSystem.Parking.Application.ParkingLayout;
using ParkingSystem.Parking.Application.ParkingLayout.HeldQuotaShortage;

namespace ParkingSystem.Parking.Application.ParkingLots.Availability;

// API-PK-06: the free spaces a customer sees on an approved lot's detail page.
// Car: slots whose effective status is Available (BR-02.a). Motorbike: zone capacity minus the zone count.
public sealed record GetLotAvailabilityQuery(Guid LotId) : IRequest<Result<LotAvailability>>;

public sealed record LotAvailability(Guid LotId, DateTimeOffset AsOfUtc, CarAvailability Car, MotorbikeLotAvailability? Motorbike);

public sealed record CarAvailability(
    int Available,
    int WalkInPlaces = 0,
    string? WalkInStatus = null,
    string? WalkInMessage = null);

public sealed record MotorbikeLotAvailability(int Capacity, int Available, IReadOnlyList<MotorbikeZoneSpaces> Zones);

public sealed record MotorbikeZoneSpaces(Guid ZoneId, string ZoneCode, string FloorName, int Capacity, int Available);

public static class GetLotAvailabilityErrors
{
    // Unknown and not-approved lots share one error, like the public lot detail.
    public static readonly Error LotNotFound =
        new("parking.lot_not_found", "The parking lot does not exist or is not approved.");
}

public sealed class GetLotAvailabilityQueryValidator : AbstractValidator<GetLotAvailabilityQuery>
{
    public GetLotAvailabilityQueryValidator()
    {
        RuleFor(query => query.LotId).NotEmpty();
    }
}

public sealed class GetLotAvailabilityQueryHandler(
    IParkingLotDetailReader parkingLots,
    IAvailableSlotReader availableSlots,
    IMotorbikeZoneCounter motorbikeZones,
    IClock clock,
    IHeldQuotaProvider heldQuotaProvider) : IRequestHandler<GetLotAvailabilityQuery, Result<LotAvailability>>
{
    public async Task<Result<LotAvailability>> Handle(GetLotAvailabilityQuery request, CancellationToken cancellationToken)
    {
        if (await parkingLots.GetApprovedByIdAsync(request.LotId, cancellationToken) is null)
            return Result<LotAvailability>.Failure(GetLotAvailabilityErrors.LotNotFound);

        var car = await availableSlots.CountAvailableCarSlotsAsync(request.LotId, cancellationToken);
        var motorbike = await motorbikeZones.GetAvailabilityAsync(request.LotId, cancellationToken);

        var quota = await heldQuotaProvider.GetHeldReservationsAsync(request.LotId, cancellationToken);
        var held = quota ?? 0;

        var (walkInPlaces, walkInStatus, walkInMessage) = WalkInPlacesCalculator.Calculate(car, held);

        return Result<LotAvailability>.Success(new LotAvailability(
            request.LotId,
            clock.UtcNow,
            new CarAvailability(car, walkInPlaces, walkInStatus, walkInMessage),
            motorbike is null
                ? null
                : new MotorbikeLotAvailability(
                    motorbike.Capacity,
                    motorbike.Available,
                    motorbike.Zones
                        .Select(zone => new MotorbikeZoneSpaces(zone.ZoneId, zone.ZoneCode, zone.FloorName, zone.Capacity, zone.Available))
                        .ToList())));
    }
}
