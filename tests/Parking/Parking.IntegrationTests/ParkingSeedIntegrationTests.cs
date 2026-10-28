using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ParkingSystem.Parking.Application.ParkingLots;
using ParkingSystem.Parking.Persistence;
using ParkingSystem.Testing;
using Xunit;

namespace ParkingSystem.Tests.Parking.IntegrationTests;

/// <summary>
/// TECH-02: execute the real shared SQL twice, then verify the seeded data
/// through the same reader used by public catalog endpoints.
/// Runs only against the disposable PostgreSQL Testcontainer fixture.
/// </summary>
public sealed class ParkingSeedIntegrationTests(PostgreSqlContainerFixture postgres)
    : IClassFixture<PostgreSqlContainerFixture>
{
    [Fact]
    public async Task Shared_catalog_seed_is_repeatable_and_does_not_expose_pending_lots()
    {
        var ct = TestContext.Current.CancellationToken;
        var services = new ServiceCollection();
        services.AddPersistence(PostgreSqlTestConfiguration.Create(postgres.ConnectionString));
        await using var provider = services.BuildServiceProvider(validateScopes: true);
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ParkingDbContext>();
        await db.Database.MigrateAsync(ct);

        var seedSql = await File.ReadAllTextAsync(
            Path.Combine(AppContext.BaseDirectory, "Seed-ParkingCatalog.sql"), ct);
        var validationSql = await File.ReadAllTextAsync(
            Path.Combine(AppContext.BaseDirectory, "Validate-ParkingCatalogSeed.sql"), ct);

        var layoutSql = await File.ReadAllTextAsync(
            Path.Combine(AppContext.BaseDirectory, "Seed-ParkingLayout.sql"), ct);
        var layoutValidation = await File.ReadAllTextAsync(
            Path.Combine(AppContext.BaseDirectory, "Validate-ParkingLayoutSeed.sql"), ct);

        await using (var transaction = await db.Database.BeginTransactionAsync(ct))
        {
            await db.Database.ExecuteSqlRawAsync(seedSql, ct);
            await db.Database.ExecuteSqlRawAsync(layoutSql, ct);
            await db.Database.ExecuteSqlRawAsync(seedSql, ct);
            await db.Database.ExecuteSqlRawAsync(layoutSql, ct);
            await db.Database.ExecuteSqlRawAsync(validationSql, ct);
            await db.Database.ExecuteSqlRawAsync(layoutValidation, ct);
            await transaction.CommitAsync(ct);
        }

        var ids = new[]
        {
            Guid.Parse("10000000-0000-0000-0000-000000000001"),
            Guid.Parse("10000000-0000-0000-0000-000000000002"),
            Guid.Parse("10000000-0000-0000-0000-000000000003"),
            Guid.Parse("10000000-0000-0000-0000-000000000004")
        };
        Assert.Equal(4, await db.ParkingLots.AsNoTracking().CountAsync(x => ids.Contains(x.Id), ct));

        var publicReader = scope.ServiceProvider.GetRequiredService<IParkingLotCatalogReader>();
        var catalog = await publicReader.ListApprovedAsync(ct);
        Assert.Equal(3, catalog.Count);
        Assert.DoesNotContain(catalog, x => x.Id == ids[3]);

        var detailReader = scope.ServiceProvider.GetRequiredService<IParkingLotDetailReader>();
        Assert.Null(await detailReader.GetApprovedByIdAsync(ids[3], ct));
        Assert.NotNull(await detailReader.GetApprovedByIdAsync(ids[0], ct));
    }
}
