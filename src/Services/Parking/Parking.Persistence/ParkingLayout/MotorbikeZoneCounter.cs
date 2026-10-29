using Microsoft.EntityFrameworkCore;
using ParkingSystem.Parking.Application.ParkingLayout;
using ParkingSystem.Parking.Domain.ParkingSessions;

namespace ParkingSystem.Parking.Persistence.ParkingLayout;

internal sealed class MotorbikeZoneCounter(ParkingDbContext dbContext) : IMotorbikeZoneCounter
{
    // Each lost race means another entry took a space, so the number of retries a lot can need is bounded by its zones.
    private const int MaxAttempts = 10;

    public async Task<MotorbikeSpaceTaken?> TakeSpaceAsync(Guid lotId, CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < MaxAttempts; attempt++)
        {
            // BR-07.b: pick the zone with the most free spaces and +1 in one statement. The final
            // "current_vehicle_count < capacity" is re-checked after the row lock, so a zone never goes over capacity.
            var taken = await dbContext.Database.SqlQuery<TakenRow>($"""
                UPDATE parking.zones AS zone
                SET current_vehicle_count = zone.current_vehicle_count + 1
                FROM parking.floors AS floor
                WHERE floor.id = zone.floor_id
                  AND zone.id = (
                    SELECT candidate.id
                    FROM parking.zones AS candidate
                    JOIN parking.floors AS candidate_floor ON candidate_floor.id = candidate.floor_id
                    WHERE candidate_floor.lot_id = {lotId}
                      AND candidate.vehicle_group = 'Motorbike'
                      AND candidate.current_vehicle_count < candidate.capacity
                    ORDER BY candidate.capacity - candidate.current_vehicle_count DESC, candidate.code
                    LIMIT 1)
                  AND zone.current_vehicle_count < zone.capacity
                RETURNING zone.id AS "ZoneId", zone.code AS "ZoneCode", floor.name AS "FloorName"
                """).ToListAsync(cancellationToken);

            if (taken.Count == 1)
            {
                var availability = await GetAvailabilityAsync(lotId, cancellationToken);
                return new MotorbikeSpaceTaken(taken[0].ZoneId, taken[0].ZoneCode, taken[0].FloorName, availability?.Available ?? 0);
            }

            // Nothing updated: either every zone is full, or a concurrent entry filled the chosen zone first.
            if ((await GetAvailabilityAsync(lotId, cancellationToken))?.Available is not > 0)
                return null;
        }

        return null;
    }

    public Task ReleaseSpaceAsync(Guid zoneId, CancellationToken cancellationToken) =>
        // BR-07.b: the count is entries minus exits and never below 0.
        dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE parking.zones
            SET current_vehicle_count = current_vehicle_count - 1
            WHERE id = {zoneId} AND vehicle_group = 'Motorbike' AND current_vehicle_count > 0
            """, cancellationToken);

    public async Task<MotorbikeAvailability?> GetAvailabilityAsync(Guid lotId, CancellationToken cancellationToken)
    {
        var zones = await dbContext.Zones.AsNoTracking()
            .Join(dbContext.Floors, zone => zone.FloorId, floor => floor.Id, (zone, floor) => new { zone, floor })
            .Where(row => row.floor.LotId == lotId && row.zone.VehicleGroup == VehicleType.Motorbike)
            .OrderBy(row => row.floor.Name).ThenBy(row => row.zone.Code)
            .Select(row => new MotorbikeZoneAvailability(row.zone.Id, row.zone.Code, row.floor.Name, row.zone.Capacity, row.zone.CurrentVehicleCount))
            .ToListAsync(cancellationToken);

        return zones.Count == 0 ? null : new MotorbikeAvailability(zones);
    }

    private sealed record TakenRow(Guid ZoneId, string ZoneCode, string FloorName);
}
