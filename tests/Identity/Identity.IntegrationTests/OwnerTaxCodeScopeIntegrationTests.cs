using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ParkingSystem.BuildingBlocks.Results;
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

// R6 (option B): a tax code belongs to one application that is in force, that is Pending or Approved. A rejected
// application keeps the code in its record, to fill the form again, but no longer holds it against other accounts.
// Real handlers, real PostgreSQL and the real migrations.
public sealed class OwnerTaxCodeScopeIntegrationTests(PostgreSqlContainerFixture postgres)
    : IClassFixture<PostgreSqlContainerFixture>
{
    [Fact]
    public async Task A_rejected_application_gives_its_tax_code_back_to_other_accounts()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var provider = await BuildProviderAsync(ct);
        var first = await CreateVerifiedAccountAsync(provider, ct);
        var second = await CreateVerifiedAccountAsync(provider, ct);
        var taxCode = NewTaxCode();
        Assert.True((await ApplyAsync(provider, first, taxCode, ct)).IsSuccess);
        await RejectAsync(provider, first, ct);

        var result = await ApplyAsync(provider, second, taxCode, ct);

        Assert.True(result.IsSuccess, $"A rejected application still holds the tax code: {result.Error?.Code}");
        // The rejected record keeps the code to fill the form again; the new application holds it.
        var firstState = await ReadAsync(provider, first, ct);
        Assert.Equal(OwnerApplicationStatus.Rejected, firstState.Status);
        Assert.Equal(taxCode, firstState.TaxCode);
        var secondState = await ReadAsync(provider, second, ct);
        Assert.Equal(OwnerApplicationStatus.Pending, secondState.Status);
        Assert.Equal(taxCode, secondState.TaxCode);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_rejected_application_cannot_be_resubmitted_while_an_application_in_force_holds_its_tax_code(bool holderIsApproved)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var provider = await BuildProviderAsync(ct);
        var rejected = await CreateVerifiedAccountAsync(provider, ct);
        var holder = await CreateVerifiedAccountAsync(provider, ct);
        var taxCode = NewTaxCode();
        Assert.True((await ApplyAsync(provider, rejected, taxCode, ct)).IsSuccess);
        await RejectAsync(provider, rejected, ct);
        var holderApplied = await ApplyAsync(provider, holder, taxCode, ct);
        Assert.True(holderApplied.IsSuccess, $"A rejected application still holds the tax code: {holderApplied.Error?.Code}");
        if (holderIsApproved)
            await ApproveAsync(provider, holder, ct);

        // No new code is sent: the one the rejected record carries is checked again.
        var result = await ResubmitAsync(provider, rejected, ct);

        Assert.True(result.IsFailure);
        Assert.Equal(OwnerApplicationErrors.TaxCodeAlreadyRegistered.Code, result.Error!.Code);
        Assert.Equal(OwnerApplicationStatus.Rejected, (await ReadAsync(provider, rejected, ct)).Status);
    }

    [Fact]
    public async Task A_rejected_application_can_be_resubmitted_with_its_own_tax_code_when_nobody_else_holds_it()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var provider = await BuildProviderAsync(ct);
        var account = await CreateVerifiedAccountAsync(provider, ct);
        var taxCode = NewTaxCode();
        Assert.True((await ApplyAsync(provider, account, taxCode, ct)).IsSuccess);
        await RejectAsync(provider, account, ct);

        var result = await ResubmitAsync(provider, account, ct);

        Assert.True(result.IsSuccess, result.Error?.Code);
        var state = await ReadAsync(provider, account, ct);
        Assert.Equal(OwnerApplicationStatus.Pending, state.Status);
        Assert.Equal(taxCode, state.TaxCode);
    }

    [Fact]
    public async Task Two_rejected_applications_may_carry_the_same_tax_code_and_either_can_be_resubmitted()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var provider = await BuildProviderAsync(ct);
        var first = await CreateVerifiedAccountAsync(provider, ct);
        var second = await CreateVerifiedAccountAsync(provider, ct);
        var taxCode = NewTaxCode();
        Assert.True((await ApplyAsync(provider, first, taxCode, ct)).IsSuccess);
        await RejectAsync(provider, first, ct);
        var secondApplied = await ApplyAsync(provider, second, taxCode, ct);
        Assert.True(secondApplied.IsSuccess, $"A rejected application still holds the tax code: {secondApplied.Error?.Code}");
        await RejectAsync(provider, second, ct);

        var result = await ResubmitAsync(provider, first, ct);

        Assert.True(result.IsSuccess, result.Error?.Code);
        Assert.Equal(OwnerApplicationStatus.Pending, (await ReadAsync(provider, first, ct)).Status);
        var secondState = await ReadAsync(provider, second, ct);
        Assert.Equal(OwnerApplicationStatus.Rejected, secondState.Status);
        Assert.Equal(taxCode, secondState.TaxCode);
    }

    // The existence check cannot see an application that is not committed yet, so the unique index decides.
    // A resubmission that loses must get the business error (409), never an unhandled exception (500), and stay rejected.
    [Fact]
    public async Task A_resubmission_that_loses_the_race_for_the_tax_code_gets_the_business_error_and_stays_rejected()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var provider = await BuildProviderAsync(ct);
        var rejected = await CreateVerifiedAccountAsync(provider, ct);
        var holder = await CreateVerifiedAccountAsync(provider, ct);
        var taxCode = NewTaxCode();
        Assert.True((await ApplyAsync(provider, rejected, taxCode, ct)).IsSuccess);
        await RejectAsync(provider, rejected, ct);

        // Another account applies with the same code inside a transaction that is not committed yet: it holds the unique key.
        await using var holdingScope = provider.CreateAsyncScope();
        var holdingDb = holdingScope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        await using var holding = await holdingDb.Database.BeginTransactionAsync(ct);
        var holderAccount = await holdingDb.Accounts.SingleAsync(account => account.Id == holder, ct);
        holderAccount.ApplyForParkingLotOwner(DateTimeOffset.UtcNow, taxCode, "Holder", OwnerType.HouseholdBusiness);
        await holdingDb.SaveChangesAsync(ct);

        // The resubmission passes the existence check (nothing is committed), then waits on the unique key.
        var loserTask = Task.Run(() => ResubmitAsync(provider, rejected, ct), ct);
        await WaitUntilAStatementWaitsOnTheKeyAsync(provider, ct);
        await holding.CommitAsync(ct);

        var result = await loserTask;

        Assert.True(result.IsFailure);
        Assert.Equal(OwnerApplicationErrors.TaxCodeAlreadyRegistered.Code, result.Error!.Code);
        Assert.Equal(OwnerApplicationStatus.Rejected, (await ReadAsync(provider, rejected, ct)).Status);
        Assert.Equal(OwnerApplicationStatus.Pending, (await ReadAsync(provider, holder, ct)).Status);
    }

    [Fact]
    public async Task The_tax_code_index_covers_only_applications_in_force()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var provider = await BuildProviderAsync(ct);
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();

        var definition = await db.Database
            .SqlQuery<string>($"SELECT indexdef AS \"Value\" FROM pg_indexes WHERE schemaname = 'identity' AND indexname = 'ux_accounts_owner_tax_code'")
            .SingleAsync(ct);

        Assert.Contains("UNIQUE", definition);
        Assert.Contains("owner_application_status", definition);
        Assert.Contains("Pending", definition);
        Assert.Contains("Approved", definition);
        Assert.DoesNotContain("Rejected", definition);
    }

    private static string NewTaxCode() => "08" + Random.Shared.Next(0, 100_000_000).ToString("D8");

    private async Task<ServiceProvider> BuildProviderAsync(CancellationToken ct)
    {
        var configuration = new ConfigurationBuilder()
            .AddConfiguration(PostgreSqlTestConfiguration.Create(postgres.ConnectionString))
            .AddConfiguration(RabbitMqTestConfiguration.Create("localhost", 5672, "user", "password", "/"))
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["OwnerDocuments:RootPath"] = Path.Combine(Path.GetTempPath(), "owner-scope-" + Guid.NewGuid().ToString("N"))
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

    private static async Task<Result<OwnerApplicationActionResult>> ApplyAsync(
        ServiceProvider provider, Guid accountId, string taxCode, CancellationToken ct)
    {
        await using var scope = provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var handler = new ApplyForOwnerApplicationCommandHandler(
            services.GetRequiredService<IAccountRepository>(),
            services.GetRequiredService<IProfileRepository>(),
            services.GetRequiredService<IClock>());
        return await handler.Handle(
            new ApplyForOwnerApplicationCommand(accountId, taxCode, "Business", OwnerType: OwnerType.HouseholdBusiness), ct);
    }

    // The checklist of documents is not what these tests are about, so it is treated as complete.
    private static async Task<Result<OwnerApplicationActionResult>> ResubmitAsync(
        ServiceProvider provider, Guid accountId, CancellationToken ct)
    {
        await using var scope = provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var handler = new ResubmitOwnerApplicationCommandHandler(
            services.GetRequiredService<IAccountRepository>(),
            services.GetRequiredService<IOwnerDocumentRepository>(),
            new CompleteChecklist(),
            services.GetRequiredService<IClock>());
        return await handler.Handle(new ResubmitOwnerApplicationCommand(accountId), ct);
    }

    private static async Task RejectAsync(ServiceProvider provider, Guid accountId, CancellationToken ct)
    {
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var account = await db.Accounts.SingleAsync(candidate => candidate.Id == accountId, ct);
        account.RejectOwnerApplication(Guid.NewGuid(), "khong hop le", DateTimeOffset.UtcNow);
        await db.SaveChangesAsync(ct);
    }

    private static async Task ApproveAsync(ServiceProvider provider, Guid accountId, CancellationToken ct)
    {
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var account = await db.Accounts.SingleAsync(candidate => candidate.Id == accountId, ct);
        account.ApproveOwnerApplication(Guid.NewGuid(), DateTimeOffset.UtcNow);
        await db.SaveChangesAsync(ct);
    }

    private static async Task<(OwnerApplicationStatus? Status, string? TaxCode)> ReadAsync(
        ServiceProvider provider, Guid accountId, CancellationToken ct)
    {
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var account = await db.Accounts.AsNoTracking().SingleAsync(candidate => candidate.Id == accountId, ct);
        return (account.OwnerApplicationStatus, account.OwnerTaxCode);
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

        Assert.Fail("The resubmission never reached the unique key of the tax code.");
    }

    private sealed class CompleteChecklist : IOwnerDocumentRequirements
    {
        public long MaxFileSizeBytes => 5 * 1024 * 1024;
        public int MaxFilesPerApplication => 10;
        public IReadOnlyList<OwnerDocumentRequirementItem> GetRequirements(OwnerType ownerType) => [];
        public bool IsAllowed(OwnerType ownerType, string documentType) => true;
        public IReadOnlyList<string> GetMissingRequiredDocuments(OwnerType ownerType, IEnumerable<string> uploadedDocumentTypes) => [];
        public string? ResolveDocumentType(OwnerType ownerType, string? documentType) => documentType;
        public string? ResolveKnownDocumentType(string? documentType) => documentType;
    }
}
