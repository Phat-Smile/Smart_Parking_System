namespace ParkingSystem.Parking.Application.ParkingLayout;

// US-PARK-008 (BR-07.b): motorbikes have no slots. Each lot has one motorbike zone holding the capacity the owner
// declared for all its motorbike floors, and a count of motorbikes inside (team decision 2026-10-16, option A).
public interface IMotorbikeZoneCounter
{
    /// <summary>
    /// Adds one motorbike to the lot's motorbike zone if it is below capacity, as one atomic statement.
    /// Returns null when the zone is full or the lot has no motorbike zone.
    /// </summary>
    Task<MotorbikeSpaceTaken?> TakeSpaceAsync(Guid lotId, CancellationToken cancellationToken);

    /// <summary>
    /// Gives one space back when a motorbike session completes (exit flow, US-PAY-006). The count never goes below 0.
    /// </summary>
    Task ReleaseSpaceAsync(Guid zoneId, CancellationToken cancellationToken);

    /// <summary>Returns null when the lot has no motorbike zone.</summary>
    Task<MotorbikeAvailability?> GetAvailabilityAsync(Guid lotId, CancellationToken cancellationToken);
}

public sealed record MotorbikeSpaceTaken(Guid ZoneId, int AvailableSpaces);

public sealed record MotorbikeAvailability(int Capacity, int Occupied)
{
    public int Available => Math.Max(0, Capacity - Occupied);
}
