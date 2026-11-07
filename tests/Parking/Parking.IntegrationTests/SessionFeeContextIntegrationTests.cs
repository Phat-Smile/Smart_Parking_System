using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ParkingSystem.BuildingBlocks.Time;
using ParkingSystem.Parking.Application;
using ParkingSystem.Parking.Application.ParkingSessions.GetSessionFeeContext;
using ParkingSystem.Parking.Domain.Entities;
using ParkingSystem.Parking.Domain.ParkingSessions;
using ParkingSystem.Parking.Persistence;
using ParkingSystem.Testing;
using Xunit;

namespace ParkingSystem.Tests.Parking.IntegrationTests;

// Contract 6 on real PostgreSQL: a price change after entry does not touch the session (BR-09.a).
public sealed class SessionFeeContextIntegrationTests(PostgreSqlContainerFixture postgres) : IClassFixture<PostgreSqlContainerFixture>
{
    private static readonly DateTime EntryUtc = new(2026, 11, 6, 8, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task The_price_in_force_at_entry_is_returned_not_a_later_one()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var serviceProvider = await CreateMigratedServiceProviderAsync(cancellationToken);
        var lotId = await SeedLotAsync(serviceProvider, cancellationToken);
        var sessionId = await SeedSessionAsync(serviceProvider, lotId, cancellationToken);
        await SeedPriceAsync(serviceProvider, lotId, version: 1, price: 10000m, EntryUtc.AddDays(-2), cancellationToken);
        await SeedPriceAsync(serviceProvider, lotId, version: 2, price: 12000m, EntryUtc.AddDays(-1), cancellationToken);
        await SeedPriceAsync(serviceProvider, lotId, version: 3, price: 20000m, EntryUtc.AddHours(1), cancellationToken);

        var result = await QueryAsync(serviceProvider, sessionId, cancellationToken);

        Assert.Equal(12000, result.Value!.Pricing.PricePerUnit);
        Assert.Equal(30, result.Value.Pricing.BlockMinutes);
    }

    [Fact]
    public async Task A_price_list_that_started_after_entry_is_not_used()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var serviceProvider = await CreateMigratedServiceProviderAsync(cancellationToken);
        var lotId = await SeedLotAsync(serviceProvider, cancellationToken);
        var sessionId = await SeedSessionAsync(serviceProvider, lotId, cancellationToken);
        await SeedPriceAsync(serviceProvider, lotId, version: 1, price: 20000m, EntryUtc.AddHours(1), cancellationToken);

        var result = await QueryAsync(serviceProvider, sessionId, cancellationToken);

        Assert.Equal(GetSessionFeeContextErrors.PricingNotFound, result.Error);
    }

    private static async Task<ParkingSystem.BuildingBlocks.Results.Result<SessionFeeContext>> QueryAsync(
        ServiceProvider serviceProvider, Guid sessionId, CancellationToken cancellationToken)
    {
        await using var scope = serviceProvider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ISender>().Send(new GetSessionFeeContextQuery(sessionId), cancellationToken);
    }

    private static async Task<Guid> SeedLotAsync(ServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        var lotId = Guid.NewGuid();
        await using var scope = serviceProvider.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<ParkingDbContext>().Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO parking.parking_lots (id, name, address, latitude, longitude, type, approval_status)
            VALUES ({lotId}, 'Fee Context Test Parking', '1 Demo Street', 10.776530, 106.700981, 'Outdoor', 'Approved')
            """,
            cancellationToken);
        return lotId;
    }

    private static async Task<Guid> SeedSessionAsync(ServiceProvider serviceProvider, Guid lotId, CancellationToken cancellationToken)
    {
        var session = ParkingSession.Open(
            Guid.NewGuid(), lotId, "51A-123.45", VehicleType.Car, new DateTimeOffset(EntryUtc), Guid.NewGuid().ToString("N"), null, null);
        await using var scope = serviceProvider.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ParkingDbContext>();
        dbContext.ParkingSessions.Add(session);
        await dbContext.SaveChangesAsync(cancellationToken);
        return session.Id;
    }

    private static async Task SeedPriceAsync(
        ServiceProvider serviceProvider, Guid lotId, int version, decimal price, DateTime effectiveFromUtc, CancellationToken cancellationToken)
    {
        await using var scope = serviceProvider.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ParkingDbContext>();
        dbContext.PricingPolicies.Add(PricingPolicy.Create(Guid.NewGuid(), lotId, "Car", version, "Block", 30, price, 0m, effectiveFromUtc));
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<ServiceProvider> CreateMigratedServiceProviderAsync(CancellationToken cancellationToken)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddPersistence(PostgreSqlTestConfiguration.Create(postgres.ConnectionString));
        services.AddApplicationServices();
        services.AddSingleton<IClock, SystemClock>();
        var serviceProvider = services.BuildServiceProvider(validateScopes: true);

        await using var scope = serviceProvider.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<ParkingDbContext>().Database.MigrateAsync(cancellationToken);
        return serviceProvider;
    }
}
