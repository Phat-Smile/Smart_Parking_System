using System.Text;
using Microsoft.Extensions.Options;
using ParkingSystem.Identity.Application.Accounts;
using ParkingSystem.Identity.Application.Accounts.OwnerApplications;
using ParkingSystem.Identity.Domain.Accounts;
using ParkingSystem.Identity.Infrastructure.OwnerDocuments;
using Xunit;

namespace ParkingSystem.Tests.Identity.UnitTests.Accounts.OwnerDocuments;

// What the admin sees of an application's documents: the list with the checklist verdict, and one file. Both are scoped to the
// application they are asked for, and neither exposes a storage key or a path.
public sealed class OwnerApplicationReviewQueryTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 24, 9, 0, 0, TimeSpan.Zero);
    private static readonly Guid AccountId = Guid.NewGuid();
    private static readonly byte[] PdfBytes = Encoding.ASCII.GetBytes("%PDF-1.4\n1 0 obj\n<<>>\nendobj");

    [Fact]
    public async Task List_for_an_unknown_account_returns_ApplicationNotFound()
    {
        var handler = ListHandler(null, new FakeDocRepo());

        var result = await handler.Handle(new GetOwnerApplicationDocumentsQuery(AccountId), TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(OwnerApplicationErrors.ApplicationNotFound.Code, result.Error!.Code);
    }

    [Fact]
    public async Task List_for_an_account_that_never_applied_returns_ApplicationNotFound()
    {
        var customer = Account.Register(AccountId, "customer@example.com", "hash", null, Now);
        var handler = ListHandler(customer, new FakeDocRepo());

        var result = await handler.Handle(new GetOwnerApplicationDocumentsQuery(AccountId), TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(OwnerApplicationErrors.ApplicationNotFound.Code, result.Error!.Code);
    }

    [Fact]
    public async Task List_of_a_complete_application_reports_complete_with_every_document()
    {
        var account = Application(OwnerType.Individual);
        var documents = new FakeDocRepo();
        var proof = documents.Add(Document(AccountId, "PremisesRightProof", "mat_bang.pdf"));
        var letter = documents.Add(Document(AccountId, "AuthorizationLetter", "uy_quyen.pdf"));
        var handler = ListHandler(account, documents);

        var result = await handler.Handle(new GetOwnerApplicationDocumentsQuery(AccountId), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess, result.Error?.Code);
        var dto = result.Value!;
        Assert.Equal(AccountId, dto.AccountId);
        Assert.Equal("Pending", dto.Status);
        Assert.Equal("Individual", dto.OwnerType);
        Assert.True(dto.IsComplete);
        Assert.Empty(dto.MissingDocumentTypes);
        Assert.Equal([proof.Id, letter.Id], dto.Documents.Select(document => document.Id));
        var listed = dto.Documents[0];
        Assert.Equal("PremisesRightProof", listed.DocumentType);
        Assert.Equal("mat_bang.pdf", listed.FileName);
        Assert.Equal("application/pdf", listed.ContentType);
        Assert.Equal(PdfBytes.Length, listed.SizeBytes);
        Assert.Equal(proof.UploadedAtUtc, listed.UploadedAtUtc);
    }

    [Fact]
    public async Task List_names_the_required_documents_that_are_still_missing()
    {
        var account = Application(OwnerType.HouseholdBusiness);
        var documents = new FakeDocRepo();
        documents.Add(Document(AccountId, "PremisesRightProof", "mat_bang.pdf"));
        var handler = ListHandler(account, documents);

        var result = await handler.Handle(new GetOwnerApplicationDocumentsQuery(AccountId), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess, result.Error?.Code);
        Assert.False(result.Value!.IsComplete);
        Assert.Equal(["HouseholdBusinessRegistration"], result.Value.MissingDocumentTypes);
        Assert.Single(result.Value.Documents);
    }

    [Fact]
    public async Task List_without_an_owner_type_is_never_complete_and_names_no_missing_type()
    {
        var account = Account.Register(AccountId, "owner@example.com", "hash", null, Now, applyAsParkingLotOwner: true);
        var handler = ListHandler(account, new FakeDocRepo());

        var result = await handler.Handle(new GetOwnerApplicationDocumentsQuery(AccountId), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess, result.Error?.Code);
        Assert.Null(result.Value!.OwnerType);
        Assert.False(result.Value.IsComplete);
        Assert.Empty(result.Value.MissingDocumentTypes);
        Assert.Empty(result.Value.Documents);
    }

    [Fact]
    public async Task List_never_includes_the_documents_of_another_account()
    {
        var account = Application(OwnerType.Individual);
        var documents = new FakeDocRepo();
        documents.Add(Document(Guid.NewGuid(), "PremisesRightProof", "cua_nguoi_khac.pdf"));
        var handler = ListHandler(account, documents);

        var result = await handler.Handle(new GetOwnerApplicationDocumentsQuery(AccountId), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess, result.Error?.Code);
        Assert.Empty(result.Value!.Documents);
        Assert.False(result.Value.IsComplete);
        Assert.Equal(["PremisesRightProof"], result.Value.MissingDocumentTypes);
    }

    [Theory]
    [InlineData("Approved")]
    [InlineData("Rejected")]
    public async Task List_is_available_for_decided_applications_too(string status)
    {
        var account = Application(OwnerType.Individual);
        var documents = new FakeDocRepo();
        documents.Add(Document(AccountId, "PremisesRightProof", "mat_bang.pdf"));
        if (status == "Approved")
            account.ApproveOwnerApplication(Guid.NewGuid(), Now);
        else
            account.RejectOwnerApplication(Guid.NewGuid(), "Giấy tờ mờ.", Now);
        var handler = ListHandler(account, documents);

        var result = await handler.Handle(new GetOwnerApplicationDocumentsQuery(AccountId), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess, result.Error?.Code);
        Assert.Equal(status, result.Value!.Status);
        Assert.Single(result.Value.Documents);
    }

    [Fact]
    public async Task Content_for_an_unknown_account_returns_ApplicationNotFound_and_opens_nothing()
    {
        var store = new FakeDocStore();
        var handler = ContentHandler(null, new FakeDocRepo(), store);

        var result = await handler.Handle(new GetOwnerApplicationDocumentContentQuery(AccountId, "PremisesRightProof"), TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(OwnerApplicationErrors.ApplicationNotFound.Code, result.Error!.Code);
        Assert.Equal(0, store.OpenCount);
    }

    [Fact]
    public async Task Content_for_an_account_that_never_applied_returns_ApplicationNotFound_and_opens_nothing()
    {
        var customer = Account.Register(AccountId, "customer@example.com", "hash", null, Now);
        var store = new FakeDocStore();
        var handler = ContentHandler(customer, new FakeDocRepo(), store);

        var result = await handler.Handle(new GetOwnerApplicationDocumentContentQuery(AccountId, "PremisesRightProof"), TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(OwnerApplicationErrors.ApplicationNotFound.Code, result.Error!.Code);
        Assert.Equal(0, store.OpenCount);
    }

    // The type comes from the URL. Anything that is not on a checklist never reaches the repository or the store.
    [Theory]
    [InlineData("Nope")]
    [InlineData("../../etc/passwd")]
    [InlineData("..\\..\\appsettings.json")]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Content_for_a_type_that_is_not_on_any_checklist_returns_DocumentNotFound_and_opens_nothing(string documentType)
    {
        var account = Application(OwnerType.Individual);
        var documents = new FakeDocRepo();
        documents.Add(Document(AccountId, "PremisesRightProof", "mat_bang.pdf"));
        var store = new FakeDocStore();
        var handler = ContentHandler(account, documents, store);

        var result = await handler.Handle(new GetOwnerApplicationDocumentContentQuery(AccountId, documentType), TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(OwnerApplicationErrors.DocumentNotFound.Code, result.Error!.Code);
        Assert.Equal(0, store.OpenCount);
    }

    [Fact]
    public async Task Content_for_a_type_that_was_not_uploaded_returns_DocumentNotFound_and_opens_nothing()
    {
        var account = Application(OwnerType.Individual);
        var store = new FakeDocStore();
        var handler = ContentHandler(account, new FakeDocRepo(), store);

        var result = await handler.Handle(new GetOwnerApplicationDocumentContentQuery(AccountId, "PremisesRightProof"), TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(OwnerApplicationErrors.DocumentNotFound.Code, result.Error!.Code);
        Assert.Equal(0, store.OpenCount);
    }

    [Theory]
    [InlineData("PremisesRightProof")]
    [InlineData("premisesrightproof")]
    [InlineData("  PREMISESRIGHTPROOF  ")]
    public async Task Content_returns_the_stored_bytes_with_the_type_and_name_of_the_row(string documentType)
    {
        var account = Application(OwnerType.Individual);
        var documents = new FakeDocRepo();
        var row = documents.Add(Document(AccountId, "PremisesRightProof", "mat_bang.pdf"));
        var store = new FakeDocStore();
        store.Files[row.StorageKey] = PdfBytes;
        var handler = ContentHandler(account, documents, store);

        var result = await handler.Handle(new GetOwnerApplicationDocumentContentQuery(AccountId, documentType), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess, result.Error?.Code);
        await using var content = result.Value!.Content;
        using var copy = new MemoryStream();
        await content.CopyToAsync(copy, TestContext.Current.CancellationToken);
        Assert.Equal(PdfBytes, copy.ToArray());
        Assert.Equal("PremisesRightProof", result.Value.DocumentType);
        Assert.Equal("application/pdf", result.Value.ContentType);
        Assert.Equal("mat_bang.pdf", result.Value.FileName);
        Assert.Equal(PdfBytes.Length, result.Value.SizeBytes);
        Assert.Equal([row.StorageKey], store.Opened);
    }

    // A document kept after the owner type changed is outside the new checklist but is still the applicant's, and the admin must see it.
    [Fact]
    public async Task Content_finds_a_document_that_is_outside_the_current_checklist()
    {
        var account = Application(OwnerType.HouseholdBusiness);
        var documents = new FakeDocRepo();
        var row = documents.Add(Document(AccountId, "EnterpriseRegistration", "gpdk.pdf"));
        var store = new FakeDocStore();
        store.Files[row.StorageKey] = PdfBytes;
        var handler = ContentHandler(account, documents, store);

        var result = await handler.Handle(new GetOwnerApplicationDocumentContentQuery(AccountId, "EnterpriseRegistration"), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess, result.Error?.Code);
        await result.Value!.Content.DisposeAsync();
        Assert.Equal("gpdk.pdf", result.Value.FileName);
    }

    [Fact]
    public async Task Content_never_reaches_the_document_of_another_account()
    {
        var account = Application(OwnerType.Individual);
        var documents = new FakeDocRepo();
        var other = documents.Add(Document(Guid.NewGuid(), "PremisesRightProof", "cua_nguoi_khac.pdf"));
        var store = new FakeDocStore();
        store.Files[other.StorageKey] = PdfBytes;
        var handler = ContentHandler(account, documents, store);

        var result = await handler.Handle(new GetOwnerApplicationDocumentContentQuery(AccountId, "PremisesRightProof"), TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(OwnerApplicationErrors.DocumentNotFound.Code, result.Error!.Code);
        Assert.Equal(0, store.OpenCount);
    }

    [Fact]
    public async Task Content_of_a_purged_document_returns_DocumentFileUnavailable_and_opens_nothing()
    {
        var account = Application(OwnerType.Individual);
        var documents = new FakeDocRepo();
        var row = documents.Add(Document(AccountId, "PremisesRightProof", "mat_bang.pdf"));
        typeof(OwnerApplicationDocument).GetProperty(nameof(OwnerApplicationDocument.PurgedAtUtc))!.SetValue(row, Now);
        var store = new FakeDocStore();
        store.Files[row.StorageKey] = PdfBytes;
        var handler = ContentHandler(account, documents, store);

        var result = await handler.Handle(new GetOwnerApplicationDocumentContentQuery(AccountId, "PremisesRightProof"), TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(OwnerApplicationErrors.DocumentFileUnavailable.Code, result.Error!.Code);
        Assert.Equal(0, store.OpenCount);
    }

    [Fact]
    public async Task Content_whose_file_is_missing_from_the_store_returns_DocumentFileUnavailable()
    {
        var account = Application(OwnerType.Individual);
        var documents = new FakeDocRepo();
        documents.Add(Document(AccountId, "PremisesRightProof", "mat_bang.pdf"));
        var handler = ContentHandler(account, documents, new FakeDocStore());

        var result = await handler.Handle(new GetOwnerApplicationDocumentContentQuery(AccountId, "PremisesRightProof"), TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(OwnerApplicationErrors.DocumentFileUnavailable.Code, result.Error!.Code);
    }

    private static Account Application(OwnerType ownerType)
    {
        var account = Account.Register(AccountId, "owner@example.com", "hash", null, Now, applyAsParkingLotOwner: true);
        account.SetOwnerType(ownerType);
        return account;
    }

    private static OwnerApplicationDocument Document(Guid accountId, string documentType, string fileName) =>
        OwnerApplicationDocument.Create(
            Guid.NewGuid(), accountId, documentType, fileName, "application/pdf", PdfBytes.Length,
            "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855", Guid.NewGuid().ToString("N"), Now);

    private static IOwnerDocumentRequirements Requirements() =>
        new ConfigurationOwnerDocumentRequirements(Options.Create(new OwnerDocumentsOptions()));

    private static GetOwnerApplicationDocumentsQueryHandler ListHandler(Account? account, FakeDocRepo documents) =>
        new(new FakeAccountRepo(account), documents, Requirements());

    private static GetOwnerApplicationDocumentContentQueryHandler ContentHandler(Account? account, FakeDocRepo documents, FakeDocStore store) =>
        new(new FakeAccountRepo(account), documents, store, Requirements());

    private sealed class FakeAccountRepo(Account? account) : IAccountRepository
    {
        public Task<Account?> FindByIdAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult(account is not null && account.Id == id ? account : null);

        public Task<Account?> FindByIdForUpdateAsync(Guid id, CancellationToken cancellationToken) => FindByIdAsync(id, cancellationToken);
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
        private readonly List<OwnerApplicationDocument> _documents = [];

        public OwnerApplicationDocument Add(OwnerApplicationDocument document)
        {
            _documents.Add(document);
            return document;
        }

        void IOwnerDocumentRepository.Add(OwnerApplicationDocument document) => _documents.Add(document);
        public void Remove(OwnerApplicationDocument document) => _documents.Remove(document);

        public Task<OwnerApplicationDocument?> FindByAccountAndTypeAsync(Guid accountId, string documentType, CancellationToken cancellationToken) =>
            Task.FromResult(_documents.FirstOrDefault(d => d.AccountId == accountId && d.DocumentType == documentType));

        public Task<IReadOnlyList<OwnerApplicationDocument>> ListByAccountAsync(Guid accountId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<OwnerApplicationDocument>>(_documents.Where(d => d.AccountId == accountId).ToList());

        public Task<int> CountByAccountAsync(Guid accountId, CancellationToken cancellationToken) =>
            Task.FromResult(_documents.Count(d => d.AccountId == accountId));

        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class FakeDocStore : IOwnerDocumentStore
    {
        public Dictionary<string, byte[]> Files { get; } = [];

        public List<string> Opened { get; } = [];

        public int OpenCount => Opened.Count;

        public Task<string> SaveAsync(Stream stream, string storageKey, CancellationToken cancellationToken) =>
            throw new NotSupportedException("A review never writes.");

        public Task<Stream?> OpenReadAsync(string storageKey, CancellationToken cancellationToken)
        {
            Opened.Add(storageKey);
            return Task.FromResult<Stream?>(Files.TryGetValue(storageKey, out var bytes) ? new MemoryStream(bytes) : null);
        }

        public Task<bool> DeleteAsync(string storageKey, CancellationToken cancellationToken) =>
            throw new NotSupportedException("A review never deletes.");
    }
}
