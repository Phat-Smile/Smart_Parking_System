using Microsoft.Extensions.Options;
using ParkingSystem.BuildingBlocks.Results;
using ParkingSystem.BuildingBlocks.Time;
using ParkingSystem.Contracts.Events.Identity;
using ParkingSystem.Identity.Application.Accounts;
using ParkingSystem.Identity.Application.Accounts.Admin.ActivateAccount;
using ParkingSystem.Identity.Application.Accounts.Admin.SuspendAccount;
using ParkingSystem.Identity.Application.Accounts.ChangeAccountRole;
using ParkingSystem.Identity.Application.Accounts.OwnerApplications;
using ParkingSystem.Identity.Application.Accounts.UnlockAccount;
using ParkingSystem.Identity.Application.Common.Auditing;
using ParkingSystem.Identity.Application.Common.Messaging.Outbox;
using ParkingSystem.Identity.Domain.Accounts;
using ParkingSystem.Identity.Domain.AuditLogs;
using ParkingSystem.Identity.Infrastructure.OwnerDocuments;
using Xunit;

namespace ParkingSystem.Tests.Identity.UnitTests;

public sealed class AdminAuditLogUnitTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 15, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid AdminId = Guid.NewGuid();
    private static readonly Guid TargetAccountId = Guid.NewGuid();
    private const string ClientIp = "192.168.1.100";

    #region Owner Application Approval & Rejection

    [Fact]
    public async Task ApproveOwnerApplication_WhenApproved_WritesExactlyOneAuditLogRecordWithoutPII()
    {
        // Arrange
        var account = Account.Register(TargetAccountId, "owner@example.com", "hashed_pwd", null, Now, applyAsParkingLotOwner: true);
        account.SetOwnerType(OwnerType.Individual);

        var accountRepo = new FakeAccountRepository(account);
        var sessionRepo = new FakeSessionRepository();
        var refreshRepo = new FakeRefreshTokenRepository();
        var docRepo = new FakeOwnerDocumentRepository();
        docRepo.Add(CreateDocument(TargetAccountId, "PremisesRightProof"));
        var outbox = new FakeOutboxWriter();
        var auditLog = new FakeIdentityAuditLog();

        var handler = new ApproveOwnerApplicationCommandHandler(
            accountRepo,
            sessionRepo,
            refreshRepo,
            docRepo,
            new ConfigurationOwnerDocumentRequirements(Options.Create(new OwnerDocumentsOptions())),
            outbox,
            new FixedClock(Now),
            auditLog);

        var command = new ApproveOwnerApplicationCommand(AdminId, TargetAccountId, ClientIp);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Single(auditLog.Entries);

        var entry = auditLog.Entries[0];
        Assert.Equal("account.owner_approved", entry.ActionType);
        Assert.Equal(AdminId, entry.ActorId);
        Assert.Null(entry.ActorEmail); // PII Protection: Plain email must NOT be recorded
        Assert.Equal("SystemAdmin", entry.ActorRole);
        Assert.Equal("account", entry.TargetType);
        Assert.Equal(TargetAccountId.ToString(), entry.TargetId);
        Assert.Equal(ClientIp, entry.IpAddress);
        Assert.False(string.IsNullOrWhiteSpace(entry.Reason));
        Assert.True(entry.Reason.Length <= 500);

        // Ensure no sensitive document numbers, CCCD, or plain emails are stored in audit log
        Assert.Null(entry.Details);
    }

    [Fact]
    public async Task ApproveOwnerApplication_WhenValidationFails_WritesZeroAuditLogs()
    {
        // Arrange: Missing documents -> approval fails
        var account = Account.Register(TargetAccountId, "applicant@example.com", "hashed_pwd", null, Now, applyAsParkingLotOwner: true);
        account.SetOwnerType(OwnerType.Enterprise);

        var accountRepo = new FakeAccountRepository(account);
        var auditLog = new FakeIdentityAuditLog();

        var handler = new ApproveOwnerApplicationCommandHandler(
            accountRepo,
            new FakeSessionRepository(),
            new FakeRefreshTokenRepository(),
            new FakeOwnerDocumentRepository(), // empty documents
            new ConfigurationOwnerDocumentRequirements(Options.Create(new OwnerDocumentsOptions())),
            new FakeOutboxWriter(),
            new FixedClock(Now),
            auditLog);

        var command = new ApproveOwnerApplicationCommand(AdminId, TargetAccountId, ClientIp);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(OwnerApplicationErrors.DocumentsIncomplete.Code, result.Error!.Code);
        Assert.Empty(auditLog.Entries);
    }

    [Fact]
    public async Task RejectOwnerApplication_WhenRejected_WritesExactlyOneAuditLogRecordWithReasonAndNoPII()
    {
        // Arrange
        var account = Account.Register(TargetAccountId, "owner@example.com", "hashed_pwd", null, Now, applyAsParkingLotOwner: true);
        var accountRepo = new FakeAccountRepository(account);
        var outbox = new FakeOutboxWriter();
        var auditLog = new FakeIdentityAuditLog();

        var handler = new RejectOwnerApplicationCommandHandler(
            accountRepo,
            outbox,
            new FixedClock(Now),
            auditLog);

        const string rejectionReason = "Giấy phép kinh doanh không khớp thông tin đăng ký.";
        var command = new RejectOwnerApplicationCommand(AdminId, TargetAccountId, rejectionReason, ClientIp);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Single(auditLog.Entries);

        var entry = auditLog.Entries[0];
        Assert.Equal("account.owner_rejected", entry.ActionType);
        Assert.Equal(AdminId, entry.ActorId);
        Assert.Null(entry.ActorEmail); // PII Protection: plain email not logged
        Assert.Equal("SystemAdmin", entry.ActorRole);
        Assert.Equal("account", entry.TargetType);
        Assert.Equal(TargetAccountId.ToString(), entry.TargetId);
        Assert.Equal(rejectionReason, entry.Reason);
        Assert.Equal(ClientIp, entry.IpAddress);
        Assert.Null(entry.Details);
    }

    [Fact]
    public async Task RejectOwnerApplication_WhenSelfRejection_WritesZeroAuditLogs()
    {
        // Arrange: Admin cannot reject own application
        var auditLog = new FakeIdentityAuditLog();
        var handler = new RejectOwnerApplicationCommandHandler(
            new FakeAccountRepository(null),
            new FakeOutboxWriter(),
            new FixedClock(Now),
            auditLog);

        var command = new RejectOwnerApplicationCommand(AdminId, AdminId, "Reason", ClientIp);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(OwnerApplicationErrors.CannotRejectOwnApplication.Code, result.Error!.Code);
        Assert.Empty(auditLog.Entries);
    }

    #endregion

    #region Role Changes

    [Fact]
    public async Task ChangeAccountRole_WhenRoleChanged_WritesExactlyOneAuditLog()
    {
        // Arrange
        var account = Account.Register(TargetAccountId, "user@example.com", "hashed_pwd", null, Now);
        account.VerifyEmail();
        Assert.Equal(AccountRole.Customer, account.Role);

        var accountRepo = new FakeAccountRepository(account);
        var sessionRepo = new FakeSessionRepository();
        var refreshRepo = new FakeRefreshTokenRepository();
        var auditLog = new FakeIdentityAuditLog();

        var handler = new ChangeAccountRoleCommandHandler(
            accountRepo,
            sessionRepo,
            refreshRepo,
            new FixedClock(Now),
            auditLog);

        var command = new ChangeAccountRoleCommand(AdminId, TargetAccountId, "Staff", ClientIp);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Single(auditLog.Entries);

        var entry = auditLog.Entries[0];
        Assert.Equal("account.role_changed", entry.ActionType);
        Assert.Equal(AdminId, entry.ActorId);
        Assert.Null(entry.ActorEmail);
        Assert.Equal("SystemAdmin", entry.ActorRole);
        Assert.Equal("account", entry.TargetType);
        Assert.Equal(TargetAccountId.ToString(), entry.TargetId);
        Assert.Equal(ClientIp, entry.IpAddress);
        Assert.NotNull(entry.Details);
        Assert.Contains("Customer", entry.Details);
        Assert.Contains("Staff", entry.Details);
        Assert.DoesNotContain("user@example.com", entry.Details); // No email PII leak
    }

    [Fact]
    public async Task ChangeAccountRole_WhenRoleUnchanged_WritesZeroAuditLogs()
    {
        // Arrange
        var account = Account.Register(TargetAccountId, "user@example.com", "hashed_pwd", null, Now);
        account.VerifyEmail();
        account.AssignRole(AccountRole.Staff);

        var accountRepo = new FakeAccountRepository(account);
        var auditLog = new FakeIdentityAuditLog();

        var handler = new ChangeAccountRoleCommandHandler(
            accountRepo,
            new FakeSessionRepository(),
            new FakeRefreshTokenRepository(),
            new FixedClock(Now),
            auditLog);

        var command = new ChangeAccountRoleCommand(AdminId, TargetAccountId, "Staff", ClientIp);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Empty(auditLog.Entries);
    }

    #endregion

    #region Account Lock / Unlock / Suspend / Activate

    [Fact]
    public async Task SuspendAccount_WhenSuspended_WritesExactlyOneAuditLogWithAccountLocked()
    {
        // Arrange
        var account = Account.Register(TargetAccountId, "user@example.com", "hashed_pwd", null, Now);
        account.VerifyEmail();

        var accountRepo = new FakeAccountRepository(account);
        var sessionRepo = new FakeSessionRepository();
        var auditLog = new FakeIdentityAuditLog();

        var handler = new SuspendAccountCommandHandler(
            accountRepo,
            sessionRepo,
            new FixedClock(Now),
            auditLog);

        var command = new SuspendAccountCommand(AdminId, TargetAccountId, ClientIp);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Single(auditLog.Entries);

        var entry = auditLog.Entries[0];
        Assert.Equal("account.locked", entry.ActionType);
        Assert.Equal(AdminId, entry.ActorId);
        Assert.Null(entry.ActorEmail);
        Assert.Equal("SystemAdmin", entry.ActorRole);
        Assert.Equal("account", entry.TargetType);
        Assert.Equal(TargetAccountId.ToString(), entry.TargetId);
        Assert.Equal(ClientIp, entry.IpAddress);
    }

    [Fact]
    public async Task ActivateAccount_WhenActivated_WritesExactlyOneAuditLogWithAccountUnlocked()
    {
        // Arrange
        var account = Account.Register(TargetAccountId, "user@example.com", "hashed_pwd", null, Now);
        account.VerifyEmail();
        account.Suspend();

        var accountRepo = new FakeAccountRepository(account);
        var auditLog = new FakeIdentityAuditLog();

        var handler = new ActivateAccountCommandHandler(accountRepo, auditLog);
        var command = new ActivateAccountCommand(TargetAccountId, AdminId, ClientIp);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Single(auditLog.Entries);

        var entry = auditLog.Entries[0];
        Assert.Equal("account.unlocked", entry.ActionType);
        Assert.Equal(AdminId, entry.ActorId);
        Assert.Null(entry.ActorEmail);
        Assert.Equal("account", entry.TargetType);
        Assert.Equal(TargetAccountId.ToString(), entry.TargetId);
        Assert.Equal(ClientIp, entry.IpAddress);
    }

    [Fact]
    public async Task UnlockAccount_WhenUnlocked_WritesExactlyOneAuditLogWithAccountUnlocked()
    {
        // Arrange
        var account = Account.Register(TargetAccountId, "user@example.com", "hashed_pwd", null, Now);
        account.VerifyEmail();
        account.RecordFailedLogin(Now);
        account.RecordFailedLogin(Now);
        account.RecordFailedLogin(Now); // Triggers 15-minute lockout

        var accountRepo = new FakeAccountRepository(account);
        var auditLog = new FakeIdentityAuditLog();

        var handler = new UnlockAccountCommandHandler(accountRepo, new FixedClock(Now), auditLog);
        var command = new UnlockAccountCommand(TargetAccountId, AdminId, ClientIp);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Single(auditLog.Entries);

        var entry = auditLog.Entries[0];
        Assert.Equal("account.unlocked", entry.ActionType);
        Assert.Equal(AdminId, entry.ActorId);
        Assert.Null(entry.ActorEmail);
        Assert.Equal("account", entry.TargetType);
        Assert.Equal(TargetAccountId.ToString(), entry.TargetId);
        Assert.Equal(ClientIp, entry.IpAddress);
    }

    #endregion

    #region Domain Entity Invariants & Sanitization

    [Fact]
    public void AuditLog_Create_ThrowsOnEmptyReason()
    {
        Assert.Throws<ArgumentException>(() => AuditLog.Create(
            Guid.NewGuid(),
            DateTime.UtcNow,
            "account.owner_approved",
            AdminId,
            null,
            "SystemAdmin",
            null,
            "account",
            TargetAccountId.ToString(),
            "",
            ClientIp,
            null));
    }

    [Fact]
    public void AuditLog_Create_ThrowsOnReasonExceeding500Characters()
    {
        var longReason = new string('A', 501);
        Assert.Throws<ArgumentException>(() => AuditLog.Create(
            Guid.NewGuid(),
            DateTime.UtcNow,
            "account.owner_approved",
            AdminId,
            null,
            "SystemAdmin",
            null,
            "account",
            TargetAccountId.ToString(),
            longReason,
            ClientIp,
            null));
    }

    [Fact]
    public void AuditLog_Create_DefaultsActorRoleToSystemWhenEmpty()
    {
        var log = AuditLog.Create(
            Guid.NewGuid(),
            DateTime.UtcNow,
            "account.owner_approved",
            null,
            null,
            "",
            null,
            "account",
            TargetAccountId.ToString(),
            "Valid Reason",
            ClientIp,
            null);

        Assert.Equal("System", log.ActorRole);
        Assert.Equal("identity", log.SourceService);
    }

    #endregion

    #region Fakes & Helpers

    private sealed class FixedClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset UtcNow => now;
    }

    private static OwnerApplicationDocument CreateDocument(Guid accountId, string docType) =>
        OwnerApplicationDocument.Create(
            Guid.NewGuid(), accountId, docType, $"{docType}.pdf", "application/pdf", 1024,
            "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855", Guid.NewGuid().ToString("N"), Now);

    public sealed record AuditLogTestEntry(
        string ActionType,
        Guid? ActorId,
        string? ActorEmail,
        string ActorRole,
        string TargetType,
        string TargetId,
        string Reason,
        string? IpAddress,
        string? Details);

    private sealed class FakeIdentityAuditLog : IIdentityAuditLog
    {
        public List<AuditLogTestEntry> Entries { get; } = [];

        public void Record(
            string actionType,
            Guid? actorId,
            string? actorEmail,
            string actorRole,
            string targetType,
            string targetId,
            string reason,
            string? ipAddress = null,
            string? details = null)
        {
            Entries.Add(new(actionType, actorId, actorEmail, actorRole, targetType, targetId, reason, ipAddress, details));
        }
    }

    private sealed class FakeAccountRepository(Account? account) : IAccountRepository
    {
        public Task<Account?> FindByIdAsync(Guid id, CancellationToken ct) =>
            Task.FromResult(account?.Id == id ? account : null);

        public Task<Account?> FindByIdForUpdateAsync(Guid id, CancellationToken ct) =>
            FindByIdAsync(id, ct);

        public Task<Account?> FindByEmailAsync(string normalizedEmail, CancellationToken ct) =>
            Task.FromResult<Account?>(null);

        public Task<Account?> FindByEmailForUpdateAsync(string normalizedEmail, CancellationToken ct) =>
            Task.FromResult<Account?>(null);

        public Task<bool> EmailExistsAsync(string normalizedEmail, CancellationToken ct) =>
            Task.FromResult(false);

        public Task<bool> OwnerTaxCodeExistsAsync(string taxCode, Guid? excludeAccountId, CancellationToken ct) =>
            Task.FromResult(false);

        public void Add(Account a) { }

        public Task<SaveAccountResult> SaveNewAccountAsync(CancellationToken ct) =>
            Task.FromResult(SaveAccountResult.Success);

        public Task<bool> TrySaveChangesAsync(CancellationToken ct) =>
            Task.FromResult(true);

        public Task SaveChangesAsync(CancellationToken ct) =>
            Task.CompletedTask;

        public Task<bool> TrySaveOwnerApplicationAsync(CancellationToken ct) =>
            Task.FromResult(true);

        public Task<TResult> ExecuteInTransactionAsync<TResult>(Func<Task<TResult>> action, CancellationToken ct) =>
            action();
    }

    private sealed class FakeSessionRepository : IAuthSessionRepository
    {
        public void Add(AuthSession s) { }
        public Task<AuthSession?> FindByIdAsync(Guid id, CancellationToken ct) => Task.FromResult<AuthSession?>(null);
        public Task<AuthSession?> FindByIdForUpdateAsync(Guid id, CancellationToken ct) => Task.FromResult<AuthSession?>(null);
        public Task RevokeAllForAccountAsync(Guid acctId, DateTimeOffset n, string reason, CancellationToken ct) => Task.CompletedTask;
        public Task<IReadOnlyList<AuthSession>> ListActiveForAccountAsync(Guid acctId, DateTimeOffset n, CancellationToken ct) => Task.FromResult<IReadOnlyList<AuthSession>>([]);
    }

    private sealed class FakeRefreshTokenRepository : IRefreshTokenRepository
    {
        public void Add(RefreshToken refreshToken) { }
        public Task<RefreshToken?> FindByHashAsync(string tokenHash, CancellationToken ct) => Task.FromResult<RefreshToken?>(null);
        public Task<RefreshToken?> FindLatestBySessionIdAsync(Guid sessionId, CancellationToken ct) => Task.FromResult<RefreshToken?>(null);
        public Task RevokeActiveTokensBySessionIdAsync(Guid sessionId, DateTimeOffset nowUtc, string reason, CancellationToken ct) => Task.CompletedTask;
        public Task RevokeAllForAccountAsync(Guid accountId, DateTimeOffset nowUtc, string reason, CancellationToken ct) => Task.CompletedTask;
        public Task ReloadAsync(RefreshToken refreshToken, CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class FakeOwnerDocumentRepository : IOwnerDocumentRepository
    {
        private readonly List<OwnerApplicationDocument> _docs = [];
        public void Add(OwnerApplicationDocument doc) => _docs.Add(doc);
        public void Remove(OwnerApplicationDocument doc) => _docs.Remove(doc);
        public Task<OwnerApplicationDocument?> FindByAccountAndTypeAsync(Guid acctId, string docType, CancellationToken ct) =>
            Task.FromResult(_docs.FirstOrDefault(d => d.AccountId == acctId && d.DocumentType == docType));
        public Task<IReadOnlyList<OwnerApplicationDocument>> ListByAccountAsync(Guid acctId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<OwnerApplicationDocument>>(_docs.Where(d => d.AccountId == acctId).ToList());
        public Task<int> CountByAccountAsync(Guid acctId, CancellationToken ct) =>
            Task.FromResult(_docs.Count(d => d.AccountId == acctId));
        public Task SaveChangesAsync(CancellationToken ct) => Task.CompletedTask;
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

    #endregion
}
