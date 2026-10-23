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

        var handler = new DeleteOwnerDocumentCommandHandler(accounts, docs, store, CreateRequirements());

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

        var handler = new DeleteOwnerDocumentCommandHandler(accounts, docs, store, CreateRequirements());

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

    // Concurrency: uploads, deletes and the admin decision for one application queue up behind the account row lock.
    // The lock has to be the first read: an earlier plain read would load the entity and a later locking read would return the stale copy.
    [Fact]
    public async Task Upload_takes_the_account_row_lock_and_never_reads_the_account_without_it()
    {
        var accounts = new FakeAccountRepo(IndividualApplication());
        var handler = new UploadOwnerDocumentCommandHandler(accounts, new FakeDocRepo(), new FakeDocStore(), CreateRequirements(), new FixedClock(Now));

        var result = await UploadPdf(handler, "PremisesRightProof");

        Assert.True(result.IsSuccess, result.Error?.Code);
        Assert.Equal([nameof(IAccountRepository.FindByIdForUpdateAsync)], accounts.Calls);
    }

    [Fact]
    public async Task Delete_takes_the_account_row_lock_and_never_reads_the_account_without_it()
    {
        var accounts = new FakeAccountRepo(IndividualApplication());
        var docs = new FakeDocRepo();
        docs.Documents.Add(StoredDocument("PremisesRightProof", "doc_key.dat"));
        var handler = new DeleteOwnerDocumentCommandHandler(accounts, docs, new FakeDocStore(), CreateRequirements());

        var result = await handler.Handle(new DeleteOwnerDocumentCommand(AccountId, "PremisesRightProof"), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess, result.Error?.Code);
        Assert.Equal([nameof(IAccountRepository.FindByIdForUpdateAsync)], accounts.Calls);
    }

    // Orphan files: the file is written before the row, so every failure after the write has to take it back.
    [Fact]
    public async Task Upload_leaves_no_file_behind_when_the_database_save_fails()
    {
        var docs = new FakeDocRepo { FailSaveWith = new InvalidOperationException("The database is unavailable.") };
        var store = new FakeDocStore();
        var handler = new UploadOwnerDocumentCommandHandler(new FakeAccountRepo(IndividualApplication()), docs, store, CreateRequirements(), new FixedClock(Now));

        await Assert.ThrowsAsync<InvalidOperationException>(() => UploadPdf(handler, "PremisesRightProof"));

        Assert.Empty(store.StoredFiles);
    }

    [Fact]
    public async Task Replacing_keeps_the_old_file_and_removes_the_new_one_when_the_database_save_fails()
    {
        var docs = new FakeDocRepo { FailSaveWith = new InvalidOperationException("The database is unavailable.") };
        docs.Documents.Add(StoredDocument("PremisesRightProof", "old_key.dat"));
        var store = new FakeDocStore();
        store.StoredFiles["old_key.dat"] = [1, 2, 3];
        var handler = new UploadOwnerDocumentCommandHandler(new FakeAccountRepo(IndividualApplication()), docs, store, CreateRequirements(), new FixedClock(Now));

        await Assert.ThrowsAsync<InvalidOperationException>(() => UploadPdf(handler, "PremisesRightProof"));

        Assert.Equal(["old_key.dat"], store.StoredFiles.Keys);
    }

    [Fact]
    public async Task Upload_removes_the_new_file_and_keeps_the_old_one_when_the_commit_fails()
    {
        var accounts = new FakeAccountRepo(IndividualApplication()) { FailCommitWith = new InvalidOperationException("The commit failed.") };
        var docs = new FakeDocRepo();
        docs.Documents.Add(StoredDocument("PremisesRightProof", "old_key.dat"));
        var store = new FakeDocStore();
        store.StoredFiles["old_key.dat"] = [1, 2, 3];
        var handler = new UploadOwnerDocumentCommandHandler(accounts, docs, store, CreateRequirements(), new FixedClock(Now));

        await Assert.ThrowsAsync<InvalidOperationException>(() => UploadPdf(handler, "PremisesRightProof"));

        Assert.Equal(["old_key.dat"], store.StoredFiles.Keys);
    }

    [Fact]
    public async Task Upload_deletes_the_replaced_file_only_after_the_commit()
    {
        var timeline = new List<string>();
        var docs = new FakeDocRepo();
        docs.Documents.Add(StoredDocument("PremisesRightProof", "old_key.dat"));
        var store = new FakeDocStore(timeline);
        store.StoredFiles["old_key.dat"] = [1, 2, 3];
        var handler = new UploadOwnerDocumentCommandHandler(new FakeAccountRepo(IndividualApplication(), timeline), docs, store, CreateRequirements(), new FixedClock(Now));

        var result = await UploadPdf(handler, "PremisesRightProof");

        Assert.True(result.IsSuccess, result.Error?.Code);
        Assert.Collection(
            timeline,
            entry => Assert.StartsWith("save:", entry),
            entry => Assert.Equal("commit", entry),
            entry => Assert.Equal("delete:old_key.dat", entry));
    }

    [Fact]
    public async Task Delete_removes_the_file_only_after_the_commit()
    {
        var timeline = new List<string>();
        var docs = new FakeDocRepo();
        docs.Documents.Add(StoredDocument("PremisesRightProof", "doc_key.dat"));
        var store = new FakeDocStore(timeline);
        store.StoredFiles["doc_key.dat"] = ValidPdfBytes;
        var handler = new DeleteOwnerDocumentCommandHandler(new FakeAccountRepo(IndividualApplication(), timeline), docs, store, CreateRequirements());

        var result = await handler.Handle(new DeleteOwnerDocumentCommand(AccountId, "PremisesRightProof"), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess, result.Error?.Code);
        Assert.Equal(["commit", "delete:doc_key.dat"], timeline);
    }

    [Fact]
    public async Task Delete_keeps_the_file_when_the_commit_fails()
    {
        var accounts = new FakeAccountRepo(IndividualApplication()) { FailCommitWith = new InvalidOperationException("The commit failed.") };
        var docs = new FakeDocRepo();
        docs.Documents.Add(StoredDocument("PremisesRightProof", "doc_key.dat"));
        var store = new FakeDocStore();
        store.StoredFiles["doc_key.dat"] = ValidPdfBytes;
        var handler = new DeleteOwnerDocumentCommandHandler(accounts, docs, store, CreateRequirements());

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.Handle(new DeleteOwnerDocumentCommand(AccountId, "PremisesRightProof"), TestContext.Current.CancellationToken));

        Assert.Equal(["doc_key.dat"], store.StoredFiles.Keys);
    }

    // The row is the source of truth: once it is gone, a file that cannot be removed is only garbage for the retention cleanup.
    [Fact]
    public async Task Delete_still_succeeds_when_the_file_cannot_be_removed()
    {
        var docs = new FakeDocRepo();
        docs.Documents.Add(StoredDocument("PremisesRightProof", "doc_key.dat"));
        var store = new FakeDocStore { FailDeleteWith = new IOException("The file is locked.") };
        var handler = new DeleteOwnerDocumentCommandHandler(new FakeAccountRepo(IndividualApplication()), docs, store, CreateRequirements());

        var result = await handler.Handle(new DeleteOwnerDocumentCommand(AccountId, "PremisesRightProof"), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess, result.Error?.Code);
        Assert.Empty(docs.Documents);
    }

    // F8: the type is stored and returned in the checklist's own spelling, so one type is one document whatever the casing.
    [Fact]
    public async Task Upload_stores_and_returns_the_canonical_document_type()
    {
        var docs = new FakeDocRepo();
        var handler = new UploadOwnerDocumentCommandHandler(new FakeAccountRepo(IndividualApplication()), docs, new FakeDocStore(), CreateRequirements(), new FixedClock(Now));

        var result = await UploadPdf(handler, "premisesrightproof");

        Assert.True(result.IsSuccess, result.Error?.Code);
        Assert.Equal("PremisesRightProof", result.Value!.DocumentType);
        Assert.Equal("PremisesRightProof", Assert.Single(docs.Documents).DocumentType);
    }

    [Fact]
    public async Task Uploading_one_type_in_two_casings_keeps_one_document_and_one_file()
    {
        var docs = new FakeDocRepo();
        var store = new FakeDocStore();
        var handler = new UploadOwnerDocumentCommandHandler(new FakeAccountRepo(IndividualApplication()), docs, store, CreateRequirements(), new FixedClock(Now));

        var first = await UploadPdf(handler, "PremisesRightProof");
        var second = await UploadPdf(handler, "premisesrightproof");

        Assert.True(first.IsSuccess, first.Error?.Code);
        Assert.True(second.IsSuccess, second.Error?.Code);
        Assert.Single(docs.Documents);
        Assert.Single(store.StoredFiles);
        Assert.Equal(first.Value!.Id, second.Value!.Id);
    }

    [Fact]
    public async Task Delete_finds_the_document_whatever_the_casing_of_the_type()
    {
        var docs = new FakeDocRepo();
        docs.Documents.Add(StoredDocument("PremisesRightProof", "doc_key.dat"));
        var store = new FakeDocStore();
        store.StoredFiles["doc_key.dat"] = ValidPdfBytes;
        var handler = new DeleteOwnerDocumentCommandHandler(new FakeAccountRepo(IndividualApplication()), docs, store, CreateRequirements());

        var result = await handler.Handle(new DeleteOwnerDocumentCommand(AccountId, "premisesrightproof"), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess, result.Error?.Code);
        Assert.Empty(docs.Documents);
        Assert.Empty(store.StoredFiles);
    }

    // The owner type can change after an upload; the document stays (it is not deleted for the owner) and must stay deletable.
    [Fact]
    public async Task Delete_can_remove_a_document_that_is_not_on_the_current_checklist()
    {
        var docs = new FakeDocRepo();
        docs.Documents.Add(StoredDocument("EnterpriseRegistration", "doc_key.dat"));
        var handler = new DeleteOwnerDocumentCommandHandler(new FakeAccountRepo(IndividualApplication()), docs, new FakeDocStore(), CreateRequirements());

        var result = await handler.Handle(new DeleteOwnerDocumentCommand(AccountId, "enterpriseregistration"), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess, result.Error?.Code);
        Assert.Empty(docs.Documents);
    }

    // F3: an owner type that is not one of the enum values must never be stored or used.
    [Theory]
    [InlineData(0)]
    [InlineData(99)]
    public async Task UpdateOwnerType_rejects_a_value_that_is_not_an_owner_type(int value)
    {
        var account = IndividualApplication();
        var handler = new UpdateOwnerTypeCommandHandler(new FakeAccountRepo(account));

        var result = await handler.Handle(new UpdateOwnerTypeCommand(AccountId, (OwnerType)value), TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(OwnerApplicationErrors.OwnerTypeInvalid.Code, result.Error!.Code);
        Assert.Equal(OwnerType.Individual, account.OwnerType);
    }

    [Fact]
    public async Task Resubmit_rejects_an_owner_type_in_the_request_that_is_not_defined_and_stays_rejected()
    {
        var account = RejectedIndividualApplication();
        var docs = new FakeDocRepo();
        docs.Documents.Add(StoredDocument("PremisesRightProof", "doc_key.dat"));
        var handler = new ResubmitOwnerApplicationCommandHandler(new FakeAccountRepo(account), docs, CreateRequirements(), new FixedClock(Now));

        var result = await handler.Handle(
            new ResubmitOwnerApplicationCommand(AccountId, OwnerType: (OwnerType)99), TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(OwnerApplicationErrors.OwnerTypeInvalid.Code, result.Error!.Code);
        Assert.Equal(OwnerApplicationStatus.Rejected, account.OwnerApplicationStatus);
    }

    // Corrupted data: a stored owner type that is not defined used to give an empty checklist, so resubmit needed no document.
    [Fact]
    public async Task Resubmit_cannot_skip_the_checklist_when_the_stored_owner_type_is_not_defined()
    {
        var account = RejectedIndividualApplication();
        typeof(Account).GetProperty(nameof(Account.OwnerType))!.SetValue(account, (OwnerType)99);
        var handler = new ResubmitOwnerApplicationCommandHandler(new FakeAccountRepo(account), new FakeDocRepo(), CreateRequirements(), new FixedClock(Now));

        var result = await handler.Handle(new ResubmitOwnerApplicationCommand(AccountId), TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(OwnerApplicationErrors.OwnerTypeInvalid.Code, result.Error!.Code);
        Assert.Equal(OwnerApplicationStatus.Rejected, account.OwnerApplicationStatus);
    }

    private static Account RejectedIndividualApplication()
    {
        var account = IndividualApplication();
        account.RejectOwnerApplication(Guid.NewGuid(), "Thiếu giấy tờ.", Now);
        return account;
    }

    private static Account IndividualApplication()
    {
        var account = Account.Register(AccountId, "owner@example.com", "hash", null, Now, applyAsParkingLotOwner: true);
        account.SetOwnerType(OwnerType.Individual);
        return account;
    }

    private static OwnerApplicationDocument StoredDocument(string documentType, string storageKey) =>
        OwnerApplicationDocument.Create(
            Guid.NewGuid(), AccountId, documentType, "old.pdf", "application/pdf", 3, new string('0', 64), storageKey, Now.AddDays(-1));

    private static Task<Result<OwnerDocumentDto>> UploadPdf(UploadOwnerDocumentCommandHandler handler, string documentType) =>
        handler.Handle(
            new UploadOwnerDocumentCommand(AccountId, documentType, "mat_bang.pdf", new MemoryStream(ValidPdfBytes), ValidPdfBytes.Length),
            TestContext.Current.CancellationToken);

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

    private sealed class FakeAccountRepo(Account? account, List<string>? timeline = null) : IAccountRepository
    {
        public List<string> Calls { get; } = [];

        // Simulates the COMMIT failing after the action, and its SaveChanges, already succeeded.
        public Exception? FailCommitWith { get; set; }

        public Task<Account?> FindByIdAsync(Guid id, CancellationToken cancellationToken)
        {
            Calls.Add(nameof(FindByIdAsync));
            return Task.FromResult(Find(id));
        }

        public Task<Account?> FindByIdForUpdateAsync(Guid id, CancellationToken cancellationToken)
        {
            Calls.Add(nameof(FindByIdForUpdateAsync));
            return Task.FromResult(Find(id));
        }

        private Account? Find(Guid id) => account is not null && account.Id == id ? account : null;

        public Task<Account?> FindByEmailAsync(string normalizedEmail, CancellationToken cancellationToken) => Task.FromResult<Account?>(null);
        public Task<Account?> FindByEmailForUpdateAsync(string normalizedEmail, CancellationToken cancellationToken) => Task.FromResult<Account?>(null);
        public Task<bool> EmailExistsAsync(string normalizedEmail, CancellationToken cancellationToken) => Task.FromResult(false);
        public void Add(Account a) { }
        public Task<SaveAccountResult> SaveNewAccountAsync(CancellationToken cancellationToken) => Task.FromResult(SaveAccountResult.Success);
        public Task<bool> TrySaveChangesAsync(CancellationToken cancellationToken) => Task.FromResult(true);
        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public async Task<TResult> ExecuteInTransactionAsync<TResult>(Func<Task<TResult>> action, CancellationToken cancellationToken)
        {
            var result = await action();
            if (FailCommitWith is not null)
                throw FailCommitWith;
            timeline?.Add("commit");
            return result;
        }
    }

    private sealed class FakeDocRepo : IOwnerDocumentRepository
    {
        public List<OwnerApplicationDocument> Documents { get; } = [];

        public Exception? FailSaveWith { get; set; }

        public Task<OwnerApplicationDocument?> FindByAccountAndTypeAsync(Guid accountId, string documentType, CancellationToken cancellationToken) =>
            Task.FromResult(Documents.FirstOrDefault(d => d.AccountId == accountId && d.DocumentType == documentType));

        public Task<IReadOnlyList<OwnerApplicationDocument>> ListByAccountAsync(Guid accountId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<OwnerApplicationDocument>>(Documents.Where(d => d.AccountId == accountId).ToList());

        public Task<int> CountByAccountAsync(Guid accountId, CancellationToken cancellationToken) =>
            Task.FromResult(Documents.Count(d => d.AccountId == accountId));

        public void Add(OwnerApplicationDocument document) => Documents.Add(document);
        public void Remove(OwnerApplicationDocument document) => Documents.Remove(document);
        public Task SaveChangesAsync(CancellationToken cancellationToken) =>
            FailSaveWith is null ? Task.CompletedTask : Task.FromException(FailSaveWith);
    }

    private sealed class FakeDocStore(List<string>? timeline = null) : IOwnerDocumentStore
    {
        public Dictionary<string, byte[]> StoredFiles { get; } = [];

        public Exception? FailDeleteWith { get; set; }

        public async Task<string> SaveAsync(Stream stream, string storageKey, CancellationToken cancellationToken)
        {
            using var ms = new MemoryStream();
            await stream.CopyToAsync(ms, cancellationToken);
            StoredFiles[storageKey] = ms.ToArray();
            timeline?.Add($"save:{storageKey}");
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
            if (FailDeleteWith is not null)
                throw FailDeleteWith;
            timeline?.Add($"delete:{storageKey}");
            var removed = StoredFiles.Remove(storageKey);
            return Task.FromResult(removed);
        }
    }
}
