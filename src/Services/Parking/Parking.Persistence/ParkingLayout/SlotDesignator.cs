using Microsoft.EntityFrameworkCore;
using ParkingSystem.Parking.Application.ParkingLayout;

namespace ParkingSystem.Parking.Persistence.ParkingLayout;

internal sealed class SlotDesignator(ParkingDbContext dbContext) : ISlotDesignator
{
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

    private sealed record DesignatedSlotRow(Guid SlotId, Guid ZoneId, string SlotCode);
}
