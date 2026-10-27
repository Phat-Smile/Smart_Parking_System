using Microsoft.Extensions.Options;
using ParkingSystem.BuildingBlocks.Results;
using ParkingSystem.BuildingBlocks.Time;
using ParkingSystem.Contracts.Events.Identity;
using ParkingSystem.Identity.Application.Accounts;
using ParkingSystem.Identity.Application.Accounts.OwnerApplications;
using ParkingSystem.Identity.Application.Common.Messaging.Outbox;
using ParkingSystem.Identity.Domain.Accounts;
using ParkingSystem.Identity.Infrastructure.OwnerDocuments;
using Xunit;

namespace ParkingSystem.Tests.Identity.UnitTests;

public sealed class OwnerApplicationHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 20, 10, 0, 0, TimeSpan.Zero);
    private static readonly Guid AdminId = Guid.NewGuid();
    private static readonly Guid OwnerId = Guid.NewGuid();

    [Fact]
    public async Task Approve_unknown_account_returns_ApplicationNotFound()
    {
        var accounts = new FakeAccountRepository(null);
        var sessions = new FakeSessionRepository();
        var refreshTokens = new FakeRefreshTokenRepository();
        var outbox = new FakeOutboxWriter();
        var handler = ApproveHandler(accounts, sessions, refreshTokens, new FakeOwnerDocumentRepository(), outbox);

        var result = await handler.Handle(new ApproveOwnerApplicationCommand(AdminId, OwnerId), TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(OwnerApplicationErrors.ApplicationNotFound.Code, result.Error!.Code);
        Assert.Empty(outbox.EnqueuedEvents);
    }

    [Fact]
    public async Task Approve_own_application_returns_CannotApproveOwnApplication()
    {
        var account = Account.Register(AdminId, "admin@example.com", "hash", null, Now, applyAsParkingLotOwner: true);
        var accounts = new FakeAccountRepository(account);
        var sessions = new FakeSessionRepository();
        var refreshTokens = new FakeRefreshTokenRepository();
        var outbox = new FakeOutboxWriter();
        var handler = ApproveHandler(accounts, sessions, refreshTokens, new FakeOwnerDocumentRepository(), outbox);

        var result = await handler.Handle(new ApproveOwnerApplicationCommand(AdminId, AdminId), TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(OwnerApplicationErrors.CannotApproveOwnApplication.Code, result.Error!.Code);
        Assert.Empty(outbox.EnqueuedEvents);
    }

    [Fact]
    public async Task Approve_non_pending_application_returns_NotPending()
    {
        var account = Account.Register(OwnerId, "customer@example.com", "hash", null, Now, applyAsParkingLotOwner: true);
        account.ApproveOwnerApplication(Guid.NewGuid(), Now);
        var accounts = new FakeAccountRepository(account);
        var sessions = new FakeSessionRepository();
        var refreshTokens = new FakeRefreshTokenRepository();
        var outbox = new FakeOutboxWriter();
        var handler = ApproveHandler(accounts, sessions, refreshTokens, new FakeOwnerDocumentRepository(), outbox);

        var result = await handler.Handle(new ApproveOwnerApplicationCommand(AdminId, OwnerId), TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(OwnerApplicationErrors.NotPending.Code, result.Error!.Code);
        Assert.Empty(outbox.EnqueuedEvents);
    }

    [Fact]
    public async Task Approve_pending_application_succeeds_and_revokes_sessions()
    {
        var account = Account.Register(OwnerId, "owner@example.com", "hash", null, Now, applyAsParkingLotOwner: true);
        account.SetOwnerType(OwnerType.Individual);
        var accounts = new FakeAccountRepository(account);
        var sessions = new FakeSessionRepository();
        var refreshTokens = new FakeRefreshTokenRepository();
        var outbox = new FakeOutboxWriter();
        var documents = new FakeOwnerDocumentRepository();
        documents.Add(Document(OwnerId, "PremisesRightProof"));
        var handler = ApproveHandler(accounts, sessions, refreshTokens, documents, outbox);

        var result = await handler.Handle(new ApproveOwnerApplicationCommand(AdminId, OwnerId), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(OwnerId, result.Value!.AccountId);
        Assert.Equal("Approved", result.Value.Status);
        Assert.Equal(AccountRole.ParkingLotOwner, account.Role);
        Assert.Equal(OwnerApplicationStatus.Approved, account.OwnerApplicationStatus);
        Assert.True(sessions.RevokedAll);
        Assert.True(refreshTokens.RevokedAll);

        Assert.Single(outbox.EnqueuedEvents);
        var enqueued = outbox.EnqueuedEvents[0];
        Assert.Equal("identity.owner-application-approved.v1", enqueued.EventType);
        Assert.Equal(1, enqueued.Version);
        var evt = Assert.IsType<OwnerApplicationApprovedIntegrationEvent>(enqueued.Event);
        Assert.Equal(OwnerId, evt.AccountId);
        Assert.Equal("owner@example.com", evt.Email);
        Assert.Equal(Now, evt.ApprovedAtUtc);
    }

    // The role is granted only for a complete checklist of the owner type the applicant chose (AGENTS 5.1). Every refusal leaves
    // the application Pending, the sessions alive and the outbox empty.
    [Fact]
    public async Task Approve_without_an_owner_type_returns_OwnerTypeRequired_and_grants_nothing()
    {
        var account = Account.Register(OwnerId, "owner@example.com", "hash", null, Now, applyAsParkingLotOwner: true);
        var sessions = new FakeSessionRepository();
        var refreshTokens = new FakeRefreshTokenRepository();
        var outbox = new FakeOutboxWriter();
        var handler = ApproveHandler(new FakeAccountRepository(account), sessions, refreshTokens, new FakeOwnerDocumentRepository(), outbox);

        var result = await handler.Handle(new ApproveOwnerApplicationCommand(AdminId, OwnerId), TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(OwnerApplicationErrors.OwnerTypeRequired.Code, result.Error!.Code);
        AssertNothingGranted(account, sessions, refreshTokens, outbox);
    }

    [Fact]
    public async Task Approve_with_a_stored_owner_type_that_is_not_defined_returns_OwnerTypeInvalid_and_grants_nothing()
    {
        var account = Account.Register(OwnerId, "owner@example.com", "hash", null, Now, applyAsParkingLotOwner: true);
        // The domain refuses to store such a value, so this is a damaged row. It must not reach the checklist lookup, which throws for it.
        typeof(Account).GetProperty(nameof(Account.OwnerType))!.SetValue(account, (OwnerType)99);
        var sessions = new FakeSessionRepository();
        var refreshTokens = new FakeRefreshTokenRepository();
        var outbox = new FakeOutboxWriter();
        var handler = ApproveHandler(new FakeAccountRepository(account), sessions, refreshTokens, new FakeOwnerDocumentRepository(), outbox);

        var result = await handler.Handle(new ApproveOwnerApplicationCommand(AdminId, OwnerId), TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(OwnerApplicationErrors.OwnerTypeInvalid.Code, result.Error!.Code);
        AssertNothingGranted(account, sessions, refreshTokens, outbox);
    }

    [Theory]
    [InlineData(OwnerType.Individual)]
    [InlineData(OwnerType.HouseholdBusiness)]
    [InlineData(OwnerType.Enterprise)]
    public async Task Approve_with_no_document_returns_DocumentsIncomplete_and_grants_nothing(OwnerType ownerType)
    {
        var account = Account.Register(OwnerId, "owner@example.com", "hash", null, Now, applyAsParkingLotOwner: true);
        account.SetOwnerType(ownerType);
        var sessions = new FakeSessionRepository();
        var refreshTokens = new FakeRefreshTokenRepository();
        var outbox = new FakeOutboxWriter();
        var handler = ApproveHandler(new FakeAccountRepository(account), sessions, refreshTokens, new FakeOwnerDocumentRepository(), outbox);

        var result = await handler.Handle(new ApproveOwnerApplicationCommand(AdminId, OwnerId), TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(OwnerApplicationErrors.DocumentsIncomplete.Code, result.Error!.Code);
        AssertNothingGranted(account, sessions, refreshTokens, outbox);
    }

    [Theory]
    [InlineData(OwnerType.Individual)]
    [InlineData(OwnerType.HouseholdBusiness)]
    [InlineData(OwnerType.Enterprise)]
    public async Task Approve_is_refused_whichever_single_required_document_is_missing(OwnerType ownerType)
    {
        var required = RequiredDocumentTypes(ownerType);

        foreach (var missing in required)
        {
            var account = Account.Register(OwnerId, "owner@example.com", "hash", null, Now, applyAsParkingLotOwner: true);
            account.SetOwnerType(ownerType);
            var documents = new FakeOwnerDocumentRepository();
            foreach (var documentType in required.Where(type => type != missing))
                documents.Add(Document(OwnerId, documentType));
            // The optional letter does not make up for a required document.
            documents.Add(Document(OwnerId, "AuthorizationLetter"));
            var sessions = new FakeSessionRepository();
            var refreshTokens = new FakeRefreshTokenRepository();
            var outbox = new FakeOutboxWriter();
            var handler = ApproveHandler(new FakeAccountRepository(account), sessions, refreshTokens, documents, outbox);

            var result = await handler.Handle(new ApproveOwnerApplicationCommand(AdminId, OwnerId), TestContext.Current.CancellationToken);

            Assert.False(result.IsSuccess, $"approved without {missing}");
            Assert.Equal(OwnerApplicationErrors.DocumentsIncomplete.Code, result.Error!.Code);
            AssertNothingGranted(account, sessions, refreshTokens, outbox);
        }
    }

    [Fact]
    public async Task Approve_does_not_count_a_document_that_belongs_to_another_owner_types_checklist()
    {
        var account = Account.Register(OwnerId, "owner@example.com", "hash", null, Now, applyAsParkingLotOwner: true);
        account.SetOwnerType(OwnerType.HouseholdBusiness);
        var documents = new FakeOwnerDocumentRepository();
        documents.Add(Document(OwnerId, "EnterpriseRegistration"));
        documents.Add(Document(OwnerId, "PremisesRightProof"));
        var sessions = new FakeSessionRepository();
        var refreshTokens = new FakeRefreshTokenRepository();
        var outbox = new FakeOutboxWriter();
        var handler = ApproveHandler(new FakeAccountRepository(account), sessions, refreshTokens, documents, outbox);

        var result = await handler.Handle(new ApproveOwnerApplicationCommand(AdminId, OwnerId), TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(OwnerApplicationErrors.DocumentsIncomplete.Code, result.Error!.Code);
        AssertNothingGranted(account, sessions, refreshTokens, outbox);
    }

    [Fact]
    public async Task Approve_does_not_count_the_documents_of_another_account()
    {
        var account = Account.Register(OwnerId, "owner@example.com", "hash", null, Now, applyAsParkingLotOwner: true);
        account.SetOwnerType(OwnerType.Individual);
        var documents = new FakeOwnerDocumentRepository();
        documents.Add(Document(Guid.NewGuid(), "PremisesRightProof"));
        var sessions = new FakeSessionRepository();
        var refreshTokens = new FakeRefreshTokenRepository();
        var outbox = new FakeOutboxWriter();
        var handler = ApproveHandler(new FakeAccountRepository(account), sessions, refreshTokens, documents, outbox);

        var result = await handler.Handle(new ApproveOwnerApplicationCommand(AdminId, OwnerId), TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(OwnerApplicationErrors.DocumentsIncomplete.Code, result.Error!.Code);
        AssertNothingGranted(account, sessions, refreshTokens, outbox);
    }

    [Theory]
    [InlineData(OwnerType.Individual)]
    [InlineData(OwnerType.HouseholdBusiness)]
    [InlineData(OwnerType.Enterprise)]
    public async Task Approve_with_every_required_document_grants_the_role_for_each_owner_type(OwnerType ownerType)
    {
        var account = Account.Register(OwnerId, "owner@example.com", "hash", null, Now, applyAsParkingLotOwner: true);
        account.SetOwnerType(ownerType);
        var documents = new FakeOwnerDocumentRepository();
        foreach (var documentType in RequiredDocumentTypes(ownerType))
            documents.Add(Document(OwnerId, documentType));
        var sessions = new FakeSessionRepository();
        var refreshTokens = new FakeRefreshTokenRepository();
        var outbox = new FakeOutboxWriter();
        var handler = ApproveHandler(new FakeAccountRepository(account), sessions, refreshTokens, documents, outbox);

        var result = await handler.Handle(new ApproveOwnerApplicationCommand(AdminId, OwnerId), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess, result.Error?.Code);
        Assert.Equal(AccountRole.ParkingLotOwner, account.Role);
        Assert.Equal(OwnerApplicationStatus.Approved, account.OwnerApplicationStatus);
        Assert.True(sessions.RevokedAll);
        Assert.True(refreshTokens.RevokedAll);
        Assert.Single(outbox.EnqueuedEvents);
    }

    [Fact]
    public async Task Reject_pending_application_succeeds_and_saves_reason()
    {
        var account = Account.Register(OwnerId, "owner@example.com", "hash", null, Now, applyAsParkingLotOwner: true);
        var accounts = new FakeAccountRepository(account);
        var outbox = new FakeOutboxWriter();
        var handler = new RejectOwnerApplicationCommandHandler(accounts, outbox, new FixedClock(Now));
        var reason = "Hồ sơ chưa có giấy phép hợp lệ.";

        var result = await handler.Handle(new RejectOwnerApplicationCommand(AdminId, OwnerId, reason), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal("Rejected", result.Value!.Status);
        Assert.Equal(OwnerApplicationStatus.Rejected, account.OwnerApplicationStatus);
        Assert.Equal(reason, account.OwnerRejectionReason);

        Assert.Single(outbox.EnqueuedEvents);
        var enqueued = outbox.EnqueuedEvents[0];
        Assert.Equal("identity.owner-application-rejected.v1", enqueued.EventType);
        Assert.Equal(1, enqueued.Version);
        var evt = Assert.IsType<OwnerApplicationRejectedIntegrationEvent>(enqueued.Event);
        Assert.Equal(OwnerId, evt.AccountId);
        Assert.Equal("owner@example.com", evt.Email);
        Assert.Equal(reason, evt.Reason);
        Assert.Equal(Now, evt.RejectedAtUtc);
    }

    [Fact]
    public async Task Resubmit_rejected_application_transitions_to_Pending()
    {
        var account = Account.Register(OwnerId, "owner@example.com", "hash", null, Now, applyAsParkingLotOwner: true);
        account.SetOwnerType(OwnerType.Individual);
        account.RejectOwnerApplication(AdminId, "Lý do", Now);
        var accounts = new FakeAccountRepository(account);
        var docs = new FakeOwnerDocumentRepository();
        docs.Add(OwnerApplicationDocument.Create(Guid.NewGuid(), OwnerId, "PremisesRightProof", "mb.pdf", "application/pdf", 100, "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855", "k1", Now));
        var reqs = new FakeRequirements();
        var handler = new ResubmitOwnerApplicationCommandHandler(accounts, docs, reqs, new FixedClock(Now));

        var result = await handler.Handle(new ResubmitOwnerApplicationCommand(OwnerId), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal("Pending", result.Value!.Status);
        Assert.Equal(OwnerApplicationStatus.Pending, account.OwnerApplicationStatus);
        Assert.Null(account.OwnerRejectionReason);
    }

    [Fact]
    public async Task Get_my_owner_application_returns_status_and_reason()
    {
        var account = Account.Register(OwnerId, "owner@example.com", "hash", null, Now, applyAsParkingLotOwner: true);
        account.RejectOwnerApplication(AdminId, "Thiếu hồ sơ pháp lý", Now);
        var accounts = new FakeAccountRepository(account);
        var handler = new GetMyOwnerApplicationQueryHandler(accounts);

        var result = await handler.Handle(new GetMyOwnerApplicationQuery(OwnerId), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(OwnerId, result.Value!.AccountId);
        Assert.Equal("Rejected", result.Value.Status);
        Assert.Equal("Thiếu hồ sơ pháp lý", result.Value.RejectionReason);
    }

    [Fact]
    public async Task List_owner_applications_validates_max_page_size()
    {
        var queries = new FakeOwnerApplicationQueries();
        var handler = new ListOwnerApplicationsQueryHandler(queries);

        var result = await handler.Handle(new ListOwnerApplicationsQuery("Pending", 1, 101), TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(OwnerApplicationErrors.InvalidPageSize.Code, result.Error!.Code);
    }

    [Fact]
    public async Task Apply_unknown_account_returns_AccountNotFound()
    {
        var accounts = new FakeAccountRepository(null);
        var profiles = new FakeProfileRepository();
        var handler = new ApplyForOwnerApplicationCommandHandler(accounts, profiles, new FixedClock(Now));

        var result = await handler.Handle(new ApplyForOwnerApplicationCommand(OwnerId, "0312345678", "Biz"), TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(OwnerApplicationErrors.AccountNotFound.Code, result.Error!.Code);
    }

    [Fact]
    public async Task Apply_already_owner_returns_AlreadyApproved()
    {
        var account = Account.Register(OwnerId, "owner@example.com", "hash", null, Now);
        account.AssignRole(AccountRole.ParkingLotOwner);
        var accounts = new FakeAccountRepository(account);
        var profiles = new FakeProfileRepository();
        var handler = new ApplyForOwnerApplicationCommandHandler(accounts, profiles, new FixedClock(Now));

        var result = await handler.Handle(new ApplyForOwnerApplicationCommand(OwnerId, "0312345678", "Biz"), TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(OwnerApplicationErrors.AlreadyApproved.Code, result.Error!.Code);
    }

    [Fact]
    public async Task Apply_already_pending_returns_AlreadyPending()
    {
        var account = Account.Register(OwnerId, "owner@example.com", "hash", null, Now, applyAsParkingLotOwner: true);
        var accounts = new FakeAccountRepository(account);
        var profiles = new FakeProfileRepository();
        var handler = new ApplyForOwnerApplicationCommandHandler(accounts, profiles, new FixedClock(Now));

        var result = await handler.Handle(new ApplyForOwnerApplicationCommand(OwnerId, "0312345678", "Biz"), TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(OwnerApplicationErrors.AlreadyPending.Code, result.Error!.Code);
    }

    [Fact]
    public async Task Apply_valid_customer_succeeds_and_updates_profile_and_phone()
    {
        var account = Account.Register(OwnerId, "customer@example.com", "hash", null, Now);
        account.VerifyEmail();
        var accounts = new FakeAccountRepository(account);
        var profiles = new FakeProfileRepository();
        var handler = new ApplyForOwnerApplicationCommandHandler(accounts, profiles, new FixedClock(Now));

        var command = new ApplyForOwnerApplicationCommand(
            OwnerId,
            TaxCode: "0312345678",
            BusinessName: "Smart Lot Co",
            PhoneNumber: "0912345678",
            FullName: "Tran Owner");

        var result = await handler.Handle(command, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal("Pending", result.Value!.Status);
        Assert.Equal(OwnerApplicationStatus.Pending, account.OwnerApplicationStatus);
        Assert.Equal("0312345678", account.OwnerTaxCode);
        Assert.Equal("Smart Lot Co", account.OwnerBusinessName);
        Assert.Equal("+84912345678", account.PhoneNumber);
        Assert.NotNull(profiles.CreatedProfile);
        Assert.Equal("Tran Owner", profiles.CreatedProfile!.FullName);
    }

    // F3: a number that is not an owner type (the endpoint used to accept "99") must not reach the application.
    [Fact]
    public async Task Apply_with_an_owner_type_that_is_not_defined_is_rejected_and_changes_nothing()
    {
        var account = Account.Register(OwnerId, "customer@example.com", "hash", null, Now);
        var accounts = new FakeAccountRepository(account);
        var handler = new ApplyForOwnerApplicationCommandHandler(accounts, new FakeProfileRepository(), new FixedClock(Now));

        var result = await handler.Handle(
            new ApplyForOwnerApplicationCommand(OwnerId, OwnerType: (OwnerType)99), TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(OwnerApplicationErrors.OwnerTypeInvalid.Code, result.Error!.Code);
        Assert.Null(account.OwnerApplicationStatus);
        Assert.Null(account.OwnerType);
    }

    private static ConfigurationOwnerDocumentRequirements RealRequirements() =>
        new(Options.Create(new OwnerDocumentsOptions()));

    private static ApproveOwnerApplicationCommandHandler ApproveHandler(
        FakeAccountRepository accounts,
        FakeSessionRepository sessions,
        FakeRefreshTokenRepository refreshTokens,
        FakeOwnerDocumentRepository documents,
        FakeOutboxWriter outbox) =>
        new(accounts, sessions, refreshTokens, documents, RealRequirements(), outbox, new FixedClock(Now));

    private static List<string> RequiredDocumentTypes(OwnerType ownerType) =>
        RealRequirements().GetRequirements(ownerType).Where(item => item.Required).Select(item => item.DocumentType).ToList();

    private static OwnerApplicationDocument Document(Guid accountId, string documentType) =>
        OwnerApplicationDocument.Create(
            Guid.NewGuid(), accountId, documentType, $"{documentType}.pdf", "application/pdf", 100,
            "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855", Guid.NewGuid().ToString("N"), Now);

    private static void AssertNothingGranted(
        Account account,
        FakeSessionRepository sessions,
        FakeRefreshTokenRepository refreshTokens,
        FakeOutboxWriter outbox)
    {
        Assert.Equal(OwnerApplicationStatus.Pending, account.OwnerApplicationStatus);
        Assert.NotEqual(AccountRole.ParkingLotOwner, account.Role);
        Assert.Null(account.OwnerReviewedAtUtc);
        Assert.False(sessions.RevokedAll);
        Assert.False(refreshTokens.RevokedAll);
        Assert.Empty(outbox.EnqueuedEvents);
    }

    private sealed class FixedClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset UtcNow => now;
    }

    private sealed class FakeProfileRepository : ParkingSystem.Identity.Application.Profiles.IProfileRepository
    {
        public ParkingSystem.Identity.Domain.Profiles.Profile? CreatedProfile { get; private set; }

        public Task<ParkingSystem.Identity.Domain.Profiles.Profile?> FindByAccountIdAsync(Guid accountId, CancellationToken cancellationToken) =>
            Task.FromResult(CreatedProfile);

        public Task<ParkingSystem.Identity.Domain.Profiles.Profile?> FindByAccountIdForUpdateAsync(Guid accountId, CancellationToken cancellationToken) =>
            Task.FromResult(CreatedProfile);

        public void Add(ParkingSystem.Identity.Domain.Profiles.Profile profile)
        {
            CreatedProfile = profile;
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class FakeOwnerApplicationQueries : IOwnerApplicationQueries
    {
        public Task<(IReadOnlyList<OwnerApplicationListItem> Items, int TotalCount)> ListOwnerApplicationsAsync(
            string? status, int page, int pageSize, CancellationToken cancellationToken) =>
            Task.FromResult<(IReadOnlyList<OwnerApplicationListItem>, int)>(([new OwnerApplicationListItem(
                OwnerId, "owner@example.com", null, "Nguyen Van A", "Pending", null, null, null, null, null, Now)], 1));
    }

    private sealed class FakeAccountRepository(Account? account) : IAccountRepository
    {
        public Task<Account?> FindByIdAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult(account is not null && account.Id == id ? account : null);

        public Task<Account?> FindByIdForUpdateAsync(Guid id, CancellationToken cancellationToken) =>
            FindByIdAsync(id, cancellationToken);

        public Task<Account?> FindByEmailAsync(string normalizedEmail, CancellationToken cancellationToken) =>
            Task.FromResult<Account?>(null);

        public Task<Account?> FindByEmailForUpdateAsync(string normalizedEmail, CancellationToken cancellationToken) =>
            Task.FromResult<Account?>(null);

        public Task<bool> EmailExistsAsync(string normalizedEmail, CancellationToken cancellationToken) =>
            Task.FromResult(false);

        public void Add(Account a) { }

        public Task<SaveAccountResult> SaveNewAccountAsync(CancellationToken cancellationToken) =>
            Task.FromResult(SaveAccountResult.Success);

        public Task<bool> TrySaveChangesAsync(CancellationToken cancellationToken) =>
            Task.FromResult(true);

        public Task SaveChangesAsync(CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task<TResult> ExecuteInTransactionAsync<TResult>(Func<Task<TResult>> action, CancellationToken cancellationToken) =>
            action();
    }

    private sealed class FakeSessionRepository : IAuthSessionRepository
    {
        public bool RevokedAll { get; private set; }

        public void Add(AuthSession session) { }
        public Task<AuthSession?> FindByIdAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult<AuthSession?>(null);
        public Task<AuthSession?> FindByIdForUpdateAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult<AuthSession?>(null);
        public Task RevokeAllForAccountAsync(Guid accountId, DateTimeOffset nowUtc, string reason, CancellationToken cancellationToken)
        {
            RevokedAll = true;
            return Task.CompletedTask;
        }
        public Task<IReadOnlyList<AuthSession>> ListActiveForAccountAsync(Guid accountId, DateTimeOffset nowUtc, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<AuthSession>>([]);
    }

    private sealed class FakeRefreshTokenRepository : IRefreshTokenRepository
    {
        public bool RevokedAll { get; private set; }

        public void Add(RefreshToken refreshToken) { }
        public Task<RefreshToken?> FindByHashAsync(string tokenHash, CancellationToken cancellationToken) => Task.FromResult<RefreshToken?>(null);
        public Task<RefreshToken?> FindLatestBySessionIdAsync(Guid sessionId, CancellationToken cancellationToken) => Task.FromResult<RefreshToken?>(null);
        public Task RevokeActiveTokensBySessionIdAsync(Guid sessionId, DateTimeOffset nowUtc, string reason, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task RevokeAllForAccountAsync(Guid accountId, DateTimeOffset nowUtc, string reason, CancellationToken cancellationToken)
        {
            RevokedAll = true;
            return Task.CompletedTask;
        }
        public Task ReloadAsync(RefreshToken refreshToken, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class FakeOutboxWriter : IOutboxWriter
    {
        public List<(object Event, string EventType, int Version)> EnqueuedEvents { get; } = [];

        public void Enqueue<TEvent>(
            TEvent integrationEvent,
            string eventType,
            int eventVersion,
            Guid? correlationId = null,
            Guid? causationId = null) where TEvent : class
        {
            EnqueuedEvents.Add((integrationEvent, eventType, eventVersion));
        }
    }

    private sealed class FakeOwnerDocumentRepository : IOwnerDocumentRepository
    {
        private readonly List<OwnerApplicationDocument> _docs = [];
        public void Add(OwnerApplicationDocument document) => _docs.Add(document);
        public void Remove(OwnerApplicationDocument document) => _docs.Remove(document);
        public Task<OwnerApplicationDocument?> FindByAccountAndTypeAsync(Guid accountId, string documentType, CancellationToken cancellationToken) =>
            Task.FromResult(_docs.FirstOrDefault(d => d.AccountId == accountId && d.DocumentType == documentType));
        public Task<IReadOnlyList<OwnerApplicationDocument>> ListByAccountAsync(Guid accountId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<OwnerApplicationDocument>>(_docs.Where(d => d.AccountId == accountId).ToList());
        public Task<int> CountByAccountAsync(Guid accountId, CancellationToken cancellationToken) =>
            Task.FromResult(_docs.Count(d => d.AccountId == accountId));
        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class FakeRequirements : IOwnerDocumentRequirements
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
