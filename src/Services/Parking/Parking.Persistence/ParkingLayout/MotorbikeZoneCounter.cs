using Microsoft.EntityFrameworkCore;
using ParkingSystem.Parking.Application.ParkingLayout;
using ParkingSystem.Parking.Domain.ParkingSessions;

namespace ParkingSystem.Parking.Persistence.ParkingLayout;

internal sealed class MotorbikeZoneCounter(ParkingDbContext dbContext) : IMotorbikeZoneCounter
{
    public async Task<MotorbikeSpaceTaken?> TakeSpaceAsync(Guid lotId, CancellationToken cancellationToken)
    {
        // BR-07.b: the capacity check and the +1 are one statement, so the row lock makes concurrent entries queue
        // and only the ones that still fit succeed. LIMIT 1 keeps the update on a single zone if a lot ever has two.
        var taken = await dbContext.Database.SqlQuery<TakenRow>($"""
            UPDATE parking.zones AS zone
            SET current_vehicle_count = zone.current_vehicle_count + 1
            WHERE zone.id = (
                SELECT candidate.id
                FROM parking.zones AS candidate
                JOIN parking.floors AS floor ON floor.id = candidate.floor_id
                WHERE floor.lot_id = {lotId}
                  AND candidate.vehicle_group = 'Motorbike'
                ORDER BY candidate.code
                LIMIT 1)
              AND zone.current_vehicle_count < zone.capacity
            RETURNING zone.id AS "ZoneId", zone.capacity - zone.current_vehicle_count AS "AvailableSpaces"
            """).ToListAsync(cancellationToken);

        return taken.Count == 0 ? null : new MotorbikeSpaceTaken(taken[0].ZoneId, taken[0].AvailableSpaces);
    }

    public Task ReleaseSpaceAsync(Guid zoneId, CancellationToken cancellationToken) =>
        // BR-07.b: the count is entries minus exits and never below 0.
        dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE parking.zones
            SET current_vehicle_count = current_vehicle_count - 1
            WHERE id = {zoneId} AND vehicle_group = 'Motorbike' AND current_vehicle_count > 0
            """, cancellationToken);

    public Task<MotorbikeAvailability?> GetAvailabilityAsync(Guid lotId, CancellationToken cancellationToken) =>
        dbContext.Zones.AsNoTracking()
            .Join(dbContext.Floors, candidate => candidate.FloorId, floor => floor.Id, (candidate, floor) => new { candidate, floor })
            .Where(row => row.floor.LotId == lotId && row.candidate.VehicleGroup == VehicleType.Motorbike)
            .OrderBy(row => row.candidate.Code)
            .Select(row => new MotorbikeAvailability(row.candidate.Capacity, row.candidate.CurrentVehicleCount))
            .FirstOrDefaultAsync(cancellationToken);

    private sealed record TakenRow(Guid ZoneId, int AvailableSpaces);
}
