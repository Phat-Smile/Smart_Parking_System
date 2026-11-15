using ParkingSystem.BuildingBlocks.Time;
using ParkingSystem.Identity.Application.Accounts;
using ParkingSystem.Identity.Application.Accounts.ChangeAccountRole;
using ParkingSystem.Identity.Application.Accounts.UnlockAccount;
using ParkingSystem.Identity.Domain.Accounts;
using Xunit;

namespace ParkingSystem.Tests.Identity.UnitTests;

public sealed class ChangeAccountRoleCommandHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 25, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Admin_cannot_change_own_role()
    {
        var adminId = Guid.NewGuid();
        var handler = CreateHandler(null, out _, out _);

        var result = await handler.Handle(
            new ChangeAccountRoleCommand(adminId, adminId, "Staff"),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ChangeAccountRoleErrors.CannotChangeOwnRole.Code, result.Error!.Code);
    }

    [Fact]
    public async Task Returns_not_found_when_account_does_not_exist()
    {
        var adminId = Guid.NewGuid();
        var targetId = Guid.NewGuid();
        var handler = CreateHandler(null, out _, out _);

        var result = await handler.Handle(
            new ChangeAccountRoleCommand(adminId, targetId, "Staff"),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(UnlockAccountErrors.AccountNotFound.Code, result.Error!.Code);
    }

    [Fact]
    public async Task Returns_success_without_revoking_sessions_when_role_is_unchanged()
    {
        var adminId = Guid.NewGuid();
        var targetId = Guid.NewGuid();
        var account = Account.Register(targetId, "staff@test.com", "hash", null, Now);
        account.VerifyEmail();
        account.AssignRole(AccountRole.Staff);

        var handler = CreateHandler(account, out var sessionRepo, out var refreshRepo);

        var result = await handler.Handle(
            new ChangeAccountRoleCommand(adminId, targetId, "Staff"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Contains("không có gì thay đổi", result.Value!.Message);
        Assert.False(sessionRepo.RevokedAll);
        Assert.False(refreshRepo.RevokedAll);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(OwnerApplicationStatus.Pending)]
    [InlineData(OwnerApplicationStatus.Rejected)]
    public async Task Blocks_direct_assignment_of_parking_lot_owner_when_not_approved(OwnerApplicationStatus? status)
    {
        var adminId = Guid.NewGuid();
        var targetId = Guid.NewGuid();
        var isOwnerApplicant = status != null;
        var account = Account.Register(targetId, "customer@test.com", "hash", null, Now, applyAsParkingLotOwner: isOwnerApplicant);
        account.VerifyEmail();

        if (status == OwnerApplicationStatus.Rejected)
        {
            account.RejectOwnerApplication(adminId, "Hồ sơ không hợp lệ", Now);
        }

        var handler = CreateHandler(account, out var sessionRepo, out var refreshRepo);

        var result = await handler.Handle(
            new ChangeAccountRoleCommand(adminId, targetId, "ParkingLotOwner"),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ChangeAccountRoleErrors.OwnerRoleRequiresApplication.Code, result.Error!.Code);
        Assert.Equal(AccountRole.Customer, account.Role);
        Assert.False(sessionRepo.RevokedAll);
        Assert.False(refreshRepo.RevokedAll);
    }

    [Fact]
    public async Task Same_role_as_parking_lot_owner_returns_success_without_changes()
    {
        var adminId = Guid.NewGuid();
        var targetId = Guid.NewGuid();
        var account = Account.Register(targetId, "owner@test.com", "hash", null, Now, applyAsParkingLotOwner: true);
        account.VerifyEmail();
        account.ApproveOwnerApplication(adminId, Now);
        Assert.Equal(AccountRole.ParkingLotOwner, account.Role);
        Assert.Equal(OwnerApplicationStatus.Approved, account.OwnerApplicationStatus);

        var handler = CreateHandler(account, out var sessionRepo, out var refreshRepo);

        var result = await handler.Handle(
            new ChangeAccountRoleCommand(adminId, targetId, "ParkingLotOwner"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Contains("không có gì thay đổi", result.Value!.Message);
        Assert.False(sessionRepo.RevokedAll);
        Assert.False(refreshRepo.RevokedAll);
    }

    [Theory]
    [InlineData("Staff", AccountRole.Staff)]
    [InlineData("SystemAdmin", AccountRole.SystemAdmin)]
    [InlineData("Customer", AccountRole.Customer)]
    public async Task Changes_role_and_revokes_all_sessions_and_tokens_for_valid_roles(string roleName, AccountRole expectedRole)
    {
        var adminId = Guid.NewGuid();
        var targetId = Guid.NewGuid();
        var account = Account.Register(targetId, "user@test.com", "hash", null, Now);
        account.VerifyEmail();
        if (expectedRole == AccountRole.Customer)
        {
            account.AssignRole(AccountRole.Staff);
        }

        var handler = CreateHandler(account, out var sessionRepo, out var refreshRepo);

        var result = await handler.Handle(
            new ChangeAccountRoleCommand(adminId, targetId, roleName),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(expectedRole, account.Role);
        Assert.True(sessionRepo.RevokedAll);
        Assert.True(refreshRepo.RevokedAll);
    }

    private static ChangeAccountRoleCommandHandler CreateHandler(
        Account? account,
        out FakeSessionRepository sessionRepo,
        out FakeRefreshTokenRepository refreshRepo)
    {
        sessionRepo = new FakeSessionRepository();
        refreshRepo = new FakeRefreshTokenRepository();
        var accountRepo = new FakeAccountRepository(account);
        var clock = new FixedClock(Now);
        return new ChangeAccountRoleCommandHandler(accountRepo, sessionRepo, refreshRepo, clock, new FakeIdentityAuditLog());
    }

    private sealed class FakeSessionRepository : IAuthSessionRepository
    {
        public bool RevokedAll { get; private set; }

        public void Add(AuthSession s) { }
        public Task<AuthSession?> FindByIdAsync(Guid id, CancellationToken ct) => Task.FromResult<AuthSession?>(null);
        public Task<AuthSession?> FindByIdForUpdateAsync(Guid id, CancellationToken ct) => Task.FromResult<AuthSession?>(null);
        public Task RevokeAllForAccountAsync(Guid acctId, DateTimeOffset n, string reason, CancellationToken ct)
        {
            RevokedAll = true;
            return Task.CompletedTask;
        }
        public Task<IReadOnlyList<AuthSession>> ListActiveForAccountAsync(Guid acctId, DateTimeOffset n, CancellationToken ct) =>
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

    private sealed class FakeAccountRepository(Account? account) : IAccountRepository
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

    private sealed class FixedClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset UtcNow => now;
    }
}
