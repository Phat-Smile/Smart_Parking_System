using MediatR;
using ParkingSystem.BuildingBlocks.Results;
using ParkingSystem.Parking.Application.ParkingLayout.HeldQuotaShortage;

namespace ParkingSystem.Parking.Application.ParkingLayout.ZoneCapacity;

// US-OWNER-003: the owner sees the declared capacity of every zone and the totals per vehicle group.
// Floors and zones are created and edited by the layout API (US-OWNER-007, Khoa); this query only reads them.
public sealed record GetZoneCapacityQuery(Guid LotId, Guid OwnerAccountId) : IRequest<Result<ZoneCapacityView>>;

public sealed record ZoneCapacityView(
    Guid LotId,
    IReadOnlyList<FloorCapacity> Floors,
    CapacityTotals Totals,
    IReadOnlyList<CapacityWarning> Warnings);

public sealed record FloorCapacity(Guid FloorId, string Name, IReadOnlyList<ZoneCapacityItem> Zones);

/// <param name="SlotCount">Car zone: slots drawn in the layout. Null for motorbike.</param>
/// <param name="ValidCapacity">Car zone: slots not in Maintenance or Unavailable (BR-02.a). Null for motorbike.</param>
/// <param name="VehiclesInside">Car: slots Assigned, Occupied or NeedsVerification. Motorbike: bikes inside (BR-07.b).</param>
public sealed record ZoneCapacityItem(
    Guid ZoneId,
    string Code,
    string VehicleGroup,
    int Capacity,
    int? SlotCount,
    int? ValidCapacity,
    int VehiclesInside);

public sealed record CapacityTotals(int CarCapacity, int MotorbikeCapacity);

public static class ZoneCapacityErrors
{
    // Same rule as the other owner APIs: never disclose whether another owner's lot exists.
    public static readonly Error LotNotFoundOrNotOwned = new(
        "parking.zones.lot_not_found", "The parking lot does not exist or you do not own it.");
}

public interface IZoneCapacityReader
{
    // Owner = a row with role Owner in parking_lot_assignments (team rule confirmed by the lead, 2026-10-23).
    Task<bool> IsOwnerAsync(Guid lotId, Guid ownerAccountId, CancellationToken cancellationToken);

    Task<IReadOnlyList<FloorCapacity>> ReadAsync(Guid lotId, CancellationToken cancellationToken);
}

public sealed class GetZoneCapacityQueryHandler(IZoneCapacityReader reader, IHeldQuotaShortageCheck shortageCheck)
    : IRequestHandler<GetZoneCapacityQuery, Result<ZoneCapacityView>>
{
    public async Task<Result<ZoneCapacityView>> Handle(GetZoneCapacityQuery request, CancellationToken cancellationToken)
    {
        if (!await reader.IsOwnerAsync(request.LotId, request.OwnerAccountId, cancellationToken))
            return Result<ZoneCapacityView>.Failure(ZoneCapacityErrors.LotNotFoundOrNotOwned);

        var floors = await reader.ReadAsync(request.LotId, cancellationToken);
        var zones = floors.SelectMany(floor => floor.Zones).ToList();
        var totals = new CapacityTotals(
            zones.Where(zone => zone.VehicleGroup == "Car").Sum(zone => zone.Capacity),
            zones.Where(zone => zone.VehicleGroup == "Motorbike").Sum(zone => zone.Capacity));

        // BR-02.f: shown on the owner's screen, never blocking.
        var warning = await shortageCheck.CheckAsync(request.LotId, cancellationToken);
        return Result<ZoneCapacityView>.Success(
            new ZoneCapacityView(request.LotId, floors, totals, warning is null ? [] : [warning]));
    }
}
