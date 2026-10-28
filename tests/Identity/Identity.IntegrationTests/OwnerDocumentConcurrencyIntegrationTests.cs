using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ParkingSystem.BuildingBlocks.Results;
using ParkingSystem.BuildingBlocks.Time;
using ParkingSystem.Identity.Application.Accounts;
using ParkingSystem.Identity.Application.Accounts.OwnerApplications;
using ParkingSystem.Identity.Domain.Accounts;
using ParkingSystem.Identity.Infrastructure;
using ParkingSystem.Identity.Persistence;
using ParkingSystem.Testing;
using Xunit;

namespace ParkingSystem.Tests.Identity.IntegrationTests;

// F1, F2 and F8 on a real PostgreSQL and a real directory: concurrent requests of one application queue up behind the
// account row lock, leave one row and one file per document type, and never fail or leave a file nobody points at.
public sealed class OwnerDocumentConcurrencyIntegrationTests(PostgreSqlContainerFixture postgres)
    : IClassFixture<PostgreSqlContainerFixture>
{
    [Fact]
    public async Task Parallel_uploads_of_one_document_type_all_succeed_and_leave_one_row_and_one_file()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var harness = await CreateAsync(ct);

        var results = await RunTogetherAsync(
            Enumerable.Range(0, 8).Select<int, Func<Task<Result<OwnerDocumentDto>>>>(index => () => UploadAsync(harness, "PremisesRightProof", index, ct)));

        Assert.All(results, result => Assert.True(result.IsSuccess, result.Error?.Code));
        Assert.Equal(1, await CountRowsAsync(harness, ct));
        Assert.Equal(1, harness.FilesOnDisk);
    }

    [Fact]
    public async Task Parallel_uploads_and_deletes_end_with_exactly_as_many_files_as_rows()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var harness = await CreateAsync(ct);

        var operations = Enumerable.Range(0, 6).Select<int, Func<Task<bool>>>(index => index % 2 == 0
            ? async () => (await UploadAsync(harness, "PremisesRightProof", index, ct)).IsSuccess
            : async () => (await DeleteAsync(harness, "PremisesRightProof", ct)).IsSuccess);
        await RunTogetherAsync(operations);

        var rows = await CountRowsAsync(harness, ct);
        Assert.InRange(rows, 0, 1);
        Assert.Equal(rows, harness.FilesOnDisk);
    }

    [Fact]
    public async Task One_type_in_two_casings_is_one_row_and_one_file_and_deletes_whatever_the_casing()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var harness = await CreateAsync(ct);

        var first = await UploadAsync(harness, "PremisesRightProof", 0, ct);
        var second = await UploadAsync(harness, "premisesrightproof", 1, ct);

        Assert.True(first.IsSuccess, first.Error?.Code);
        Assert.True(second.IsSuccess, second.Error?.Code);
        Assert.Equal("PremisesRightProof", second.Value!.DocumentType);
        Assert.Equal(["PremisesRightProof"], await ListTypesAsync(harness, ct));
        Assert.Equal(1, harness.FilesOnDisk);

        var deleted = await DeleteAsync(harness, "PREMISESRIGHTPROOF", ct);

        Assert.True(deleted.IsSuccess, deleted.Error?.Code);
        Assert.Equal(0, await CountRowsAsync(harness, ct));
        Assert.Equal(0, harness.FilesOnDisk);
    }

    private sealed class Harness(ServiceProvider provider, string root, Guid accountId) : IAsyncDisposable
    {
        public ServiceProvider Provider { get; } = provider;

        public Guid AccountId { get; } = accountId;

        public int FilesOnDisk => Directory.Exists(root) ? Directory.GetFiles(root).Length : 0;

        public async ValueTask DisposeAsync()
        {
            await Provider.DisposeAsync();
            try
            {
                Directory.Delete(root, recursive: true);
            }
            catch (IOException)
            {
                // A test directory that cannot be removed is harmless.
            }
        }
    }

    private async Task<Harness> CreateAsync(CancellationToken ct)
    {
        var root = Path.Combine(Path.GetTempPath(), "owner-docs-" + Guid.NewGuid().ToString("N"));
        var configuration = new ConfigurationBuilder()
            .AddConfiguration(PostgreSqlTestConfiguration.Create(postgres.ConnectionString))
            .AddConfiguration(RabbitMqTestConfiguration.Create("localhost", 5672, "user", "password", "/"))
            .AddInMemoryCollection(new Dictionary<string, string?> { ["OwnerDocuments:RootPath"] = root })
            .Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddPersistence(configuration);
        services.AddInfrastructure(configuration);
        var provider = services.BuildServiceProvider(validateScopes: true);

        var accountId = Guid.NewGuid();
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        await db.Database.MigrateAsync(ct);
        var account = Account.Register(accountId, $"{accountId:N}@example.test", "hash", null, DateTimeOffset.UtcNow, applyAsParkingLotOwner: true);
        account.SetOwnerType(OwnerType.HouseholdBusiness);
        db.Accounts.Add(account);
        await db.SaveChangesAsync(ct);
        return new Harness(provider, root, accountId);
    }

    private static async Task<Result<OwnerDocumentDto>> UploadAsync(Harness harness, string documentType, int index, CancellationToken ct)
    {
        await using var scope = harness.Provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var handler = new UploadOwnerDocumentCommandHandler(
            services.GetRequiredService<IAccountRepository>(),
            services.GetRequiredService<IOwnerDocumentRepository>(),
            services.GetRequiredService<IOwnerDocumentStore>(),
            services.GetRequiredService<IOwnerDocumentRequirements>(),
            services.GetRequiredService<IClock>());
        var bytes = new byte[300 + index];
        "%PDF-1.7\n"u8.CopyTo(bytes);
        return await handler.Handle(
            new UploadOwnerDocumentCommand(harness.AccountId, documentType, $"f{index}.pdf", new MemoryStream(bytes), bytes.Length), ct);
    }

    private static async Task<Result<bool>> DeleteAsync(Harness harness, string documentType, CancellationToken ct)
    {
        await using var scope = harness.Provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var handler = new DeleteOwnerDocumentCommandHandler(
            services.GetRequiredService<IAccountRepository>(),
            services.GetRequiredService<IOwnerDocumentRepository>(),
            services.GetRequiredService<IOwnerDocumentStore>(),
            services.GetRequiredService<IOwnerDocumentRequirements>());
        return await handler.Handle(new DeleteOwnerDocumentCommand(harness.AccountId, documentType), ct);
    }

    private static async Task<int> CountRowsAsync(Harness harness, CancellationToken ct)
    {
        await using var scope = harness.Provider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IdentityDbContext>()
            .OwnerApplicationDocuments.CountAsync(document => document.AccountId == harness.AccountId, ct);
    }

    private static async Task<List<string>> ListTypesAsync(Harness harness, CancellationToken ct)
    {
        await using var scope = harness.Provider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IdentityDbContext>()
            .OwnerApplicationDocuments.Where(document => document.AccountId == harness.AccountId)
            .Select(document => document.DocumentType).ToListAsync(ct);
    }

    // Releases every operation at the same moment so they really overlap.
    private static async Task<T[]> RunTogetherAsync<T>(IEnumerable<Func<Task<T>>> operations)
    {
        var gate = new TaskCompletionSource();
        var tasks = operations.Select(async operation =>
        {
            await gate.Task;
            return await operation();
        }).ToArray();
        gate.SetResult();
        return await Task.WhenAll(tasks);
    }
}
