using MediatR;
using ParkingSystem.BuildingBlocks.Results;

namespace ParkingSystem.Parking.Application.ParkingLayout.ManageLayout;

// Car utilization uses physical slots, not the owner's declared capacity. Held reservation quotas
// belong to Booking and are not assigned to zones before a vehicle enters (BR-02.g).
public sealed record ParkingZoneUtilization(
    Guid FloorId, Guid ZoneId, string ZoneCode, string VehicleGroup,
    int DeclaredCapacity, int UsableCapacity, int InUse, int Available,
    int Occupied, int Assigned, int NeedsVerification, int Maintenance, int Unavailable,
    double UtilizationPercent);

public sealed record ParkingLotZoneUtilization(Guid LotId, int LayoutVersion, IReadOnlyList<ParkingZoneUtilization> Zones);

public static class ParkingZoneUtilizationCalculator
{
    public static ParkingLotZoneUtilization Calculate(ParkingLotLayout layout)
    {
        var zones = layout.Floors.SelectMany(floor => floor.Zones.Select(zone =>
        {
            if (zone.VehicleGroup.Equals("Motorbike", StringComparison.OrdinalIgnoreCase))
            {
                var motorbikeUsed = Math.Clamp(zone.CurrentVehicleCount, 0, zone.Capacity);
                return new ParkingZoneUtilization(floor.Id, zone.Id, zone.Code, zone.VehicleGroup,
                    zone.Capacity, zone.Capacity, motorbikeUsed, zone.Capacity - motorbikeUsed,
                    motorbikeUsed, 0, 0, 0, 0, Percentage(motorbikeUsed, zone.Capacity));
            }

            var occupied = zone.Slots.Count(s => s.EffectiveStatus == "Occupied");
            var assigned = zone.Slots.Count(s => s.EffectiveStatus == "Assigned");
            var needsVerification = zone.Slots.Count(s => s.EffectiveStatus == "NeedsVerification");
            var maintenance = zone.Slots.Count(s => s.EffectiveStatus == "Maintenance");
            var unavailable = zone.Slots.Count(s => s.EffectiveStatus == "Unavailable");
            var available = zone.Slots.Count(s => s.EffectiveStatus == "Available");
            var usable = zone.Slots.Count - maintenance - unavailable;
            var used = occupied + assigned + needsVerification;
            return new ParkingZoneUtilization(floor.Id, zone.Id, zone.Code, zone.VehicleGroup,
                zone.Capacity, usable, used, available, occupied, assigned, needsVerification,
                maintenance, unavailable, Percentage(used, usable));
        })).ToArray();

        return new ParkingLotZoneUtilization(layout.LotId, layout.Version, zones);
    }

    private static double Percentage(int used, int capacity) =>
        capacity == 0 ? 0 : Math.Round(100d * used / capacity, 2);
}

public sealed record GetOwnerZoneUtilizationQuery(Guid LotId, Guid ActorId)
    : IRequest<Result<ParkingLotZoneUtilization>>;

public sealed class GetOwnerZoneUtilizationQueryHandler(IManageParkingLayoutRepository repository)
    : IRequestHandler<GetOwnerZoneUtilizationQuery, Result<ParkingLotZoneUtilization>>
{
    public async Task<Result<ParkingLotZoneUtilization>> Handle(GetOwnerZoneUtilizationQuery request, CancellationToken cancellationToken)
    {
        if (!await repository.LotExistsAsync(request.LotId, cancellationToken))
            return Result<ParkingLotZoneUtilization>.Failure(ParkingLayoutErrors.LotNotFound);
        if (!await repository.IsOwnerAsync(request.LotId, request.ActorId, cancellationToken))
            return Result<ParkingLotZoneUtilization>.Failure(ParkingLayoutErrors.OwnerRequired);

        var layout = await repository.ReadAsync(request.LotId, cancellationToken);
        return Result<ParkingLotZoneUtilization>.Success(ParkingZoneUtilizationCalculator.Calculate(layout));
    }
}
