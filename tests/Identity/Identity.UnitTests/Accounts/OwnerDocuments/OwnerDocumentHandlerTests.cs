using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using ParkingSystem.BuildingBlocks.Results;
using ParkingSystem.BuildingBlocks.Time;
using ParkingSystem.Identity.Application.Accounts;
using ParkingSystem.Identity.Application.Accounts.OwnerApplications;
using ParkingSystem.Identity.Domain.Accounts;
using ParkingSystem.Identity.Infrastructure.OwnerDocuments;
using Xunit;

namespace ParkingSystem.Tests.Identity.UnitTests.Accounts.OwnerDocuments;

public sealed class OwnerDocumentHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 23, 10, 0, 0, TimeSpan.Zero);
    private static readonly Guid AccountId = Guid.NewGuid();

    private static readonly byte[] ValidPdfBytes = Encoding.ASCII.GetBytes("%PDF-1.4\n%âãÏÓ\n1 0 obj\n<<>>\nendobj");
    private static readonly string ValidPdfSha256 = Convert.ToHexString(SHA256.HashData(ValidPdfBytes)).ToLowerInvariant();

    [Fact]
    public async Task Upload_account_not_found_returns_ApplicationNotFound()
    {
        var accounts = new FakeAccountRepo(null);
        var docs = new FakeDocRepo();
        var store = new FakeDocStore();
        var reqs = CreateRequirements();

        var handler = new UploadOwnerDocumentCommandHandler(accounts, docs, store, reqs, new FixedClock(Now));

        using var stream = new MemoryStream(ValidPdfBytes);
        var command = new UploadOwnerDocumentCommand(AccountId, "PremisesRightProof", "mat_bang.pdf", stream, ValidPdfBytes.Length);

        var result = await handler.Handle(command, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(OwnerApplicationErrors.ApplicationNotFound.Code, result.Error!.Code);
    }

    [Fact]
    public async Task Upload_when_application_is_approved_returns_ApplicationLocked()
    {
        var account = Account.Register(AccountId, "owner@example.com", "hash", null, Now, applyAsParkingLotOwner: true);
        account.SetOwnerType(OwnerType.Individual);
        account.ApproveOwnerApplication(Guid.NewGuid(), Now.AddHours(1));

        var accounts = new FakeAccountRepo(account);
        var docs = new FakeDocRepo();
        var store = new FakeDocStore();
        var reqs = CreateRequirements();

        var handler = new UploadOwnerDocumentCommandHandler(accounts, docs, store, reqs, new FixedClock(Now));

        using var stream = new MemoryStream(ValidPdfBytes);
        var command = new UploadOwnerDocumentCommand(AccountId, "PremisesRightProof", "mat_bang.pdf", stream, ValidPdfBytes.Length);

        var result = await handler.Handle(command, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(OwnerApplicationErrors.ApplicationLocked.Code, result.Error!.Code);
    }

    [Fact]
    public async Task Upload_without_owner_type_returns_OwnerTypeRequired()
    {
        var account = Account.Register(AccountId, "owner@example.com", "hash", null, Now, applyAsParkingLotOwner: true);
        // OwnerType is null

        var accounts = new FakeAccountRepo(account);
        var docs = new FakeDocRepo();
        var store = new FakeDocStore();
        var reqs = CreateRequirements();

        var handler = new UploadOwnerDocumentCommandHandler(accounts, docs, store, reqs, new FixedClock(Now));

        using var stream = new MemoryStream(ValidPdfBytes);
        var command = new UploadOwnerDocumentCommand(AccountId, "PremisesRightProof", "mat_bang.pdf", stream, ValidPdfBytes.Length);

        var result = await handler.Handle(command, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(OwnerApplicationErrors.OwnerTypeRequired.Code, result.Error!.Code);
    }

    [Fact]
    public async Task Upload_disallowed_document_type_for_owner_type_returns_DocumentInvalidType()
    {
        var account = Account.Register(AccountId, "owner@example.com", "hash", null, Now, applyAsParkingLotOwner: true);
        account.SetOwnerType(OwnerType.Individual); // Individual cannot upload EnterpriseRegistration

        var accounts = new FakeAccountRepo(account);
        var docs = new FakeDocRepo();
        var store = new FakeDocStore();
        var reqs = CreateRequirements();

        var handler = new UploadOwnerDocumentCommandHandler(accounts, docs, store, reqs, new FixedClock(Now));

        using var stream = new MemoryStream(ValidPdfBytes);
        var command = new UploadOwnerDocumentCommand(AccountId, "EnterpriseRegistration", "gpdk.pdf", stream, ValidPdfBytes.Length);

        var result = await handler.Handle(command, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(OwnerApplicationErrors.DocumentInvalidType.Code, result.Error!.Code);
    }

    [Fact]
    public async Task Upload_spoofed_or_unsupported_content_returns_DocumentUnsupportedContent()
    {
        var account = Account.Register(AccountId, "owner@example.com", "hash", null, Now, applyAsParkingLotOwner: true);
        account.SetOwnerType(OwnerType.Individual);

        var accounts = new FakeAccountRepo(account);
        var docs = new FakeDocRepo();
        var store = new FakeDocStore();
        var reqs = CreateRequirements();

        var handler = new UploadOwnerDocumentCommandHandler(accounts, docs, store, reqs, new FixedClock(Now));

        // Text file disguised as pdf
        var fakeBytes = Encoding.UTF8.GetBytes("fake file content that is not pdf or image");
        using var stream = new MemoryStream(fakeBytes);
        var command = new UploadOwnerDocumentCommand(AccountId, "PremisesRightProof", "fake.pdf", stream, fakeBytes.Length);

        var result = await handler.Handle(command, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(OwnerApplicationErrors.DocumentUnsupportedContent.Code, result.Error!.Code);
    }

    [Fact]
    public async Task Upload_exceeding_max_file_size_returns_DocumentTooLarge()
    {
        var account = Account.Register(AccountId, "owner@example.com", "hash", null, Now, applyAsParkingLotOwner: true);
        account.SetOwnerType(OwnerType.Individual);

        var accounts = new FakeAccountRepo(account);
        var docs = new FakeDocRepo();
        var store = new FakeDocStore();
        var reqs = CreateRequirements(maxFileSize: 10); // Limit to 10 bytes

        var handler = new UploadOwnerDocumentCommandHandler(accounts, docs, store, reqs, new FixedClock(Now));

        using var stream = new MemoryStream(ValidPdfBytes); // > 10 bytes
        var command = new UploadOwnerDocumentCommand(AccountId, "PremisesRightProof", "mat_bang.pdf", stream, ValidPdfBytes.Length);

        var result = await handler.Handle(command, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(OwnerApplicationErrors.DocumentTooLarge.Code, result.Error!.Code);
    }

    [Fact]
    public async Task Upload_exceeding_max_files_limit_returns_DocumentLimitReached()
    {
        var account = Account.Register(AccountId, "owner@example.com", "hash", null, Now, applyAsParkingLotOwner: true);
        account.SetOwnerType(OwnerType.Individual);

        var accounts = new FakeAccountRepo(account);
        var docs = new FakeDocRepo();
        docs.Documents.Add(OwnerApplicationDocument.Create(Guid.NewGuid(), AccountId, "doc1", "f1.pdf", "application/pdf", 100, ValidPdfSha256, "k1", Now));
        docs.Documents.Add(OwnerApplicationDocument.Create(Guid.NewGuid(), AccountId, "doc2", "f2.pdf", "application/pdf", 100, ValidPdfSha256, "k2", Now));

        var store = new FakeDocStore();
        var reqs = CreateRequirements(maxFiles: 2); // Limit is 2

        var handler = new UploadOwnerDocumentCommandHandler(accounts, docs, store, reqs, new FixedClock(Now));

        using var stream = new MemoryStream(ValidPdfBytes);
        var command = new UploadOwnerDocumentCommand(AccountId, "PremisesRightProof", "mat_bang.pdf", stream, ValidPdfBytes.Length);

        var result = await handler.Handle(command, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(OwnerApplicationErrors.DocumentLimitReached.Code, result.Error!.Code);
    }

    [Fact]
    public async Task Upload_new_valid_document_persists_to_store_and_repository()
    {
        var account = Account.Register(AccountId, "owner@example.com", "hash", null, Now, applyAsParkingLotOwner: true);
        account.SetOwnerType(OwnerType.Individual);

        var accounts = new FakeAccountRepo(account);
        var docs = new FakeDocRepo();
        var store = new FakeDocStore();
        var reqs = CreateRequirements();

        var handler = new UploadOwnerDocumentCommandHandler(accounts, docs, store, reqs, new FixedClock(Now));

        using var stream = new MemoryStream(ValidPdfBytes);
        var command = new UploadOwnerDocumentCommand(AccountId, "PremisesRightProof", "mat_bang.pdf", stream, ValidPdfBytes.Length);

        var result = await handler.Handle(command, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal("PremisesRightProof", result.Value!.DocumentType);
        Assert.Equal("mat_bang.pdf", result.Value.FileName);
        Assert.Equal("application/pdf", result.Value.ContentType);
        Assert.Equal(ValidPdfBytes.Length, result.Value.SizeBytes);

        Assert.Single(docs.Documents);
        Assert.Single(store.StoredFiles);
    }

    [Fact]
    public async Task Upload_replaces_existing_document_and_purges_old_file_from_store()
    {
        var account = Account.Register(AccountId, "owner@example.com", "hash", null, Now, applyAsParkingLotOwner: true);
        account.SetOwnerType(OwnerType.Individual);

        var accounts = new FakeAccountRepo(account);
        var docs = new FakeDocRepo();
        var store = new FakeDocStore();
        store.StoredFiles["old_key.dat"] = [1, 2, 3];

        var existingDoc = OwnerApplicationDocument.Create(
            Guid.NewGuid(),
            AccountId,
            "PremisesRightProof",
            "old_mat_bang.pdf",
            "application/pdf",
            3,
            "0000000000000000000000000000000000000000000000000000000000000000",
            "old_key.dat",
            Now.AddDays(-1));
        docs.Documents.Add(existingDoc);

        var reqs = CreateRequirements();

        var handler = new UploadOwnerDocumentCommandHandler(accounts, docs, store, reqs, new FixedClock(Now));

        using var stream = new MemoryStream(ValidPdfBytes);
        var command = new UploadOwnerDocumentCommand(AccountId, "PremisesRightProof", "new_mat_bang.pdf", stream, ValidPdfBytes.Length);

        var result = await handler.Handle(command, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Single(docs.Documents);
        Assert.Equal("new_mat_bang.pdf", docs.Documents[0].OriginalFileName);
        Assert.False(store.StoredFiles.ContainsKey("old_key.dat")); // Old file purged
    }

    [Fact]
    public async Task Delete_document_removes_from_repository_and_storage()
    {
        var account = Account.Register(AccountId, "owner@example.com", "hash", null, Now, applyAsParkingLotOwner: true);
        account.SetOwnerType(OwnerType.Individual);

        var accounts = new FakeAccountRepo(account);
        var docs = new FakeDocRepo();
        var store = new FakeDocStore();
        store.StoredFiles["doc_key.dat"] = ValidPdfBytes;

        var existingDoc = OwnerApplicationDocument.Create(
            Guid.NewGuid(),
            AccountId,
            "PremisesRightProof",
            "mat_bang.pdf",
            "application/pdf",
            ValidPdfBytes.Length,
            ValidPdfSha256,
            "doc_key.dat",
            Now);
        docs.Documents.Add(existingDoc);

        var handler = new DeleteOwnerDocumentCommandHandler(accounts, docs, store);

        var result = await handler.Handle(new DeleteOwnerDocumentCommand(AccountId, "PremisesRightProof"), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Empty(docs.Documents);
        Assert.False(store.StoredFiles.ContainsKey("doc_key.dat"));
    }

    [Fact]
    public async Task Delete_unknown_document_returns_DocumentNotFound()
    {
        var account = Account.Register(AccountId, "owner@example.com", "hash", null, Now, applyAsParkingLotOwner: true);
        account.SetOwnerType(OwnerType.Individual);

        var accounts = new FakeAccountRepo(account);
        var docs = new FakeDocRepo();
        var store = new FakeDocStore();

        var handler = new DeleteOwnerDocumentCommandHandler(accounts, docs, store);

        var result = await handler.Handle(new DeleteOwnerDocumentCommand(AccountId, "non_existent"), TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(OwnerApplicationErrors.DocumentNotFound.Code, result.Error!.Code);
    }

    [Fact]
    public async Task UpdateOwnerType_successfully_changes_type()
    {
        var account = Account.Register(AccountId, "owner@example.com", "hash", null, Now, applyAsParkingLotOwner: true);

        var accounts = new FakeAccountRepo(account);

        var handler = new UpdateOwnerTypeCommandHandler(accounts);

        var result = await handler.Handle(new UpdateOwnerTypeCommand(AccountId, OwnerType.Enterprise), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal("Enterprise", result.Value!.OwnerType);
        Assert.Equal(OwnerType.Enterprise, account.OwnerType);
    }

    [Fact]
    public async Task Resubmit_without_owner_type_fails_with_OwnerTypeRequired()
    {
        var account = Account.Register(AccountId, "owner@example.com", "hash", null, Now, applyAsParkingLotOwner: true);
        account.RejectOwnerApplication(Guid.NewGuid(), "Need more docs", Now.AddDays(-1));
        // OwnerType is null

        var accounts = new FakeAccountRepo(account);
        var docs = new FakeDocRepo();
        var reqs = CreateRequirements();

        var handler = new ResubmitOwnerApplicationCommandHandler(accounts, docs, reqs, new FixedClock(Now));

        var result = await handler.Handle(new ResubmitOwnerApplicationCommand(AccountId), TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(OwnerApplicationErrors.OwnerTypeRequired.Code, result.Error!.Code);
    }

    [Fact]
    public async Task Resubmit_with_incomplete_documents_fails_with_DocumentsIncomplete()
    {
        var account = Account.Register(AccountId, "owner@example.com", "hash", null, Now, applyAsParkingLotOwner: true);
        account.SetOwnerType(OwnerType.HouseholdBusiness); // Requires HouseholdBusinessRegistration + PremisesRightProof
        account.RejectOwnerApplication(Guid.NewGuid(), "Missing license", Now.AddDays(-1));

        var accounts = new FakeAccountRepo(account);
        var docs = new FakeDocRepo();
        // Only uploaded PremisesRightProof
        docs.Documents.Add(OwnerApplicationDocument.Create(
            Guid.NewGuid(), AccountId, "PremisesRightProof", "mat_bang.pdf", "application/pdf", 100, ValidPdfSha256, "k1", Now));

        var reqs = CreateRequirements();

        var handler = new ResubmitOwnerApplicationCommandHandler(accounts, docs, reqs, new FixedClock(Now));

        var result = await handler.Handle(new ResubmitOwnerApplicationCommand(AccountId), TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(OwnerApplicationErrors.DocumentsIncomplete.Code, result.Error!.Code);
    }

    [Fact]
    public async Task Resubmit_with_all_required_documents_transitions_to_Pending()
    {
        var account = Account.Register(AccountId, "owner@example.com", "hash", null, Now, applyAsParkingLotOwner: true);
        account.SetOwnerType(OwnerType.Individual); // Requires PremisesRightProof
        account.RejectOwnerApplication(Guid.NewGuid(), "Missing docs", Now.AddDays(-1));

        var accounts = new FakeAccountRepo(account);
        var docs = new FakeDocRepo();
        // Uploaded PremisesRightProof
        docs.Documents.Add(OwnerApplicationDocument.Create(
            Guid.NewGuid(), AccountId, "PremisesRightProof", "mat_bang.pdf", "application/pdf", 100, ValidPdfSha256, "k1", Now));

        var reqs = CreateRequirements();

        var handler = new ResubmitOwnerApplicationCommandHandler(accounts, docs, reqs, new FixedClock(Now));

        var result = await handler.Handle(new ResubmitOwnerApplicationCommand(AccountId), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal("Pending", result.Value!.Status);
        Assert.Equal(OwnerApplicationStatus.Pending, account.OwnerApplicationStatus);
    }

    [Fact]
    public async Task ListMyOwnerDocuments_returns_all_uploaded_documents_without_storage_key()
    {
        var account = Account.Register(AccountId, "owner@example.com", "hash", null, Now, applyAsParkingLotOwner: true);
        var accounts = new FakeAccountRepo(account);

        var docs = new FakeDocRepo();
        docs.Documents.Add(OwnerApplicationDocument.Create(Guid.NewGuid(), AccountId, "PremisesRightProof", "mb.pdf", "application/pdf", 100, ValidPdfSha256, "secret_key_1.dat", Now));
        docs.Documents.Add(OwnerApplicationDocument.Create(Guid.NewGuid(), AccountId, "AuthorizationLetter", "uq.pdf", "application/pdf", 200, ValidPdfSha256, "secret_key_2.dat", Now));

        var handler = new ListMyOwnerDocumentsQueryHandler(accounts, docs);

        var result = await handler.Handle(new ListMyOwnerDocumentsQuery(AccountId), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value!.Count);
        Assert.Equal("PremisesRightProof", result.Value[0].DocumentType);
        Assert.Equal("AuthorizationLetter", result.Value[1].DocumentType);
    }

    [Fact]
    public async Task GetOwnerDocumentRequirements_returns_checklist_for_owner_type()
    {
        var reqs = CreateRequirements();
        var handler = new GetOwnerDocumentRequirementsQueryHandler(reqs);

        var result = await handler.Handle(new GetOwnerDocumentRequirementsQuery(OwnerType.HouseholdBusiness), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal("HouseholdBusiness", result.Value!.OwnerType);
        Assert.Contains(result.Value.Documents, d => d.DocumentType == "HouseholdBusinessRegistration" && d.Required);
    }

    private static IOwnerDocumentRequirements CreateRequirements(long? maxFileSize = null, int? maxFiles = null)
    {
        var options = Options.Create(new OwnerDocumentsOptions
        {
            MaxFileSizeBytes = maxFileSize ?? 5 * 1024 * 1024,
            MaxFilesPerApplication = maxFiles ?? 10
        });
        return new ConfigurationOwnerDocumentRequirements(options);
    }

    private sealed class FixedClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset UtcNow => now;
    }

    private sealed class FakeAccountRepo(Account? account) : IAccountRepository
    {
        public Task<Account?> FindByIdAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult(account is not null && account.Id == id ? account : null);

        public Task<Account?> FindByIdForUpdateAsync(Guid id, CancellationToken cancellationToken) =>
            FindByIdAsync(id, cancellationToken);

        public Task<Account?> FindByEmailAsync(string normalizedEmail, CancellationToken cancellationToken) => Task.FromResult<Account?>(null);
        public Task<Account?> FindByEmailForUpdateAsync(string normalizedEmail, CancellationToken cancellationToken) => Task.FromResult<Account?>(null);
        public Task<bool> EmailExistsAsync(string normalizedEmail, CancellationToken cancellationToken) => Task.FromResult(false);
        public void Add(Account a) { }
        public Task<SaveAccountResult> SaveNewAccountAsync(CancellationToken cancellationToken) => Task.FromResult(SaveAccountResult.Success);
        public Task<bool> TrySaveChangesAsync(CancellationToken cancellationToken) => Task.FromResult(true);
        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<TResult> ExecuteInTransactionAsync<TResult>(Func<Task<TResult>> action, CancellationToken cancellationToken) => action();
    }

    private sealed class FakeDocRepo : IOwnerDocumentRepository
    {
        public List<OwnerApplicationDocument> Documents { get; } = [];

        public Task<OwnerApplicationDocument?> FindByAccountAndTypeAsync(Guid accountId, string documentType, CancellationToken cancellationToken) =>
            Task.FromResult(Documents.FirstOrDefault(d => d.AccountId == accountId && d.DocumentType == documentType));

        public Task<IReadOnlyList<OwnerApplicationDocument>> ListByAccountAsync(Guid accountId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<OwnerApplicationDocument>>(Documents.Where(d => d.AccountId == accountId).ToList());

        public Task<int> CountByAccountAsync(Guid accountId, CancellationToken cancellationToken) =>
            Task.FromResult(Documents.Count(d => d.AccountId == accountId));

        public void Add(OwnerApplicationDocument document) => Documents.Add(document);
        public void Remove(OwnerApplicationDocument document) => Documents.Remove(document);
        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class FakeDocStore : IOwnerDocumentStore
    {
        public Dictionary<string, byte[]> StoredFiles { get; } = [];

        public async Task<string> SaveAsync(Stream stream, string storageKey, CancellationToken cancellationToken)
        {
            using var ms = new MemoryStream();
            await stream.CopyToAsync(ms, cancellationToken);
            StoredFiles[storageKey] = ms.ToArray();
            return storageKey;
        }

        public Task<Stream?> OpenReadAsync(string storageKey, CancellationToken cancellationToken)
        {
            if (StoredFiles.TryGetValue(storageKey, out var bytes))
                return Task.FromResult<Stream?>(new MemoryStream(bytes));
            return Task.FromResult<Stream?>(null);
        }

        public Task<bool> DeleteAsync(string storageKey, CancellationToken cancellationToken)
        {
            var removed = StoredFiles.Remove(storageKey);
            return Task.FromResult(removed);
        }
    }
}
