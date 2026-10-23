using ParkingSystem.BuildingBlocks.Results;
using ParkingSystem.BuildingBlocks.Time;
using ParkingSystem.Contracts.Events.Identity;
using ParkingSystem.Identity.Application.Accounts;
using ParkingSystem.Identity.Application.Accounts.OwnerApplications;
using ParkingSystem.Identity.Application.Common.Messaging.Outbox;
using ParkingSystem.Identity.Domain.Accounts;
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
        var handler = new ApproveOwnerApplicationCommandHandler(accounts, sessions, refreshTokens, outbox, new FixedClock(Now));

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
        var handler = new ApproveOwnerApplicationCommandHandler(accounts, sessions, refreshTokens, outbox, new FixedClock(Now));

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
        var handler = new ApproveOwnerApplicationCommandHandler(accounts, sessions, refreshTokens, outbox, new FixedClock(Now));

        var result = await handler.Handle(new ApproveOwnerApplicationCommand(AdminId, OwnerId), TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(OwnerApplicationErrors.NotPending.Code, result.Error!.Code);
        Assert.Empty(outbox.EnqueuedEvents);
    }

    [Fact]
    public async Task Approve_pending_application_succeeds_and_revokes_sessions()
    {
        var account = Account.Register(OwnerId, "owner@example.com", "hash", null, Now, applyAsParkingLotOwner: true);
        var accounts = new FakeAccountRepository(account);
        var sessions = new FakeSessionRepository();
        var refreshTokens = new FakeRefreshTokenRepository();
        var outbox = new FakeOutboxWriter();
        var handler = new ApproveOwnerApplicationCommandHandler(accounts, sessions, refreshTokens, outbox, new FixedClock(Now));

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

    // R5: the existence check cannot see an application that is not committed yet, so the unique index decides.
    // The loser gets the business error (409), never an unhandled exception (500).
    [Fact]
    public async Task Apply_that_loses_the_unique_index_race_returns_TaxCodeAlreadyRegistered()
    {
        var account = Account.Register(OwnerId, "customer@example.com", "hash", null, Now);
        account.VerifyEmail();
        var accounts = new FakeAccountRepository(account) { OwnerApplicationSaveWins = false };
        var handler = new ApplyForOwnerApplicationCommandHandler(accounts, new FakeProfileRepository(), new FixedClock(Now));

        var result = await handler.Handle(new ApplyForOwnerApplicationCommand(OwnerId, TaxCode: "0312345678"), TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(OwnerApplicationErrors.TaxCodeAlreadyRegistered.Code, result.Error!.Code);
    }

    [Fact]
    public async Task Apply_with_a_tax_code_that_is_already_registered_returns_TaxCodeAlreadyRegistered_and_changes_nothing()
    {
        var account = Account.Register(OwnerId, "customer@example.com", "hash", null, Now);
        account.VerifyEmail();
        var accounts = new FakeAccountRepository(account) { TaxCodeIsTaken = true };
        var handler = new ApplyForOwnerApplicationCommandHandler(accounts, new FakeProfileRepository(), new FixedClock(Now));

        var result = await handler.Handle(new ApplyForOwnerApplicationCommand(OwnerId, TaxCode: "0312345678"), TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(OwnerApplicationErrors.TaxCodeAlreadyRegistered.Code, result.Error!.Code);
        Assert.Null(account.OwnerApplicationStatus);
        Assert.Null(account.OwnerTaxCode);
    }

    [Fact]
    public async Task Resubmit_that_loses_the_unique_index_race_returns_TaxCodeAlreadyRegistered()
    {
        var handler = ResubmitHandlerForARejectedOwner(new FakeAccountRepository(RejectedOwner()) { OwnerApplicationSaveWins = false });

        var result = await handler.Handle(new ResubmitOwnerApplicationCommand(OwnerId, TaxCode: "0312345678"), TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(OwnerApplicationErrors.TaxCodeAlreadyRegistered.Code, result.Error!.Code);
    }

    [Fact]
    public async Task Resubmit_with_a_tax_code_that_is_already_registered_returns_TaxCodeAlreadyRegistered()
    {
        var handler = ResubmitHandlerForARejectedOwner(new FakeAccountRepository(RejectedOwner()) { TaxCodeIsTaken = true });

        var result = await handler.Handle(new ResubmitOwnerApplicationCommand(OwnerId, TaxCode: "0312345678"), TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(OwnerApplicationErrors.TaxCodeAlreadyRegistered.Code, result.Error!.Code);
    }

    private static Account RejectedOwner()
    {
        var account = Account.Register(OwnerId, "owner@example.com", "hash", null, Now, applyAsParkingLotOwner: true);
        account.SetOwnerType(OwnerType.Individual);
        account.RejectOwnerApplication(AdminId, "Lý do", Now);
        return account;
    }

    private static ResubmitOwnerApplicationCommandHandler ResubmitHandlerForARejectedOwner(FakeAccountRepository accounts)
    {
        var docs = new FakeOwnerDocumentRepository();
        docs.Add(OwnerApplicationDocument.Create(Guid.NewGuid(), OwnerId, "PremisesRightProof", "mb.pdf", "application/pdf", 100, "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855", "stored/mb.pdf", Now));
        return new ResubmitOwnerApplicationCommandHandler(accounts, docs, new FakeRequirements(), new FixedClock(Now));
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
        // Both default to the answers of an empty database, so the existing tests keep their meaning.
        public bool TaxCodeIsTaken { get; init; }

        public bool OwnerApplicationSaveWins { get; init; } = true;

        public Task<bool> OwnerTaxCodeExistsAsync(string taxCode, Guid? excludeAccountId, CancellationToken cancellationToken) =>
            Task.FromResult(TaxCodeIsTaken);

        public Task<bool> TrySaveOwnerApplicationAsync(CancellationToken cancellationToken) =>
            Task.FromResult(OwnerApplicationSaveWins);

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
