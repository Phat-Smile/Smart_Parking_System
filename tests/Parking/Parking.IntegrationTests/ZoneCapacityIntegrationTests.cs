using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ParkingSystem.Parking.Application.ParkingLayout.ZoneCapacity;
using ParkingSystem.Parking.Domain.ParkingLayout;
using ParkingSystem.Parking.Domain.ParkingLots;
using ParkingSystem.Parking.Domain.ParkingSessions;
using ParkingSystem.Parking.Persistence;
using ParkingSystem.Testing;
using Xunit;

namespace ParkingSystem.Tests.Parking.IntegrationTests;

// US-OWNER-003 against real PostgreSQL: owner check and the slot counts behind each zone.
public sealed class ZoneCapacityIntegrationTests(PostgreSqlContainerFixture postgres) : IClassFixture<PostgreSqlContainerFixture>
{
    [Fact]
    public async Task Reads_capacity_slots_and_vehicles_inside_of_every_zone_for_the_owner_only()
    {
        var token = TestContext.Current.CancellationToken;
        await using var provider = await NewProviderAsync(token);
        var lotId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var staffId = Guid.NewGuid();
        Guid carZoneId, bikeZoneId;

        await using (var scope = provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ParkingDbContext>();
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"""
                INSERT INTO parking.parking_lots (id, name, address, latitude, longitude, type, approval_status)
                VALUES ({lotId}, 'Zone Capacity Test', '1 Demo Street', 10.77, 106.70, 'MultiStorey', 'Approved')
                """, token);
            db.ParkingLotAssignments.Add(ParkingLotAssignment.Create(lotId, ownerId, ParkingLotMemberRole.Owner, Guid.NewGuid(), DateTimeOffset.UtcNow));
            db.ParkingLotAssignments.Add(ParkingLotAssignment.Create(lotId, staffId, ParkingLotMemberRole.Staff, Guid.NewGuid(), DateTimeOffset.UtcNow));

            var floor = Floor.Create(Guid.NewGuid(), lotId, "Tầng 1");
            var cars = Zone.Create(Guid.NewGuid(), floor.Id, "A1", VehicleType.Car, 20, hasSlotSensor: true);
            var bikes = Zone.Create(Guid.NewGuid(), floor.Id, "M1", VehicleType.Motorbike, 100, hasSlotSensor: false);
            carZoneId = cars.Id;
            bikeZoneId = bikes.Id;
            db.Floors.Add(floor);
            db.Zones.AddRange(cars, bikes);
            db.ParkingSlots.AddRange(
                ParkingSlot.Create(Guid.NewGuid(), lotId, cars.Id, "A1-01"),
                ParkingSlot.Create(Guid.NewGuid(), lotId, cars.Id, "A1-02"),
                ParkingSlot.Create(Guid.NewGuid(), lotId, cars.Id, "A1-03", OverrideState.Unavailable));
            await db.SaveChangesAsync(token);
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE parking.zones SET current_vehicle_count = 7 WHERE id = {bikes.Id}", token);
        }

        await using var readScope = provider.CreateAsyncScope();
        var reader = readScope.ServiceProvider.GetRequiredService<IZoneCapacityReader>();
        var floors = await reader.ReadAsync(lotId, token);
        var car = floors[0].Zones.Single(zone => zone.ZoneId == carZoneId);
        var bike = floors[0].Zones.Single(zone => zone.ZoneId == bikeZoneId);

        Assert.True(await reader.IsOwnerAsync(lotId, ownerId, token));
        Assert.False(await reader.IsOwnerAsync(lotId, staffId, token));
        Assert.False(await reader.IsOwnerAsync(lotId, Guid.NewGuid(), token));
        Assert.Equal((20, 3, 2, 0), (car.Capacity, car.SlotCount!.Value, car.ValidCapacity!.Value, car.VehiclesInside));
        Assert.Equal((100, (int?)null, 7), (bike.Capacity, bike.SlotCount, bike.VehiclesInside));
    }

    private async Task<ServiceProvider> NewProviderAsync(CancellationToken token)
    {
        var services = new ServiceCollection();
        services.AddPersistence(PostgreSqlTestConfiguration.Create(postgres.ConnectionString));
        var provider = services.BuildServiceProvider(validateScopes: true);
        await using var scope = provider.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<ParkingDbContext>().Database.MigrateAsync(token);
        return provider;
    }
}
