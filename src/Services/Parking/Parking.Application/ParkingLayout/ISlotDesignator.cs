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
    /// US-PARK-007: locks the lot for car entry until the current transaction ends, then counts its Available car slots.
    /// DesignateCarSlotAsync alone stops two cars taking the same slot, but not two gates both taking the last walk-in
    /// slot (count, then designate). With this lock the car entries of one lot run one after another.
    /// Must be called inside an IParkingUnitOfWork transaction.
    /// </summary>
    Task<int> LockLotAndCountAvailableCarSlotsAsync(Guid lotId, CancellationToken cancellationToken);

    /// <summary>The code of the slot assigned to each session, for the sessions that hold one (read only).</summary>
    Task<IReadOnlyDictionary<Guid, string>> FindSlotCodesAsync(IReadOnlyCollection<Guid> sessionIds, CancellationToken cancellationToken);

    /// <summary>
    /// Clears the assignment of the slot held by the session, if any, and returns its slot code.
    /// The change is only tracked: it is saved by the caller's next SaveChanges, in the same transaction as the session.
    /// </summary>
    Task<string?> ReleaseSessionSlotAsync(Guid sessionId, CancellationToken cancellationToken);
}

// SlotCode is what the driver is told at the gate (BR-04.a).
public sealed record DesignatedSlot(Guid SlotId, Guid ZoneId, string SlotCode);
