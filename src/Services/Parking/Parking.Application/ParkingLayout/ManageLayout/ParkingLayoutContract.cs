using ParkingSystem.BuildingBlocks.Results;
using ParkingSystem.Parking.Domain.ParkingLayout;
using ParkingSystem.Parking.Domain.ParkingSessions;

namespace ParkingSystem.Parking.Application.ParkingLayout.ManageLayout;

// Stable client-generated GUIDs are required. Existing IDs must be submitted on subsequent saves.
public sealed record GridPoint(int X, int Y);
public sealed record GridBounds(int X, int Y, int Width, int Height);
public sealed record LayoutSlotInput(Guid Id, string? SlotCode, string? OverrideState, GridPoint? Position = null);
public sealed record LayoutZoneInput(Guid Id, string? Code, string? VehicleGroup, int Capacity, bool HasSlotSensor, IReadOnlyList<LayoutSlotInput>? Slots, GridBounds? Bounds = null, IReadOnlyList<GridPoint>? Polygon = null);
public sealed record LayoutFloorInput(Guid Id, string? Name, IReadOnlyList<LayoutZoneInput>? Zones);

public sealed record LayoutSlot(Guid Id, string SlotCode, string? OverrideState, string EffectiveStatus, GridPoint? Position);
public sealed record LayoutZone(Guid Id, string Code, string VehicleGroup, int Capacity, int CurrentVehicleCount, bool HasSlotSensor, IReadOnlyList<LayoutSlot> Slots, GridBounds? Bounds, IReadOnlyList<GridPoint>? Polygon);
public sealed record LayoutFloor(Guid Id, string Name, IReadOnlyList<LayoutZone> Zones);
public sealed record ParkingLotLayout(Guid LotId, int Version, IReadOnlyList<LayoutFloor> Floors);

public static class ParkingLayoutErrors
{
    public static readonly Error LotNotFound = new("parking.layout.lot_not_found", "The parking lot does not exist.");
    public static readonly Error OwnerRequired = new("parking.layout.owner_required", "You must be assigned as Owner of this parking lot.");
    public static readonly Error Conflict = new("parking.layout.conflict", "Existing floors, zones and slots must retain their IDs and parents. Removing layout items or editing an active slot is not supported.");
    public static readonly Error StaleVersion = new("parking.layout.stale_version", "Layout has changed. Read the latest version before saving.");
    public static readonly Error DuplicateId = new("parking.layout.duplicate_id", "A layout ID already belongs to another parking lot.");
}

public static class ParkingLayoutInputValidator
{
    public static Error? Validate(IReadOnlyList<LayoutFloorInput>? floors)
    {
        if (floors is null)
            return Invalid("floors is required.");

        var floorIds = new HashSet<Guid>();
        var zoneIds = new HashSet<Guid>();
        var slotIds = new HashSet<Guid>();
        var floorNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var lotSlotCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var floor in floors)
        {
            if (floor is null || floor.Id == Guid.Empty || !floorIds.Add(floor.Id)
                || !ValidName(floor.Name, Floor.NameMaxLength) || !floorNames.Add(floor.Name!.Trim()))
                return Invalid("Each floor requires a unique non-empty ID and name (max 100 characters).");
            if (floor.Zones is null)
                return Invalid("zones is required for every floor.");
            var zoneCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var zone in floor.Zones)
            {
                if (zone is null || zone.Id == Guid.Empty || !zoneIds.Add(zone.Id)
                    || !ValidName(zone.Code, Zone.CodeMaxLength) || !zoneCodes.Add(zone.Code!.Trim()))
                    return Invalid("Each zone requires a unique non-empty ID and code within its floor (max 50 characters).");
                if (zone.Capacity < 0 || !Enum.GetNames<VehicleType>().Contains(zone.VehicleGroup, StringComparer.OrdinalIgnoreCase))
                    return Invalid("Zone capacity must be >= 0 and vehicleGroup must be Car or Motorbike.");
                if (zone.Slots is null)
                    return Invalid("slots is required for every zone.");
                var isCar = zone.VehicleGroup!.Equals(nameof(VehicleType.Car), StringComparison.OrdinalIgnoreCase);
                if (!isCar && zone.Slots.Count != 0)
                    return Invalid("Motorbike zones must not contain individual slots.");
                if (isCar && zone.Slots.Count > zone.Capacity)
                    return Invalid("A car zone cannot contain more slots than its declared capacity.");
                if (zone.Bounds is not null && (zone.Bounds.Width <= 0 || zone.Bounds.Height <= 0))
                    return Invalid("bounds width and height must be positive.");
                if (zone.Polygon is not null && zone.Polygon.Count < 3)
                    return Invalid("polygon must have at least three points.");
                if (!isCar && ((zone.Bounds is null) == (zone.Polygon is null)))
                    return Invalid("Motorbike zones require exactly one geometry: bounds or polygon.");
                var positions = new HashSet<GridPoint>();
                foreach (var slot in zone.Slots)
                {
                    if (slot is null || slot.Id == Guid.Empty || !slotIds.Add(slot.Id)
                        || !ValidName(slot.SlotCode, ParkingSlot.SlotCodeMaxLength)
                        || !lotSlotCodes.Add(ParkingSlot.NormalizeSlotCode(slot.SlotCode!)))
                        return Invalid("Each car slot requires a unique non-empty ID and code within the lot (max 20 characters).");
                    if (slot.Position is null || !positions.Add(slot.Position))
                        return Invalid("Every car slot requires a unique grid position within its zone.");
                    if (slot.OverrideState is not null
                        && !Enum.GetNames<OverrideState>().Contains(slot.OverrideState, StringComparer.OrdinalIgnoreCase))
                        return Invalid("overrideState must be null, Maintenance or Unavailable.");
                }
            }
        }
        return null;
    }

    private static bool ValidName(string? name, int maxLength) =>
        !string.IsNullOrWhiteSpace(name) && name.Trim().Length <= maxLength;

    private static Error Invalid(string message) => new("parking.layout.invalid", message.Trim());
}
