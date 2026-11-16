using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ParkingSystem.BuildingBlocks.Results;
using ParkingSystem.BuildingBlocks.Time;
using ParkingSystem.Identity.Application.Accounts;
using ParkingSystem.Identity.Application.Accounts.OwnerApplications;
using ParkingSystem.Identity.Application.Common.Auditing;
using ParkingSystem.Identity.Application.Common.Messaging.Outbox;
using ParkingSystem.Identity.Domain.Accounts;
using ParkingSystem.Identity.Infrastructure;
using ParkingSystem.Identity.Persistence;
using ParkingSystem.Testing;
using Xunit;

namespace ParkingSystem.Tests.Identity.IntegrationTests;

// WP-B on a real PostgreSQL and a real directory: the role is granted only for a complete checklist, the decision and the
// documents of one application queue up behind the same account row lock, and the admin reads back exactly what was uploaded.
public sealed class OwnerApplicationReviewIntegrationTests(PostgreSqlContainerFixture postgres)
    : IClassFixture<PostgreSqlContainerFixture>
{
    private const string ApprovedEvent = "identity.owner-application-approved.v1";

    [Fact]
    public async Task Approve_is_refused_while_a_required_document_is_missing_and_granted_once_the_checklist_is_complete()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var harness = await CreateAsync(ct);
        var accountId = await NewApplicationAsync(harness, OwnerType.HouseholdBusiness, ct);
        Assert.True((await UploadAsync(harness, accountId, "PremisesRightProof", 0, ct)).IsSuccess);

        var refused = await ApproveAsync(harness, accountId, ct);

        Assert.False(refused.IsSuccess);
        Assert.Equal(OwnerApplicationErrors.DocumentsIncomplete.Code, refused.Error!.Code);
        var pending = await StateAsync(harness, accountId, ct);
        Assert.Equal(OwnerApplicationStatus.Pending, pending.Status);
        Assert.NotEqual(AccountRole.ParkingLotOwner, pending.Role);
        Assert.Equal(0, await ApprovedEventsAsync(harness, accountId, ct));

        Assert.True((await UploadAsync(harness, accountId, "HouseholdBusinessRegistration", 1, ct)).IsSuccess);
        var approved = await ApproveAsync(harness, accountId, ct);

        Assert.True(approved.IsSuccess, approved.Error?.Code);
        var granted = await StateAsync(harness, accountId, ct);
        Assert.Equal(OwnerApplicationStatus.Approved, granted.Status);
        Assert.Equal(AccountRole.ParkingLotOwner, granted.Role);
        Assert.Equal(1, await ApprovedEventsAsync(harness, accountId, ct));
    }

    [Fact]
    public async Task Approve_without_an_owner_type_is_refused_and_changes_nothing()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var harness = await CreateAsync(ct);
        var accountId = await NewApplicationAsync(harness, null, ct);

        var refused = await ApproveAsync(harness, accountId, ct);

        Assert.False(refused.IsSuccess);
        Assert.Equal(OwnerApplicationErrors.OwnerTypeRequired.Code, refused.Error!.Code);
        Assert.Equal(OwnerApplicationStatus.Pending, (await StateAsync(harness, accountId, ct)).Status);
    }

    // Deleting the last required document and approving are two requests on one application. Whichever the row lock lets in first,
    // the application must never end up Approved without the document the checklist demands.
    [Fact]
    public async Task Approve_racing_a_delete_never_leaves_an_approved_application_without_its_required_document()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var harness = await CreateAsync(ct);

        for (var round = 0; round < 12; round++)
        {
            var accountId = await NewApplicationAsync(harness, OwnerType.Individual, ct);
            Assert.True((await UploadAsync(harness, accountId, "PremisesRightProof", round, ct)).IsSuccess);

            var outcomes = await RunTogetherAsync<object>(
            [
                async () => await ApproveAsync(harness, accountId, ct),
                async () => await DeleteAsync(harness, accountId, "PremisesRightProof", ct)
            ]);

            var approve = (Result<OwnerApplicationActionResult>)outcomes[0];
            var state = await StateAsync(harness, accountId, ct);
            var rows = await DocumentRowsAsync(harness, accountId, ct);
            if (state.Status == OwnerApplicationStatus.Approved)
            {
                Assert.True(approve.IsSuccess, approve.Error?.Code);
                Assert.Equal(AccountRole.ParkingLotOwner, state.Role);
                Assert.Equal(1, rows);
            }
            else
            {
                Assert.False(approve.IsSuccess);
                Assert.Equal(OwnerApplicationErrors.DocumentsIncomplete.Code, approve.Error!.Code);
                Assert.Equal(OwnerApplicationStatus.Pending, state.Status);
                Assert.Equal(0, rows);
            }
        }
    }

    [Fact]
    public async Task Parallel_approvals_of_one_complete_application_grant_the_role_once()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var harness = await CreateAsync(ct);
        var accountId = await NewApplicationAsync(harness, OwnerType.Individual, ct);
        Assert.True((await UploadAsync(harness, accountId, "PremisesRightProof", 0, ct)).IsSuccess);

        var results = await RunTogetherAsync(
            Enumerable.Range(0, 6).Select<int, Func<Task<Result<OwnerApplicationActionResult>>>>(_ => () => ApproveAsync(harness, accountId, ct)));

        Assert.Single(results, result => result.IsSuccess);
        Assert.All(results.Where(result => !result.IsSuccess), result => Assert.Equal(OwnerApplicationErrors.NotPending.Code, result.Error!.Code));
        Assert.Equal(1, await ApprovedEventsAsync(harness, accountId, ct));
    }

    [Fact]
    public async Task Review_reads_back_exactly_what_the_applicant_uploaded()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var harness = await CreateAsync(ct);
        var accountId = await NewApplicationAsync(harness, OwnerType.Individual, ct);

        var before = await ListAsync(harness, accountId, ct);
        Assert.False(before.IsComplete);
        Assert.Equal(["PremisesRightProof"], before.MissingDocumentTypes);
        Assert.Empty(before.Documents);

        var uploaded = await UploadAsync(harness, accountId, "PremisesRightProof", 7, ct);
        Assert.True(uploaded.IsSuccess, uploaded.Error?.Code);

        var after = await ListAsync(harness, accountId, ct);
        Assert.True(after.IsComplete);
        Assert.Empty(after.MissingDocumentTypes);
        var listed = Assert.Single(after.Documents);
        Assert.Equal(uploaded.Value!.Id, listed.Id);
        Assert.Equal("application/pdf", listed.ContentType);

        var content = await ContentAsync(harness, accountId, "premisesrightproof", ct);
        Assert.True(content.IsSuccess, content.Error?.Code);
        await using var stream = content.Value!.Content;
        using var copy = new MemoryStream();
        await stream.CopyToAsync(copy, ct);
        Assert.Equal(PdfBytes(7), copy.ToArray());
        Assert.Equal(listed.SizeBytes, copy.Length);
    }

    [Fact]
    public async Task Review_reports_a_file_missing_from_disk_as_unavailable_while_the_row_is_still_listed()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var harness = await CreateAsync(ct);
        var accountId = await NewApplicationAsync(harness, OwnerType.Individual, ct);
        Assert.True((await UploadAsync(harness, accountId, "PremisesRightProof", 0, ct)).IsSuccess);
        foreach (var file in Directory.GetFiles(harness.Root))
            File.Delete(file);

        var content = await ContentAsync(harness, accountId, "PremisesRightProof", ct);

        Assert.False(content.IsSuccess);
        Assert.Equal(OwnerApplicationErrors.DocumentFileUnavailable.Code, content.Error!.Code);
        Assert.Single((await ListAsync(harness, accountId, ct)).Documents);
    }

    [Fact]
    public async Task Review_never_reaches_the_documents_of_another_application()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var harness = await CreateAsync(ct);
        var mine = await NewApplicationAsync(harness, OwnerType.Individual, ct);
        var theirs = await NewApplicationAsync(harness, OwnerType.Individual, ct);
        Assert.True((await UploadAsync(harness, theirs, "PremisesRightProof", 3, ct)).IsSuccess);

        var content = await ContentAsync(harness, mine, "PremisesRightProof", ct);

        Assert.False(content.IsSuccess);
        Assert.Equal(OwnerApplicationErrors.DocumentNotFound.Code, content.Error!.Code);
        Assert.Empty((await ListAsync(harness, mine, ct)).Documents);
    }

    private sealed class Harness(ServiceProvider provider, string root, Guid adminId) : IAsyncDisposable
    {
        public ServiceProvider Provider { get; } = provider;

        public string Root { get; } = root;

        public Guid AdminId { get; } = adminId;

        public async ValueTask DisposeAsync()
        {
            await Provider.DisposeAsync();
            try
            {
                Directory.Delete(Root, recursive: true);
            }
            catch (IOException)
            {
                // A test directory that cannot be removed is harmless.
            }
        }
    }

    private async Task<Harness> CreateAsync(CancellationToken ct)
    {
        var root = Path.Combine(Path.GetTempPath(), "owner-review-" + Guid.NewGuid().ToString("N"));
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

        var adminId = Guid.NewGuid();
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        await db.Database.MigrateAsync(ct);
        var admin = Account.Register(adminId, $"{adminId:N}@example.test", "hash", null, DateTimeOffset.UtcNow);
        admin.AssignRole(AccountRole.SystemAdmin);
        db.Accounts.Add(admin);
        await db.SaveChangesAsync(ct);
        return new Harness(provider, root, adminId);
    }

    private static async Task<Guid> NewApplicationAsync(Harness harness, OwnerType? ownerType, CancellationToken ct)
    {
        var accountId = Guid.NewGuid();
        await using var scope = harness.Provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var account = Account.Register(accountId, $"{accountId:N}@example.test", "hash", null, DateTimeOffset.UtcNow, applyAsParkingLotOwner: true);
        if (ownerType is { } type)
            account.SetOwnerType(type);
        db.Accounts.Add(account);
        await db.SaveChangesAsync(ct);
        return accountId;
    }

    private static byte[] PdfBytes(int index)
    {
        var bytes = new byte[300 + index];
        "%PDF-1.7\n"u8.CopyTo(bytes);
        return bytes;
    }

    private static async Task<Result<OwnerDocumentDto>> UploadAsync(Harness harness, Guid accountId, string documentType, int index, CancellationToken ct)
    {
        await using var scope = harness.Provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var handler = new UploadOwnerDocumentCommandHandler(
            services.GetRequiredService<IAccountRepository>(),
            services.GetRequiredService<IOwnerDocumentRepository>(),
            services.GetRequiredService<IOwnerDocumentStore>(),
            services.GetRequiredService<IOwnerDocumentRequirements>(),
            services.GetRequiredService<IClock>());
        var bytes = PdfBytes(index);
        return await handler.Handle(
            new UploadOwnerDocumentCommand(accountId, documentType, $"f{index}.pdf", new MemoryStream(bytes), bytes.Length), ct);
    }

    private static async Task<Result<bool>> DeleteAsync(Harness harness, Guid accountId, string documentType, CancellationToken ct)
    {
        await using var scope = harness.Provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var handler = new DeleteOwnerDocumentCommandHandler(
            services.GetRequiredService<IAccountRepository>(),
            services.GetRequiredService<IOwnerDocumentRepository>(),
            services.GetRequiredService<IOwnerDocumentStore>(),
            services.GetRequiredService<IOwnerDocumentRequirements>());
        return await handler.Handle(new DeleteOwnerDocumentCommand(accountId, documentType), ct);
    }

    private static async Task<Result<OwnerApplicationActionResult>> ApproveAsync(Harness harness, Guid accountId, CancellationToken ct)
    {
        await using var scope = harness.Provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var handler = new ApproveOwnerApplicationCommandHandler(
            services.GetRequiredService<IAccountRepository>(),
            services.GetRequiredService<IAuthSessionRepository>(),
            services.GetRequiredService<IRefreshTokenRepository>(),
            services.GetRequiredService<IOwnerDocumentRepository>(),
            services.GetRequiredService<IOwnerDocumentRequirements>(),
            services.GetRequiredService<IOutboxWriter>(),
            services.GetRequiredService<IClock>(),
            services.GetRequiredService<IIdentityAuditLog>());
        return await handler.Handle(new ApproveOwnerApplicationCommand(harness.AdminId, accountId), ct);
    }

    private static async Task<OwnerApplicationDocumentsDto> ListAsync(Harness harness, Guid accountId, CancellationToken ct)
    {
        await using var scope = harness.Provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var handler = new GetOwnerApplicationDocumentsQueryHandler(
            services.GetRequiredService<IAccountRepository>(),
            services.GetRequiredService<IOwnerDocumentRepository>(),
            services.GetRequiredService<IOwnerDocumentRequirements>());
        var result = await handler.Handle(new GetOwnerApplicationDocumentsQuery(accountId), ct);
        Assert.True(result.IsSuccess, result.Error?.Code);
        return result.Value!;
    }

    private static async Task<Result<OwnerDocumentContent>> ContentAsync(Harness harness, Guid accountId, string documentType, CancellationToken ct)
    {
        await using var scope = harness.Provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var handler = new GetOwnerApplicationDocumentContentQueryHandler(
            services.GetRequiredService<IAccountRepository>(),
            services.GetRequiredService<IOwnerDocumentRepository>(),
            services.GetRequiredService<IOwnerDocumentStore>(),
            services.GetRequiredService<IOwnerDocumentRequirements>());
        return await handler.Handle(new GetOwnerApplicationDocumentContentQuery(accountId, documentType), ct);
    }

    private static async Task<(OwnerApplicationStatus? Status, AccountRole Role)> StateAsync(Harness harness, Guid accountId, CancellationToken ct)
    {
        await using var scope = harness.Provider.CreateAsyncScope();
        var account = await scope.ServiceProvider.GetRequiredService<IdentityDbContext>()
            .Accounts.AsNoTracking().SingleAsync(a => a.Id == accountId, ct);
        return (account.OwnerApplicationStatus, account.Role);
    }

    private static async Task<int> DocumentRowsAsync(Harness harness, Guid accountId, CancellationToken ct)
    {
        await using var scope = harness.Provider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IdentityDbContext>()
            .OwnerApplicationDocuments.CountAsync(document => document.AccountId == accountId, ct);
    }

    // The payload is read back and searched here because it is stored as JSON text.
    private static async Task<int> ApprovedEventsAsync(Harness harness, Guid accountId, CancellationToken ct)
    {
        await using var scope = harness.Provider.CreateAsyncScope();
        var payloads = await scope.ServiceProvider.GetRequiredService<IdentityDbContext>()
            .OutboxMessages.AsNoTracking().Where(message => message.EventType == ApprovedEvent).Select(message => message.Payload).ToListAsync(ct);
        return payloads.Count(payload => payload.Contains(accountId.ToString(), StringComparison.OrdinalIgnoreCase));
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
