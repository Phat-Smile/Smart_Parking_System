using Microsoft.EntityFrameworkCore;
using ParkingSystem.Parking.Application.ParkingLots.Availability;
using ParkingSystem.Parking.Domain.ParkingLayout;
using ParkingSystem.Parking.Domain.ParkingSessions;

namespace ParkingSystem.Parking.Persistence.ParkingLots;

// No new table/migration: derives historical 15-minute-slot proxy data from actual parking sessions.
// It cannot reconstruct historical sensor overrides or real physical occupancy; see API dataSource.
internal sealed class ArrivalSnapshotReader(ParkingDbContext db) : IArrivalSnapshotReader
{
    private static readonly TimeSpan VietnamOffset = TimeSpan.FromHours(7);

    public async Task<IReadOnlyList<ArrivalAvailabilitySnapshot>> GetSessionProxyAsync(
        Guid lotId, string vehicleType, DateTimeOffset arrivalAt, CancellationToken cancellationToken)
    {
        var type = vehicleType == "Car" ? VehicleType.Car : VehicleType.Motorbike;
        var localArrival = arrivalAt.ToOffset(VietnamOffset);
        var localNow = DateTimeOffset.UtcNow.ToOffset(VietnamOffset);
        var firstDay = localNow.Date.AddDays(-28);
        var firstUtc = new DateTimeOffset(firstDay, VietnamOffset).ToUniversalTime();
        var now = DateTimeOffset.UtcNow;

        int capacity;
        if (type == VehicleType.Car)
        {
            // Capacity is approximate: current declared slots, not historic per-slot state.
            capacity = await (from slot in db.ParkingSlots.AsNoTracking()
                join zone in db.Zones.AsNoTracking() on slot.ZoneId equals zone.Id
                where slot.LotId == lotId && zone.VehicleGroup == VehicleType.Car
                      && slot.OverrideState != OverrideState.Unavailable
                      && slot.OverrideState != OverrideState.Maintenance
                select slot.Id).CountAsync(cancellationToken);
        }
        else
        {
            capacity = await (from zone in db.Zones.AsNoTracking()
                join floor in db.Floors.AsNoTracking() on zone.FloorId equals floor.Id
                where floor.LotId == lotId && zone.VehicleGroup == VehicleType.Motorbike
                select zone.Capacity).SumAsync(cancellationToken);
        }
        if (capacity <= 0)
            return [];

        // Bounded by the last 28 days; include sessions entered earlier and still active in that period.
        var sessions = await db.ParkingSessions.AsNoTracking()
            .Where(s => s.LotId == lotId && s.VehicleType == type
                && s.Status != ParkingSessionStatus.Cancelled
                && s.EntryTime < now && (s.ExitTime == null || s.ExitTime >= firstUtc))
            .Select(s => new { s.EntryTime, s.ExitTime })
            .ToListAsync(cancellationToken);
        if (sessions.Count == 0)
            return []; // No recorded sessions: don't invent a full lot of empty history.

        // Keep only days with recorded arrivals. Other days have no observation coverage.
        var observedDays = sessions.Select(s => s.EntryTime.ToOffset(VietnamOffset).Date)
            .Where(day => day >= firstDay && day < localNow.Date)
            .Distinct()
            .OrderBy(day => day);
        var localQuarterMinute = (localArrival.Minute / 15) * 15;
        var snapshots = new List<ArrivalAvailabilitySnapshot>();
        foreach (var day in observedDays)
        {
            var captured = new DateTimeOffset(
                day.Year, day.Month, day.Day, localArrival.Hour, localQuarterMinute, 0, VietnamOffset);
            var at = captured.ToUniversalTime();
            var occupied = sessions.Count(s => s.EntryTime <= at &&
                (!s.ExitTime.HasValue || s.ExitTime > at));
            snapshots.Add(new ArrivalAvailabilitySnapshot(lotId, vehicleType, captured,
                Math.Max(0, capacity - occupied)));
        }
        return snapshots;
    }
}
