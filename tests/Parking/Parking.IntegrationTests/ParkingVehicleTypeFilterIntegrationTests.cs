using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ParkingSystem.Parking.Application.ParkingLots;
using ParkingSystem.Parking.Domain.ParkingSessions;
using ParkingSystem.Parking.Persistence;
using ParkingSystem.Testing;
using Xunit;

namespace ParkingSystem.Tests.Parking.IntegrationTests;

public sealed class ParkingVehicleTypeFilterIntegrationTests(PostgreSqlContainerFixture postgres)
    : IClassFixture<PostgreSqlContainerFixture>
{
    [Fact]
    public async Task Filter_returns_only_approved_lots_with_matching_zone_and_no_duplicates()
    {
        var ct = TestContext.Current.CancellationToken;
        var services = new ServiceCollection();
        services.AddPersistence(PostgreSqlTestConfiguration.Create(postgres.ConnectionString));
        await using var serviceProvider = services.BuildServiceProvider(validateScopes: true);

        // Distinct IDs avoid collisions with the baseline sample data.
        var carLotId = Guid.Parse("91111111-0000-0000-0000-000000000001");
        var motorbikeLotId = Guid.Parse("91111111-0000-0000-0000-000000000002");
        var mixedLotId = Guid.Parse("91111111-0000-0000-0000-000000000003");
        var noZoneLotId = Guid.Parse("91111111-0000-0000-0000-000000000004");
        var pendingLotId = Guid.Parse("91111111-0000-0000-0000-000000000005");

        await using (var migrationScope = serviceProvider.CreateAsyncScope())
        {
            var db = migrationScope.ServiceProvider.GetRequiredService<ParkingDbContext>();
            await db.Database.MigrateAsync(ct);

            await db.Database.ExecuteSqlRawAsync(
                """
                INSERT INTO parking.parking_lots (id, name, address, latitude, longitude, type, approval_status)
                VALUES
                ('91111111-0000-0000-0000-000000000001', 'Car only', 'A', 10.77, 106.70, 'Outdoor', 'Approved'),
                ('91111111-0000-0000-0000-000000000002', 'Motorbike only', 'B', 10.77, 106.70, 'Outdoor', 'Approved'),
                ('91111111-0000-0000-0000-000000000003', 'Mixed', 'C', 10.77, 106.70, 'Outdoor', 'Approved'),
                ('91111111-0000-0000-0000-000000000004', 'No zone', 'D', 10.77, 106.70, 'Outdoor', 'Approved'),
                ('91111111-0000-0000-0000-000000000005', 'Pending car', 'E', 10.77, 106.70, 'Outdoor', 'Pending');

                INSERT INTO parking.floors (id, lot_id, name)
                VALUES
                ('92222222-0000-0000-0000-000000000001', '91111111-0000-0000-0000-000000000001', 'CAR-F1'),
                ('92222222-0000-0000-0000-000000000002', '91111111-0000-0000-0000-000000000002', 'MOTO-F1'),
                ('92222222-0000-0000-0000-000000000003', '91111111-0000-0000-0000-000000000003', 'MIX-F1'),
                ('92222222-0000-0000-0000-000000000005', '91111111-0000-0000-0000-000000000005', 'PEND-F1');

                INSERT INTO parking.zones
                    (id, floor_id, code, vehicle_group, capacity, current_vehicle_count, has_slot_sensor)
                VALUES
                ('93333333-0000-0000-0000-000000000001', '92222222-0000-0000-0000-000000000001', 'CAR-Z1', 'Car', 10, 0, true),
                ('93333333-0000-0000-0000-000000000002', '92222222-0000-0000-0000-000000000002', 'MOTO-Z1', 'Motorbike', 20, 0, false),
                ('93333333-0000-0000-0000-000000000003', '92222222-0000-0000-0000-000000000003', 'MIX-Z1', 'Car', 10, 0, true),
                ('93333333-0000-0000-0000-000000000004', '92222222-0000-0000-0000-000000000003', 'MIX-Z2', 'Car', 10, 0, true),
                ('93333333-0000-0000-0000-000000000005', '92222222-0000-0000-0000-000000000003', 'MIX-Z3', 'Motorbike', 10, 0, false),
                ('93333333-0000-0000-0000-000000000006', '92222222-0000-0000-0000-000000000005', 'PEND-Z1', 'Car', 10, 0, true);
                """, ct);
        }

        await using var scope = serviceProvider.CreateAsyncScope();
        var reader = scope.ServiceProvider.GetRequiredService<IParkingLotCatalogReader>();

        var carLots = await reader.ListApprovedAsync(ct, VehicleType.Car);
        var motoLots = await reader.ListApprovedAsync(ct, VehicleType.Motorbike);
        var allLots = await reader.ListApprovedAsync(ct);

        Assert.Contains(carLots, lot => lot.Id == carLotId);
        Assert.Contains(carLots, lot => lot.Id == mixedLotId);
        Assert.DoesNotContain(carLots, lot => lot.Id == motorbikeLotId || lot.Id == noZoneLotId || lot.Id == pendingLotId);
        Assert.Single(carLots, lot => lot.Id == mixedLotId); // two matching car zones still yield one lot

        Assert.Contains(motoLots, lot => lot.Id == motorbikeLotId);
        Assert.Contains(motoLots, lot => lot.Id == mixedLotId);
        Assert.DoesNotContain(motoLots, lot => lot.Id == carLotId || lot.Id == noZoneLotId || lot.Id == pendingLotId);

        Assert.Contains(allLots, lot => lot.Id == noZoneLotId);
        Assert.DoesNotContain(allLots, lot => lot.Id == pendingLotId);
    }
}
