using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ParkingSystem.BuildingBlocks.Time;
using ParkingSystem.Identity.Application.Accounts;
using ParkingSystem.Identity.Application.Accounts.OwnerApplications;
using ParkingSystem.Identity.Application.Profiles;
using ParkingSystem.Identity.Domain.Accounts;
using ParkingSystem.Identity.Infrastructure;
using ParkingSystem.Identity.Persistence;
using ParkingSystem.Testing;
using Xunit;

namespace ParkingSystem.Tests.Identity.IntegrationTests;

// Review of commit ac82147 (R5): two applications racing for one owner tax code. The existence check cannot see an
// application that is not committed yet, so the loser is stopped by the unique index ux_accounts_owner_tax_code.
// The loser must get the business error (409 identity.owner_tax_code_already_registered), never an unhandled exception (500).
public sealed class OwnerTaxCodeRaceIntegrationTests(PostgreSqlContainerFixture postgres)
    : IClassFixture<PostgreSqlContainerFixture>
{
    [Fact]
    public async Task An_application_that_loses_the_race_for_a_tax_code_gets_a_business_error_not_an_exception()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var provider = await BuildProviderAsync(ct);
        var winner = await CreateVerifiedAccountAsync(provider, ct);
        var loser = await CreateVerifiedAccountAsync(provider, ct);
        var taxCode = NewTaxCode();

        // The winner has saved its application but not committed yet: it holds the unique key of the tax code.
        await using var holdingScope = provider.CreateAsyncScope();
        var holdingDb = holdingScope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        await using var holding = await holdingDb.Database.BeginTransactionAsync(ct);
        var holder = await holdingDb.Accounts.SingleAsync(account => account.Id == winner, ct);
        holder.ApplyForParkingLotOwner(DateTimeOffset.UtcNow, taxCode, "Winner", OwnerType.HouseholdBusiness);
        await holdingDb.SaveChangesAsync(ct);

        // The loser passes the existence check (nothing is committed yet), then waits on the unique key.
        var loserTask = Task.Run(async () =>
        {
            await using var scope = provider.CreateAsyncScope();
            var services = scope.ServiceProvider;
            var handler = new ApplyForOwnerApplicationCommandHandler(
                services.GetRequiredService<IAccountRepository>(),
                services.GetRequiredService<IProfileRepository>(),
                services.GetRequiredService<IClock>());
            return await handler.Handle(
                new ApplyForOwnerApplicationCommand(loser, taxCode, "Loser", OwnerType: OwnerType.HouseholdBusiness), ct);
        }, ct);

        await WaitUntilAStatementWaitsOnTheKeyAsync(provider, ct);
        await holding.CommitAsync(ct);

        var result = await loserTask;

        Assert.True(result.IsFailure);
        Assert.Equal(OwnerApplicationErrors.TaxCodeAlreadyRegistered.Code, result.Error!.Code);

        // Exactly one account holds the code, and nothing of the losing application was kept.
        await using var verifyScope = provider.CreateAsyncScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var holders = await verifyDb.Accounts.AsNoTracking()
            .Where(account => account.OwnerTaxCode == taxCode).Select(account => account.Id).ToListAsync(ct);
        Assert.Equal([winner], holders);
        var loserAccount = await verifyDb.Accounts.AsNoTracking().SingleAsync(account => account.Id == loser, ct);
        Assert.Null(loserAccount.OwnerApplicationStatus);
        Assert.Null(loserAccount.OwnerBusinessName);
    }

    private static string NewTaxCode() => "09" + Random.Shared.Next(0, 100_000_000).ToString("D8");

    private async Task<ServiceProvider> BuildProviderAsync(CancellationToken ct)
    {
        var configuration = new ConfigurationBuilder()
            .AddConfiguration(PostgreSqlTestConfiguration.Create(postgres.ConnectionString))
            .AddConfiguration(RabbitMqTestConfiguration.Create("localhost", 5672, "user", "password", "/"))
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["OwnerDocuments:RootPath"] = Path.Combine(Path.GetTempPath(), "owner-tax-" + Guid.NewGuid().ToString("N"))
            })
            .Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddPersistence(configuration);
        services.AddInfrastructure(configuration);
        var provider = services.BuildServiceProvider(validateScopes: true);

        await using var scope = provider.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IdentityDbContext>().Database.MigrateAsync(ct);
        return provider;
    }

    private static async Task<Guid> CreateVerifiedAccountAsync(ServiceProvider provider, CancellationToken ct)
    {
        var id = Guid.NewGuid();
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var account = Account.Register(id, $"{id:N}@example.test", "hash", null, DateTimeOffset.UtcNow);
        account.VerifyEmail();
        db.Accounts.Add(account);
        await db.SaveChangesAsync(ct);
        return id;
    }

    // Waits until some statement is blocked on another transaction's lock, i.e. the loser really sits on the unique key.
    private static async Task WaitUntilAStatementWaitsOnTheKeyAsync(ServiceProvider provider, CancellationToken ct)
    {
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        for (var attempt = 0; attempt < 150; attempt++)
        {
            var waiting = await db.Database
                .SqlQuery<int>($"SELECT count(*)::int AS \"Value\" FROM pg_stat_activity WHERE wait_event_type = 'Lock' AND wait_event = 'transactionid'")
                .SingleAsync(ct);
            if (waiting > 0)
                return;
            await Task.Delay(100, ct);
        }

        Assert.Fail("The second application never reached the unique key of the tax code.");
    }
}
