using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ParkingSystem.Parking.Application.ParkingSessions;
using ParkingSystem.Parking.Domain.ParkingSessions;
using ParkingSystem.Parking.Persistence;
using ParkingSystem.Testing;
using Xunit;

namespace ParkingSystem.Tests.Parking.IntegrationTests;

public sealed class ParkingSessionIntegrationTests(PostgreSqlContainerFixture postgres) : IClassFixture<PostgreSqlContainerFixture>
{
    private const string Plate = "51A-123.45";

    [Fact]
    public async Task Unique_index_blocks_a_second_open_session_for_the_same_plate_in_the_same_lot()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var serviceProvider = await CreateMigratedServiceProviderAsync(cancellationToken);
        var lotId = Guid.NewGuid();

        Assert.True(await SaveNewSessionAsync(serviceProvider, lotId, cancellationToken));
        Assert.False(await SaveNewSessionAsync(serviceProvider, lotId, cancellationToken));

        // The same plate may still enter another lot.
        Assert.True(await SaveNewSessionAsync(serviceProvider, Guid.NewGuid(), cancellationToken));

        await using var scope = serviceProvider.CreateAsyncScope();
        var sessions = scope.ServiceProvider.GetRequiredService<IParkingSessionRepository>();
        Assert.True(await sessions.HasOpenSessionAsync(lotId, Plate, cancellationToken));
    }

    [Fact]
    public async Task Plate_can_open_a_new_session_after_the_previous_one_is_completed()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var serviceProvider = await CreateMigratedServiceProviderAsync(cancellationToken);
        var lotId = Guid.NewGuid();
        Assert.True(await SaveNewSessionAsync(serviceProvider, lotId, cancellationToken));

        // Completing a session belongs to the exit story; it is simulated here with SQL.
        await using (var scope = serviceProvider.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ParkingDbContext>();
            await dbContext.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE parking.parking_sessions SET status = {nameof(ParkingSessionStatus.Completed)} WHERE lot_id = {lotId}",
                cancellationToken);
        }

        await using (var scope = serviceProvider.CreateAsyncScope())
        {
            var sessions = scope.ServiceProvider.GetRequiredService<IParkingSessionRepository>();
            Assert.False(await sessions.HasOpenSessionAsync(lotId, Plate, cancellationToken));
        }

        Assert.True(await SaveNewSessionAsync(serviceProvider, lotId, cancellationToken));
    }

    private async Task<ServiceProvider> CreateMigratedServiceProviderAsync(CancellationToken cancellationToken)
    {
        var services = new ServiceCollection();
        services.AddPersistence(PostgreSqlTestConfiguration.Create(postgres.ConnectionString));
        var serviceProvider = services.BuildServiceProvider(validateScopes: true);

        await using var scope = serviceProvider.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<ParkingDbContext>().Database.MigrateAsync(cancellationToken);
        return serviceProvider;
    }

    private static async Task<bool> SaveNewSessionAsync(ServiceProvider serviceProvider, Guid lotId, CancellationToken cancellationToken)
    {
        await using var scope = serviceProvider.CreateAsyncScope();
        var sessions = scope.ServiceProvider.GetRequiredService<IParkingSessionRepository>();
        sessions.Add(ParkingSession.Open(
            Guid.NewGuid(),
            lotId,
            Plate,
            VehicleType.Car,
            DateTimeOffset.UtcNow,
            Guid.NewGuid().ToString("N"),
            userId: null,
            vehicleId: null));
        return await sessions.TrySaveChangesAsync(cancellationToken);
    }
}
