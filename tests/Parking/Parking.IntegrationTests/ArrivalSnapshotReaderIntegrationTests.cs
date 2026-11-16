using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ParkingSystem.Parking.Application;
using ParkingSystem.Parking.Application.ParkingLots.Availability;
using ParkingSystem.Parking.Domain.Entities;
using ParkingSystem.Parking.Domain.ParkingLayout;
using ParkingSystem.Parking.Domain.ParkingSessions;
using ParkingSystem.Parking.Persistence;
using ParkingSystem.Testing;
using Xunit;

namespace ParkingSystem.Tests.Parking.IntegrationTests;

public sealed class ArrivalSnapshotReaderIntegrationTests(PostgreSqlContainerFixture postgres)
    : IClassFixture<PostgreSqlContainerFixture>
{
    private static readonly TimeSpan VietnamOffset = TimeSpan.FromHours(7);

    [Fact]
    public async Task GetSessionProxyAsync_returns_three_windows_and_excludes_future_snapshots()
    {
        var ct = TestContext.Current.CancellationToken;
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddPersistence(PostgreSqlTestConfiguration.Create(postgres.ConnectionString));
        services.AddApplicationServices();
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ParkingDbContext>();
        await db.Database.MigrateAsync(ct);

        var lotId = Guid.NewGuid();
        var floorId = Guid.NewGuid();
        var zoneId = Guid.NewGuid();
        var slot1 = Guid.NewGuid();
        var slot2 = Guid.NewGuid();

        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO parking.parking_lots (id, name, address, latitude, longitude, type, approval_status,
                supports_electric_vehicles, supports_large_vehicles, has_ev_charging, is_covered, has_cctv)
            VALUES ({lotId}, 'Snapshot Test Lot', 'Demo Address', 10.77, 106.70, 'Outdoor', 'Approved',
                true, true, true, true, true)
            """, ct);

        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO parking.floors (id, lot_id, name) VALUES ({floorId}, {lotId}, 'F1')
            """, ct);

        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO parking.zones (id, floor_id, code, vehicle_group, capacity, current_vehicle_count, has_slot_sensor)
            VALUES ({zoneId}, {floorId}, 'C1', 'Car', 2, 0, false)
            """, ct);

        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO parking.parking_slots (id, lot_id, zone_id, slot_code, effective_status, row_version)
            VALUES ({slot1}, {lotId}, {zoneId}, 'S1', 'Available', 1),
                   ({slot2}, {lotId}, {zoneId}, 'S2', 'Available', 1)
            """, ct);

        var localNow = DateTimeOffset.UtcNow.ToOffset(VietnamOffset);
        var yesterday = localNow.Date.AddDays(-1);

        // Historical session on yesterday
        var entryYesterday = new DateTimeOffset(yesterday.Year, yesterday.Month, yesterday.Day, 10, 0, 0, VietnamOffset).ToUniversalTime();
        var exitYesterday = entryYesterday.AddHours(2);
        var s1Id = Guid.NewGuid();

        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO parking.parking_sessions (id, lot_id, plate_number, vehicle_type, status, entry_time, qr_token, exit_time)
            VALUES ({s1Id}, {lotId}, '51A-12345', 'Car', 'Completed', {entryYesterday}, 'QR123456', {exitYesterday})
            """, ct);

        var reader = scope.ServiceProvider.GetRequiredService<IArrivalSnapshotReader>();
        var targetArrival = new DateTimeOffset(localNow.Date.AddDays(1), VietnamOffset).AddHours(10).AddMinutes(30);

        var snapshots = await reader.GetSessionProxyAsync(lotId, "Car", targetArrival, ct);

        // Should return 3 snapshots for yesterday: 10:15 (-15m), 10:30 (center), 10:45 (+15m)
        Assert.Equal(3, snapshots.Count);
        Assert.All(snapshots, s => Assert.Equal(lotId, s.LotId));
        Assert.All(snapshots, s => Assert.True(s.CapturedAt.ToUniversalTime() <= DateTimeOffset.UtcNow));
    }

    [Fact]
    public async Task GetSessionProxyAsync_excludes_plus_15_minutes_snapshot_if_in_the_future()
    {
        var ct = TestContext.Current.CancellationToken;
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddPersistence(PostgreSqlTestConfiguration.Create(postgres.ConnectionString));
        services.AddApplicationServices();
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ParkingDbContext>();
        await db.Database.MigrateAsync(ct);

        var lotId = Guid.NewGuid();
        var floorId = Guid.NewGuid();
        var zoneId = Guid.NewGuid();
        var slot1 = Guid.NewGuid();

        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO parking.parking_lots (id, name, address, latitude, longitude, type, approval_status,
                supports_electric_vehicles, supports_large_vehicles, has_ev_charging, is_covered, has_cctv)
            VALUES ({lotId}, 'Future Snapshot Guard Lot', 'Demo Address', 10.77, 106.70, 'Outdoor', 'Approved',
                true, true, true, true, true)
            """, ct);

        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO parking.floors (id, lot_id, name) VALUES ({floorId}, {lotId}, 'F1')
            """, ct);

        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO parking.zones (id, floor_id, code, vehicle_group, capacity, current_vehicle_count, has_slot_sensor)
            VALUES ({zoneId}, {floorId}, 'C1', 'Car', 1, 0, false)
            """, ct);

        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO parking.parking_slots (id, lot_id, zone_id, slot_code, effective_status, row_version)
            VALUES ({slot1}, {lotId}, {zoneId}, 'S1', 'Available', 1)
            """, ct);

        var now = DateTimeOffset.UtcNow;
        var localNow = now.ToOffset(VietnamOffset);
        var yesterday = localNow.Date.AddDays(-1);

        // Historical session on yesterday
        var entryYesterday = new DateTimeOffset(yesterday.Year, yesterday.Month, yesterday.Day, 23, 45, 0, VietnamOffset).ToUniversalTime();
        var s2Id = Guid.NewGuid();
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO parking.parking_sessions (id, lot_id, plate_number, vehicle_type, status, entry_time, qr_token)
            VALUES ({s2Id}, {lotId}, '51A-99999', 'Car', 'Active', {entryYesterday}, 'QR999999')
            """, ct);

        var reader = scope.ServiceProvider.GetRequiredService<IArrivalSnapshotReader>();

        // Target arrival at 23:45. +15m would cross to 00:00.
        // We assert all returned snapshots are strictly <= now.
        var targetArrival = new DateTimeOffset(yesterday.Year, yesterday.Month, yesterday.Day, 23, 45, 0, VietnamOffset);
        var snapshots = await reader.GetSessionProxyAsync(lotId, "Car", targetArrival, ct);

        Assert.NotEmpty(snapshots);
        foreach (var s in snapshots)
        {
            Assert.True(s.CapturedAt.ToUniversalTime() <= now,
                $"Snapshot timestamp {s.CapturedAt} was in the future relative to call time {now}.");
        }
    }
}
