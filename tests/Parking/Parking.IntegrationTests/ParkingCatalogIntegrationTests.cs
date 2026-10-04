using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ParkingSystem.Parking.Application.ParkingLots;
using ParkingSystem.Parking.Persistence;
using ParkingSystem.Testing;
using Xunit;

namespace ParkingSystem.Tests.Parking.IntegrationTests;

public sealed class ParkingCatalogIntegrationTests(PostgreSqlContainerFixture postgres) : IClassFixture<PostgreSqlContainerFixture>
{
    [Fact]
    public async Task Catalog_returns_only_approved_parking_lots_in_a_deterministic_order()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var services = new ServiceCollection();
        services.AddPersistence(PostgreSqlTestConfiguration.Create(postgres.ConnectionString));
        await using var serviceProvider = services.BuildServiceProvider(validateScopes: true);

        await using (var migrationScope = serviceProvider.CreateAsyncScope())
        {
            var dbContext = migrationScope.ServiceProvider.GetRequiredService<ParkingDbContext>();
            await dbContext.Database.MigrateAsync(cancellationToken);
            await dbContext.Database.ExecuteSqlRawAsync(
                """
                INSERT INTO parking.parking_lots
                    (id, name, address, latitude, longitude, type, approval_status)
                VALUES
                    ('11111111-1111-1111-1111-111111111111', 'Zulu Parking', '3 Demo Street', 10.790000, 106.710000, 'Outdoor', 'Approved'),
                    ('22222222-2222-2222-2222-222222222222', 'Alpha Parking', '1 Demo Street', 10.770000, 106.690000, 'MultiStorey', 'Approved'),
                    ('33333333-3333-3333-3333-333333333333', 'Pending Parking', '2 Demo Street', 10.780000, 106.700000, 'Underground', 'Pending'),
                    ('44444444-4444-4444-4444-444444444444', 'Rejected Parking', '4 Demo Street', 10.800000, 106.720000, 'Outdoor', 'Rejected');
                """,
                cancellationToken);
        }

        await using var scope = serviceProvider.CreateAsyncScope();
        var reader = scope.ServiceProvider.GetRequiredService<IParkingLotCatalogReader>();

        var result = await reader.ListApprovedAsync(cancellationToken);

        Assert.Collection(
            result,
            parkingLot =>
            {
                Assert.Equal("Alpha Parking", parkingLot.Name);
                Assert.Equal("MultiStorey", parkingLot.Type);
            },
            parkingLot =>
            {
                Assert.Equal("Zulu Parking", parkingLot.Name);
                Assert.Equal("Outdoor", parkingLot.Type);
            });
        Assert.DoesNotContain(result, parkingLot => parkingLot.Name is "Pending Parking" or "Rejected Parking");
    }
}
