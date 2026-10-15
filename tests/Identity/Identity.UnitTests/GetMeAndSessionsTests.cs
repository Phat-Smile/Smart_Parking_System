using ParkingSystem.Identity.Application.Accounts;
using ParkingSystem.Identity.Application.Accounts.GetMe;
using ParkingSystem.Identity.Application.Accounts.ListSessions;
using ParkingSystem.Identity.Application.Accounts.RevokeSession;
using ParkingSystem.Identity.Application.Profiles;
using ParkingSystem.Identity.Domain.Accounts;
using ParkingSystem.Identity.Domain.Profiles;
using Xunit;

namespace ParkingSystem.Tests.Identity.UnitTests;

public sealed class GetMeTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 15, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Returns_full_profile_for_authenticated_user()
    {
        var accountId = Guid.NewGuid();
        var account = Account.Register(accountId, "user@test.com", "hashedpw", "+84901234567", Now);
        account.VerifyEmail();
        var profile = Profile.Create(accountId, "Nguyễn Văn A", Now);

        var handler = new GetMeQueryHandler(
            new FakeAccountRepository(account),
            new FakeProfileRepository(profile));

        var result = await handler.Handle(new GetMeQuery(accountId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("user@test.com", result.Value!.Email);
        Assert.Equal("Nguyễn Văn A", result.Value.FullName);
        Assert.Equal("+84901234567", result.Value.PhoneNumber);
        Assert.True(result.Value.HasPassword);
        Assert.False(result.Value.GoogleLinked);
        Assert.Equal("EmailVerified", result.Value.Status);
        Assert.Equal("Customer", result.Value.Role);
    }

    [Fact]
    public async Task Returns_null_fullName_when_profile_not_set()
    {
        var accountId = Guid.NewGuid();
        var account = Account.Register(accountId, "noname@test.com", "hashedpw", null, Now);
        account.VerifyEmail();

        var handler = new GetMeQueryHandler(
            new FakeAccountRepository(account),
            new FakeProfileRepository(null));

        var result = await handler.Handle(new GetMeQuery(accountId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value!.FullName);
    }

    [Fact]
    public async Task Returns_failure_when_account_not_found()
    {
        var handler = new GetMeQueryHandler(
            new FakeAccountRepository(null),
            new FakeProfileRepository(null));

        var result = await handler.Handle(new GetMeQuery(Guid.NewGuid()), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("identity.account_not_found", result.Error!.Code);
    }

    [Fact]
    public async Task Returns_hasPassword_false_for_google_only_account()
    {
        var accountId = Guid.NewGuid();
        var account = Account.RegisterWithGoogle(accountId, "google@test.com", "google-sub-123", Now);

        var handler = new GetMeQueryHandler(
            new FakeAccountRepository(account),
            new FakeProfileRepository(null));

        var result = await handler.Handle(new GetMeQuery(accountId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value!.HasPassword);
        Assert.True(result.Value.GoogleLinked);
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

    private sealed class FakeProfileRepository(Profile? profile) : IProfileRepository
    {
        public void Add(Profile p) { }
        public Task<Profile?> FindByAccountIdAsync(Guid accountId, CancellationToken ct) =>
            Task.FromResult(profile?.AccountId == accountId ? profile : null);
        public Task SaveChangesAsync(CancellationToken ct) => Task.CompletedTask;
    }
}

public sealed class ListSessionsTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 15, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Returns_only_active_sessions_for_account()
    {
        var accountId = Guid.NewGuid();
        var active = AuthSession.Create(Guid.NewGuid(), accountId, "Chrome", "1.2.3.4", Now, Now.AddDays(30));
        var revoked = AuthSession.Create(Guid.NewGuid(), accountId, "Firefox", "1.2.3.5", Now, Now.AddDays(30));
        revoked.Revoke(Now, "Test");
        var expired = AuthSession.Create(Guid.NewGuid(), accountId, "Safari", "1.2.3.6", Now.AddDays(-31), Now.AddDays(-1));
        var otherUser = AuthSession.Create(Guid.NewGuid(), Guid.NewGuid(), "Edge", "1.2.3.7", Now, Now.AddDays(30));

        var handler = new ListSessionsQueryHandler(
            new FakeAuthSessionRepo([active, revoked, expired, otherUser]),
            new FixedClock(Now));

        var result = await handler.Handle(new ListSessionsQuery(accountId, active.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value!.Sessions);
        Assert.Equal(active.Id, result.Value.Sessions[0].Id);
        Assert.True(result.Value.Sessions[0].IsCurrent);
    }

    [Fact]
    public async Task Returns_empty_list_when_no_active_sessions()
    {
        var handler = new ListSessionsQueryHandler(
            new FakeAuthSessionRepo([]),
            new FixedClock(Now));

        var result = await handler.Handle(new ListSessionsQuery(Guid.NewGuid(), null), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value!.Sessions);
    }

    private sealed class FakeAuthSessionRepo(IEnumerable<AuthSession> sessions) : IAuthSessionRepository
    {
        private readonly List<AuthSession> _sessions = [.. sessions];

        public void Add(AuthSession s) => _sessions.Add(s);
        public Task<AuthSession?> FindByIdAsync(Guid id, CancellationToken ct) =>
            Task.FromResult(_sessions.FirstOrDefault(s => s.Id == id));
        public Task<AuthSession?> FindByIdForUpdateAsync(Guid id, CancellationToken ct) => FindByIdAsync(id, ct);
        public Task RevokeAllForAccountAsync(Guid accountId, DateTimeOffset now, string reason, CancellationToken ct) => Task.CompletedTask;
        public Task<IReadOnlyList<AuthSession>> ListActiveForAccountAsync(Guid accountId, DateTimeOffset now, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<AuthSession>>(
                _sessions.Where(s => s.AccountId == accountId && s.IsActive(now)).ToList());
    }

    private sealed class FixedClock(DateTimeOffset now) : ParkingSystem.BuildingBlocks.Time.IClock
    {
        public DateTimeOffset UtcNow => now;
    }
}

public sealed class RevokeSessionTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 15, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Revokes_session_belonging_to_current_user()
    {
        var accountId = Guid.NewGuid();
        var session = AuthSession.Create(Guid.NewGuid(), accountId, "Chrome", "1.1.1.1", Now, Now.AddDays(30));
        var repo = new FakeSessionRepo([session]);
        var accounts = new FakeAccountRepo();

        var handler = new RevokeSessionCommandHandler(repo, accounts, new FixedClock(Now));
        var result = await handler.Handle(new RevokeSessionCommand(accountId, session.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(session.IsRevoked);
    }

    [Fact]
    public async Task Returns_not_found_when_session_belongs_to_different_user()
    {
        var accountId = Guid.NewGuid();
        var otherAccountId = Guid.NewGuid();
        var session = AuthSession.Create(Guid.NewGuid(), otherAccountId, "Chrome", "1.1.1.1", Now, Now.AddDays(30));
        var repo = new FakeSessionRepo([session]);
        var accounts = new FakeAccountRepo();

        var handler = new RevokeSessionCommandHandler(repo, accounts, new FixedClock(Now));
        var result = await handler.Handle(new RevokeSessionCommand(accountId, session.Id), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("identity.session_not_found", result.Error!.Code);
    }

    [Fact]
    public async Task Returns_not_found_when_session_already_revoked()
    {
        var accountId = Guid.NewGuid();
        var session = AuthSession.Create(Guid.NewGuid(), accountId, "Chrome", "1.1.1.1", Now, Now.AddDays(30));
        session.Revoke(Now, "AlreadyRevoked");
        var repo = new FakeSessionRepo([session]);
        var accounts = new FakeAccountRepo();

        var handler = new RevokeSessionCommandHandler(repo, accounts, new FixedClock(Now));
        var result = await handler.Handle(new RevokeSessionCommand(accountId, session.Id), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("identity.session_not_found", result.Error!.Code);
    }

    private sealed class FakeSessionRepo(IEnumerable<AuthSession> sessions) : IAuthSessionRepository
    {
        private readonly List<AuthSession> _sessions = [.. sessions];

        public void Add(AuthSession s) => _sessions.Add(s);
        public Task<AuthSession?> FindByIdAsync(Guid id, CancellationToken ct) =>
            Task.FromResult(_sessions.FirstOrDefault(s => s.Id == id));
        public Task<AuthSession?> FindByIdForUpdateAsync(Guid id, CancellationToken ct) => FindByIdAsync(id, ct);
        public Task RevokeAllForAccountAsync(Guid accountId, DateTimeOffset now, string reason, CancellationToken ct) => Task.CompletedTask;
        public Task<IReadOnlyList<AuthSession>> ListActiveForAccountAsync(Guid accountId, DateTimeOffset now, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<AuthSession>>([]);
    }

    private sealed class FakeAccountRepo : IAccountRepository
    {
        public Task<Account?> FindByIdAsync(Guid id, CancellationToken ct) => Task.FromResult<Account?>(null);
        public Task<Account?> FindByIdForUpdateAsync(Guid id, CancellationToken ct) => Task.FromResult<Account?>(null);
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
