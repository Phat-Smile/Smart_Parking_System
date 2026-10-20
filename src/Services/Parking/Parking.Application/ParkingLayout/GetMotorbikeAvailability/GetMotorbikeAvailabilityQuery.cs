using FluentValidation;
using MediatR;
using ParkingSystem.BuildingBlocks.Identity;
using ParkingSystem.BuildingBlocks.Results;
using ParkingSystem.Parking.Application.ParkingLots;

namespace ParkingSystem.Parking.Application.ParkingLayout.GetMotorbikeAvailability;

// US-PARK-008: the staff screen shows how many motorbike spaces are left, so staff decide whether to accept
// a motorbike and print its QR (team decision 2026-10-16).
public sealed record GetMotorbikeAvailabilityQuery(Guid LotId, Guid ActorUserId) : IRequest<Result<MotorbikeAvailabilityResponse>>;

public sealed record MotorbikeAvailabilityResponse(Guid LotId, int Capacity, int Occupied, int Available, bool IsFull);

public static class GetMotorbikeAvailabilityErrors
{
    public static readonly Error LotNotFound =
        new("parking.lot.not_found", "The parking lot does not exist or is not approved.");

    public static readonly Error MotorbikeZoneNotConfigured =
        new("parking.motorbike_zone.not_configured", "This lot has no motorbike zone.");
}

public sealed class GetMotorbikeAvailabilityQueryValidator : AbstractValidator<GetMotorbikeAvailabilityQuery>
{
    public GetMotorbikeAvailabilityQueryValidator()
    {
        RuleFor(query => query.LotId).NotEmpty();
        RuleFor(query => query.ActorUserId).NotEmpty();
    }
}

public sealed class GetMotorbikeAvailabilityQueryHandler(
    IParkingLotDetailReader parkingLots,
    IParkingLotAccess lotAccess,
    IMotorbikeZoneCounter motorbikeZones) : IRequestHandler<GetMotorbikeAvailabilityQuery, Result<MotorbikeAvailabilityResponse>>
{
    public async Task<Result<MotorbikeAvailabilityResponse>> Handle(GetMotorbikeAvailabilityQuery request, CancellationToken cancellationToken)
    {
        if (await parkingLots.GetApprovedByIdAsync(request.LotId, cancellationToken) is null)
            return Result<MotorbikeAvailabilityResponse>.Failure(GetMotorbikeAvailabilityErrors.LotNotFound);

        // TECH-12: only staff assigned to the lot see its counts.
        if (!await lotAccess.CanOperateAsync(request.ActorUserId, request.LotId, cancellationToken))
            return Result<MotorbikeAvailabilityResponse>.Failure(ParkingLotAccessErrors.AccessDenied);

        var availability = await motorbikeZones.GetAvailabilityAsync(request.LotId, cancellationToken);
        if (availability is null)
            return Result<MotorbikeAvailabilityResponse>.Failure(GetMotorbikeAvailabilityErrors.MotorbikeZoneNotConfigured);

        return Result<MotorbikeAvailabilityResponse>.Success(new MotorbikeAvailabilityResponse(
            request.LotId,
            availability.Capacity,
            availability.Occupied,
            availability.Available,
            availability.Available == 0));
    }
}
