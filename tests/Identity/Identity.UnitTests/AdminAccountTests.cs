using ParkingSystem.Identity.Application.Accounts;
using ParkingSystem.Identity.Application.Accounts.Admin.ActivateAccount;
using ParkingSystem.Identity.Application.Accounts.Admin.SuspendAccount;
using ParkingSystem.Identity.Application.Accounts.UnlockAccount;
using ParkingSystem.Identity.Domain.Accounts;
using Xunit;

namespace ParkingSystem.Tests.Identity.UnitTests;

public sealed class SuspendAccountTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 15, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Suspends_account_and_revokes_all_sessions()
    {
        var adminId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var account = Account.Register(userId, "user@test.com", "hash", null, Now);
        account.VerifyEmail();
        var sessionRepo = new FakeSessionRepo(userId);
        var accountRepo = new FakeAccountRepo(account);

        var handler = new SuspendAccountCommandHandler(accountRepo, sessionRepo, new FixedClock(Now), new FakeIdentityAuditLog());
        var result = await handler.Handle(new SuspendAccountCommand(adminId, userId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(AccountStatus.Suspended, account.Status);
        Assert.True(sessionRepo.RevokedAll);
    }

    [Fact]
    public async Task Returns_cannot_suspend_self_when_admin_suspends_own_account()
    {
        var adminId = Guid.NewGuid();
        var handler = new SuspendAccountCommandHandler(
            new FakeAccountRepo(null),
            new FakeSessionRepo(adminId),
            new FixedClock(Now),
            new FakeIdentityAuditLog());

        var result = await handler.Handle(new SuspendAccountCommand(adminId, adminId), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("identity.admin_cannot_suspend_self", result.Error!.Code);
    }

    [Fact]
    public async Task Returns_cannot_suspend_admin_when_target_is_system_admin()
    {
        var adminId = Guid.NewGuid();
        var targetId = Guid.NewGuid();
        var targetAdmin = Account.Register(targetId, "admin2@test.com", "hash", null, Now);
        targetAdmin.VerifyEmail();
        targetAdmin.AssignRole(AccountRole.SystemAdmin);

        var handler = new SuspendAccountCommandHandler(
            new FakeAccountRepo(targetAdmin),
            new FakeSessionRepo(targetId),
            new FixedClock(Now),
            new FakeIdentityAuditLog());

        var result = await handler.Handle(new SuspendAccountCommand(adminId, targetId), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("identity.cannot_suspend_admin", result.Error!.Code);
    }

    [Fact]
    public async Task Returns_not_found_when_account_does_not_exist()
    {
        var handler = new SuspendAccountCommandHandler(
            new FakeAccountRepo(null),
            new FakeSessionRepo(Guid.NewGuid()),
            new FixedClock(Now),
            new FakeIdentityAuditLog());

        var result = await handler.Handle(new SuspendAccountCommand(Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("identity.account_not_found", result.Error!.Code);
    }

    private sealed class FakeSessionRepo(Guid accountId) : IAuthSessionRepository
    {
        public bool RevokedAll { get; private set; }

        public void Add(AuthSession s) { }
        public Task<AuthSession?> FindByIdAsync(Guid id, CancellationToken ct) => Task.FromResult<AuthSession?>(null);
        public Task<AuthSession?> FindByIdForUpdateAsync(Guid id, CancellationToken ct) => Task.FromResult<AuthSession?>(null);
        public Task RevokeAllForAccountAsync(Guid acctId, DateTimeOffset n, string reason, CancellationToken ct)
        {
            if (acctId == accountId) RevokedAll = true;
            return Task.CompletedTask;
        }
        public Task<IReadOnlyList<AuthSession>> ListActiveForAccountAsync(Guid acctId, DateTimeOffset n, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<AuthSession>>([]);
    }

    private sealed class FakeAccountRepo(Account? account) : IAccountRepository
    {
        public Task<Account?> FindByIdAsync(Guid id, CancellationToken ct) => Task.FromResult(account?.Id == id ? account : null);
        public Task<Account?> FindByIdForUpdateAsync(Guid id, CancellationToken ct) => FindByIdAsync(id, ct);
        public Task<bool> EmailExistsAsync(string e, CancellationToken ct) => Task.FromResult(false);
        public Task<Account?> FindByEmailAsync(string e, CancellationToken ct) => Task.FromResult<Account?>(null);
        public Task<Account?> FindByEmailForUpdateAsync(string e, CancellationToken ct) => Task.FromResult<Account?>(null);
        public void Add(Account a) { }
        public Task<SaveAccountResult> SaveNewAccountAsync(CancellationToken ct) => Task.FromResult(SaveAccountResult.Success);
        public Task<bool> TrySaveChangesAsync(CancellationToken ct) => Task.FromResult(true);
        public Task SaveChangesAsync(CancellationToken ct) => Task.CompletedTask;
        public Task<TResult> ExecuteInTransactionAsync<TResult>(Func<Task<TResult>> action, CancellationToken ct) => action();
    }

    private sealed class FixedClock(DateTimeOffset now) : ParkingSystem.BuildingBlocks.Time.IClock
    {
        public DateTimeOffset UtcNow => now;
    }
}

public sealed class ActivateAccountTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 15, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Activates_suspended_account()
    {
        var userId = Guid.NewGuid();
        var account = Account.Register(userId, "user@test.com", "hash", null, Now);
        account.VerifyEmail();
        account.Suspend();

        var handler = new ActivateAccountCommandHandler(new FakeAccountRepo(account), new FakeIdentityAuditLog());
        var result = await handler.Handle(new ActivateAccountCommand(userId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(AccountStatus.EmailVerified, account.Status);
    }

    [Fact]
    public async Task Returns_not_found_when_account_does_not_exist()
    {
        var handler = new ActivateAccountCommandHandler(new FakeAccountRepo(null), new FakeIdentityAuditLog());
        var result = await handler.Handle(new ActivateAccountCommand(Guid.NewGuid()), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("identity.account_not_found", result.Error!.Code);
    }

    [Fact]
    public async Task Returns_error_when_account_is_not_suspended()
    {
        var userId = Guid.NewGuid();
        var account = Account.Register(userId, "user@test.com", "hash", null, Now);
        account.VerifyEmail();

        var handler = new ActivateAccountCommandHandler(new FakeAccountRepo(account), new FakeIdentityAuditLog());
        var result = await handler.Handle(new ActivateAccountCommand(userId), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("identity.account_not_suspended", result.Error!.Code);
    }

    private sealed class FakeAccountRepo(Account? account) : IAccountRepository
    {
        public Task<Account?> FindByIdAsync(Guid id, CancellationToken ct) => Task.FromResult(account?.Id == id ? account : null);
        public Task<Account?> FindByIdForUpdateAsync(Guid id, CancellationToken ct) => FindByIdAsync(id, ct);
        public Task<bool> EmailExistsAsync(string e, CancellationToken ct) => Task.FromResult(false);
        public Task<Account?> FindByEmailAsync(string e, CancellationToken ct) => Task.FromResult<Account?>(null);
        public Task<Account?> FindByEmailForUpdateAsync(string e, CancellationToken ct) => Task.FromResult<Account?>(null);
        public void Add(Account a) { }
        public Task<SaveAccountResult> SaveNewAccountAsync(CancellationToken ct) => Task.FromResult(SaveAccountResult.Success);
        public Task<bool> TrySaveChangesAsync(CancellationToken ct) => Task.FromResult(true);
        public Task SaveChangesAsync(CancellationToken ct) => Task.CompletedTask;
        public Task<TResult> ExecuteInTransactionAsync<TResult>(Func<Task<TResult>> action, CancellationToken ct) => action();
    }
}

public sealed class UnlockAccountCommandHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 15, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Returns_not_found_when_account_does_not_exist()
    {
        var handler = new UnlockAccountCommandHandler(new FakeAccountRepo(null), new FixedClock(Now), new FakeIdentityAuditLog());
        var result = await handler.Handle(new UnlockAccountCommand(Guid.NewGuid()), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("identity.account_not_found", result.Error!.Code);
    }

    [Fact]
    public async Task Reports_unlock_when_account_is_locked()
    {
        var userId = Guid.NewGuid();
        var account = Account.Register(userId, "user@test.com", "hash", null, Now.AddMinutes(-20));
        account.VerifyEmail();
        // Force a lockout that has not expired yet
        account.RecordFailedLogin(Now.AddMinutes(-1));
        account.RecordFailedLogin(Now.AddMinutes(-1));
        account.RecordFailedLogin(Now.AddMinutes(-1));
        Assert.True(account.IsLockedOut(Now));

        var handler = new UnlockAccountCommandHandler(new FakeAccountRepo(account), new FixedClock(Now), new FakeIdentityAuditLog());
        var result = await handler.Handle(new UnlockAccountCommand(userId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Contains("mở khóa", result.Value!.Message);
        Assert.False(account.IsLockedOut(Now));
        Assert.Equal(0, account.FailedLoginAttempts);
    }

    [Fact]
    public async Task Reports_reset_when_account_is_not_locked()
    {
        var userId = Guid.NewGuid();
        var account = Account.Register(userId, "user@test.com", "hash", null, Now);
        account.VerifyEmail();

        var handler = new UnlockAccountCommandHandler(new FakeAccountRepo(account), new FixedClock(Now), new FakeIdentityAuditLog());
        var result = await handler.Handle(new UnlockAccountCommand(userId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Contains("không bị khóa", result.Value!.Message);
    }

    private sealed class FakeAccountRepo(Account? account) : IAccountRepository
    {
        public Task<Account?> FindByIdAsync(Guid id, CancellationToken ct) => Task.FromResult(account?.Id == id ? account : null);
        public Task<Account?> FindByIdForUpdateAsync(Guid id, CancellationToken ct) => FindByIdAsync(id, ct);
        public Task<bool> EmailExistsAsync(string e, CancellationToken ct) => Task.FromResult(false);
        public Task<Account?> FindByEmailAsync(string e, CancellationToken ct) => Task.FromResult<Account?>(null);
        public Task<Account?> FindByEmailForUpdateAsync(string e, CancellationToken ct) => Task.FromResult<Account?>(null);
        public void Add(Account a) { }
        public Task<SaveAccountResult> SaveNewAccountAsync(CancellationToken ct) => Task.FromResult(SaveAccountResult.Success);
        public Task<bool> TrySaveChangesAsync(CancellationToken ct) => Task.FromResult(true);
        public Task SaveChangesAsync(CancellationToken ct) => Task.CompletedTask;
        public Task<TResult> ExecuteInTransactionAsync<TResult>(Func<Task<TResult>> action, CancellationToken ct) => action();
    }

    private sealed class FixedClock(DateTimeOffset now) : ParkingSystem.BuildingBlocks.Time.IClock
    {
        public DateTimeOffset UtcNow => now;
    }
}
