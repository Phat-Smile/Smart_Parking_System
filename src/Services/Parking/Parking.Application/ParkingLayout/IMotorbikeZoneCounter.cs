namespace ParkingSystem.Parking.Application.ParkingLayout;

// US-PARK-008 (BR-07.b): motorbikes have no slots. A lot can have several motorbike zones (one per floor area),
// each with the capacity the owner declared and a count of motorbikes inside (context 6.3, team decision 2026-10-22).
public interface IMotorbikeZoneCounter
{
    /// <summary>
    /// Adds one motorbike to the lot's motorbike zone with the most free spaces, as one atomic statement per zone.
    /// The rider is told the chosen zone (printed with the QR). Returns null when every motorbike zone is full
    /// or the lot has no motorbike zone.
    /// </summary>
    Task<MotorbikeSpaceTaken?> TakeSpaceAsync(Guid lotId, CancellationToken cancellationToken);

    /// <summary>
    /// Gives one space back when a motorbike session completes (exit flow, US-PAY-006). The count never goes below 0.
    /// </summary>
    Task ReleaseSpaceAsync(Guid zoneId, CancellationToken cancellationToken);

    /// <summary>Returns null when the lot has no motorbike zone.</summary>
    Task<MotorbikeAvailability?> GetAvailabilityAsync(Guid lotId, CancellationToken cancellationToken);
}

// AvailableSpaces is for the whole lot right after this entry, so the staff screen can refresh its total.
public sealed record MotorbikeSpaceTaken(Guid ZoneId, string ZoneCode, string FloorName, int AvailableSpaces);

public sealed record MotorbikeZoneAvailability(Guid ZoneId, string ZoneCode, string FloorName, int Capacity, int Occupied)
{
    // A zone whose capacity was lowered below its count shows 0, never a negative number.
    public int Available => Math.Max(0, Capacity - Occupied);
}

public sealed record MotorbikeAvailability(IReadOnlyList<MotorbikeZoneAvailability> Zones)
{
    public int Capacity => Zones.Sum(zone => zone.Capacity);
    public int Occupied => Zones.Sum(zone => zone.Occupied);
    public int Available => Zones.Sum(zone => zone.Available);
}
