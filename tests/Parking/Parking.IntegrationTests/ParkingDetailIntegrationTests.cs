using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ParkingSystem.Parking.Application.ParkingLots;
using ParkingSystem.Parking.Persistence;
using ParkingSystem.Testing;
using Xunit;

namespace ParkingSystem.Tests.Parking.IntegrationTests;

public sealed class ParkingDetailIntegrationTests(PostgreSqlContainerFixture postgres) : IClassFixture<PostgreSqlContainerFixture>
{
    [Fact]
    public async Task Detail_exposes_only_an_approved_parking_lot()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var services = new ServiceCollection();
        services.AddPersistence(PostgreSqlTestConfiguration.Create(postgres.ConnectionString));
        await using var serviceProvider = services.BuildServiceProvider(validateScopes: true);

        var approvedParkingLotId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var pendingParkingLotId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var rejectedParkingLotId = Guid.Parse("33333333-3333-3333-3333-333333333333");

        await using (var migrationScope = serviceProvider.CreateAsyncScope())
        {
            var dbContext = migrationScope.ServiceProvider.GetRequiredService<ParkingDbContext>();
            await dbContext.Database.MigrateAsync(cancellationToken);
            await dbContext.Database.ExecuteSqlRawAsync(
                """
                INSERT INTO parking.parking_lots
                    (id, name, address, latitude, longitude, type, approval_status)
                VALUES
                    ('11111111-1111-1111-1111-111111111111', 'Approved Parking', '1 Demo Street', 10.776530, 106.700981, 'MultiStorey', 'Approved'),
                    ('22222222-2222-2222-2222-222222222222', 'Pending Parking', '2 Demo Street', 10.780000, 106.705000, 'Underground', 'Pending'),
                    ('33333333-3333-3333-3333-333333333333', 'Rejected Parking', '3 Demo Street', 10.790000, 106.710000, 'Outdoor', 'Rejected');
                """,
                cancellationToken);
        }

        await using var scope = serviceProvider.CreateAsyncScope();
        var reader = scope.ServiceProvider.GetRequiredService<IParkingLotDetailReader>();

        var approved = await reader.GetApprovedByIdAsync(approvedParkingLotId, cancellationToken);
        var pending = await reader.GetApprovedByIdAsync(pendingParkingLotId, cancellationToken);
        var rejected = await reader.GetApprovedByIdAsync(rejectedParkingLotId, cancellationToken);
        var missing = await reader.GetApprovedByIdAsync(Guid.Parse("44444444-4444-4444-4444-444444444444"), cancellationToken);

        Assert.NotNull(approved);
        Assert.Equal(approvedParkingLotId, approved.Id);
        Assert.Equal("Approved Parking", approved.Name);
        Assert.Equal("1 Demo Street", approved.Address);
        Assert.Equal(10.776530m, approved.Latitude);
        Assert.Equal(106.700981m, approved.Longitude);
        Assert.Equal("MultiStorey", approved.Type);
        Assert.Null(pending);
        Assert.Null(rejected);
        Assert.Null(missing);
    }
}
