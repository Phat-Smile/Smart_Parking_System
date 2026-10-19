using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ParkingSystem.Parking.Application.ParkingLots;
using ParkingSystem.Parking.Persistence;
using ParkingSystem.Testing;
using Xunit;

namespace ParkingSystem.Tests.Parking.IntegrationTests;

public sealed class NearbyParkingLotIntegrationTests(PostgreSqlContainerFixture postgres)
    : IClassFixture<PostgreSqlContainerFixture>
{
    [Fact]
    public async Task Search_returns_only_nearby_approved_lots_ordered_by_distance()
    {
        var ct = TestContext.Current.CancellationToken;
        var services = new ServiceCollection();
        services.AddPersistence(PostgreSqlTestConfiguration.Create(postgres.ConnectionString));
        await using var provider = services.BuildServiceProvider(validateScopes: true);
        await using (var scope = provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ParkingDbContext>();
            await db.Database.MigrateAsync(ct);
            await db.Database.ExecuteSqlRawAsync(
                """
                INSERT INTO parking.parking_lots
                    (id, name, address, latitude, longitude, type, approval_status)
                VALUES
                    ('10000000-0000-0000-0000-000000000001', 'Near', 'A', 10.776530, 106.700981, 'Outdoor', 'Approved'),
                    ('10000000-0000-0000-0000-000000000002', 'Further', 'B', 10.786530, 106.700981, 'Outdoor', 'Approved'),
                    ('10000000-0000-0000-0000-000000000003', 'Pending', 'C', 10.776530, 106.700981, 'Outdoor', 'Pending'),
                    ('10000000-0000-0000-0000-000000000004', 'Far Away', 'D', 11.000000, 107.000000, 'Outdoor', 'Approved');
                """, ct);
        }

        await using var queryScope = provider.CreateAsyncScope();
        var reader = queryScope.ServiceProvider.GetRequiredService<INearbyParkingLotReader>();
        var results = await reader.SearchAsync(10.776530m, 106.700981m, 3, 10, ct);

        Assert.Collection(results,
            near => { Assert.Equal("Near", near.Name); Assert.Equal(0, near.DistanceKm); },
            further => { Assert.Equal("Further", further.Name); Assert.True(further.DistanceKm > 0); });
    }
}
