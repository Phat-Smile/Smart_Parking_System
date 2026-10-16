namespace ParkingSystem.Parking.Application.ParkingLayout;

public interface ISlotDesignator
{
    /// <summary>
    /// Picks one Available car slot of the lot and marks it Assigned to the session, as one atomic step (BR-04.a, BR-04.g).
    /// Two concurrent calls never get the same slot. Returns null when no Available car slot is left;
    /// the caller then applies BR-02.f (car with a booking) or BR-04.h (walk-in car).
    /// </summary>
    Task<DesignatedSlot?> DesignateCarSlotAsync(Guid lotId, Guid sessionId, DateTimeOffset assignedAt, CancellationToken cancellationToken);

    /// <summary>
    /// Clears the assignment of the slot held by the session, if any, and returns its slot code.
    /// The change is only tracked: it is saved by the caller's next SaveChanges, in the same transaction as the session.
    /// </summary>
    Task<string?> ReleaseSessionSlotAsync(Guid sessionId, CancellationToken cancellationToken);
}

// SlotCode is what the driver is told at the gate (BR-04.a).
public sealed record DesignatedSlot(Guid SlotId, Guid ZoneId, string SlotCode);
