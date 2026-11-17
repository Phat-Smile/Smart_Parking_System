using Microsoft.EntityFrameworkCore;
using ParkingSystem.Parking.Application.ParkingLayout;
using ParkingSystem.Parking.Domain.ParkingLayout;
using ParkingSystem.Parking.Domain.ParkingSessions;

namespace ParkingSystem.Parking.Persistence.ParkingLayout;

internal sealed class SlotDesignator(ParkingDbContext dbContext) : ISlotDesignator, IAvailableCarSlotCounter
{
    private const string CarEntryLockPrefix = "parking.car-entry:";

    public async Task<DesignatedSlot?> DesignateCarSlotAsync(
        Guid lotId,
        Guid sessionId,
        DateTimeOffset assignedAt,
        CancellationToken cancellationToken)
    {
        // BR-04.g: choosing the slot and marking it Assigned is a single SQL statement, so there is no gap
        // between reading "Available" and writing "Assigned".
        // FOR UPDATE SKIP LOCKED: when two cars enter at the same time, the second statement skips the row the first
        // one is taking and picks the next Available slot, so they never get the same slot and nobody waits or retries.
        // An Available slot has no override, no dispatch state and no car on its sensor, so setting the dispatch layer
        // to Assigned makes its effective status Assigned (same result as ParkingSlot.ComputeEffectiveStatus).
        // BR-04.a does not fix an order; ordering by slot code keeps the choice predictable.
        var designated = await dbContext.Database.SqlQuery<DesignatedSlotRow>($"""
            UPDATE parking.parking_slots AS slot
            SET dispatch_state = 'Assigned',
                effective_status = 'Assigned',
                assigned_session_id = {sessionId},
                assigned_at = {assignedAt},
                row_version = slot.row_version + 1
            WHERE slot.id = (
                SELECT candidate.id
                FROM parking.parking_slots AS candidate
                JOIN parking.zones AS zone ON zone.id = candidate.zone_id
                WHERE candidate.lot_id = {lotId}
                  AND candidate.effective_status = 'Available'
                  AND zone.vehicle_group = 'Car'
                ORDER BY candidate.slot_code
                LIMIT 1
                FOR UPDATE OF candidate SKIP LOCKED)
            RETURNING slot.id AS "SlotId", slot.zone_id AS "ZoneId", slot.slot_code AS "SlotCode"
            """).ToListAsync(cancellationToken);

        return designated.Count == 0
            ? null
            : new DesignatedSlot(designated[0].SlotId, designated[0].ZoneId, designated[0].SlotCode);
    }

    public async Task<int> LockLotAndCountAvailableCarSlotsAsync(Guid lotId, CancellationToken cancellationToken)
    {
        // Transaction-level advisory lock: released by PostgreSQL itself at commit or rollback, so an error cannot leave
        // the lot locked. The key is a hash of the lot id with a prefix, so it does not mix with other advisory locks.
        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtextextended({CarEntryLockPrefix + lotId}, 0))", cancellationToken);

        // Counted after the lock, so a car admitted by the previous holder is already seen.
        return await CountAvailableCarSlotsAsync(lotId, cancellationToken);
    }

    // Same query with or without the lock, so the gate check before Booking and the locked check agree (US-PARK-004).
    public Task<int> CountAvailableCarSlotsAsync(Guid lotId, CancellationToken cancellationToken) =>
        (from slot in dbContext.ParkingSlots
         join zone in dbContext.Zones on slot.ZoneId equals zone.Id
         where slot.LotId == lotId
               && slot.EffectiveStatus == SlotEffectiveStatus.Available
               && zone.VehicleGroup == VehicleType.Car
         select slot.Id)
        .CountAsync(cancellationToken);

    public async Task<IReadOnlyDictionary<Guid, string>> FindSlotCodesAsync(
        IReadOnlyCollection<Guid> sessionIds,
        CancellationToken cancellationToken)
    {
        if (sessionIds.Count == 0)
            return new Dictionary<Guid, string>();

        return await dbContext.ParkingSlots.AsNoTracking()
            .Where(slot => slot.AssignedSessionId != null && sessionIds.Contains(slot.AssignedSessionId.Value))
            .ToDictionaryAsync(slot => slot.AssignedSessionId!.Value, slot => slot.SlotCode, cancellationToken);
    }

    public async Task<string?> ReleaseSessionSlotAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        // Tracked on purpose (not a SQL UPDATE): the release is written by the same SaveChanges as the cancelled
        // session, so both succeed or neither does. row_version is a concurrency token, so a concurrent change to
        // the slot fails the save instead of being overwritten.
        var slot = await dbContext.ParkingSlots.FirstOrDefaultAsync(item => item.AssignedSessionId == sessionId, cancellationToken);
        if (slot is null)
            return null;

        slot.ReleaseAssignment();
        return slot.SlotCode;
    }

    private sealed record DesignatedSlotRow(Guid SlotId, Guid ZoneId, string SlotCode);
}
