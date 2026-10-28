using ParkingSystem.BuildingBlocks.Time;
using ParkingSystem.Identity.Application.Accounts;
using ParkingSystem.Identity.Domain.Accounts;
using Xunit;

namespace ParkingSystem.Tests.Identity.UnitTests;

public sealed class AuthSessionManagerTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 15, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task RevokeSessionAsync_does_nothing_when_session_does_not_exist()
    {
        var sessions = new FakeAuthSessionRepository([]);
        var tokens = new FakeRefreshTokenRepository();
        var accounts = new FakeAccountRepository([]);

        var manager = new AuthSessionManager(sessions, tokens, accounts, new FixedClock(Now));
        // Should complete without throwing
        await manager.RevokeSessionAsync(Guid.NewGuid(), "test", CancellationToken.None);
        Assert.False(sessions.RevokedAllForAccount);
    }

    [Fact]
    public async Task RevokeSessionAsync_does_nothing_when_session_is_already_revoked()
    {
        var accountId = Guid.NewGuid();
        var session = AuthSession.Create(Guid.NewGuid(), accountId, null, null, Now, Now.AddDays(30));
        session.Revoke(Now, "AlreadyRevoked");
        var originalRevokedAt = session.RevokedAtUtc;

        var sessions = new FakeAuthSessionRepository([session]);
        var tokens = new FakeRefreshTokenRepository();
        var accounts = new FakeAccountRepository([]);

        var manager = new AuthSessionManager(sessions, tokens, accounts, new FixedClock(Now));
        await manager.RevokeSessionAsync(session.Id, "test", CancellationToken.None);

        // Revoke time must not change (idempotent)
        Assert.Equal(originalRevokedAt, session.RevokedAtUtc);
    }

    [Fact]
    public async Task RevokeSessionAsync_revokes_session_and_its_refresh_tokens()
    {
        var accountId = Guid.NewGuid();
        var session = AuthSession.Create(Guid.NewGuid(), accountId, "Chrome", "1.1.1.1", Now, Now.AddDays(30));
        var token = RefreshToken.Issue(Guid.NewGuid(), session.Id, accountId, "hash", Now, Now.AddDays(7));

        var sessions = new FakeAuthSessionRepository([session]);
        var tokens = new FakeRefreshTokenRepository([token]);
        var accounts = new FakeAccountRepository([]);

        var manager = new AuthSessionManager(sessions, tokens, accounts, new FixedClock(Now));
        await manager.RevokeSessionAsync(session.Id, "logout", CancellationToken.None);

        Assert.True(session.IsRevoked);
        Assert.True(token.IsRevoked);
    }

    [Fact]
    public async Task RevokeAllSessionsAsync_revokes_all_sessions_and_tokens_for_account()
    {
        var accountId = Guid.NewGuid();
        var s1 = AuthSession.Create(Guid.NewGuid(), accountId, "Chrome", "1.1.1.1", Now, Now.AddDays(30));
        var s2 = AuthSession.Create(Guid.NewGuid(), accountId, "Firefox", "1.1.1.2", Now, Now.AddDays(30));
        var t1 = RefreshToken.Issue(Guid.NewGuid(), s1.Id, accountId, "hash1", Now, Now.AddDays(7));
        var t2 = RefreshToken.Issue(Guid.NewGuid(), s2.Id, accountId, "hash2", Now, Now.AddDays(7));

        var sessions = new FakeAuthSessionRepository([s1, s2]);
        var tokens = new FakeRefreshTokenRepository([t1, t2]);
        var accounts = new FakeAccountRepository([]);

        var manager = new AuthSessionManager(sessions, tokens, accounts, new FixedClock(Now));
        await manager.RevokeAllSessionsAsync(accountId, "admin_suspend", CancellationToken.None);

        Assert.True(sessions.RevokedAllForAccount);
        Assert.True(tokens.RevokedAllForAccount);
    }

    private sealed class FixedClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset UtcNow => now;
    }

    private sealed class FakeAuthSessionRepository(IEnumerable<AuthSession> initial) : IAuthSessionRepository
    {
        private readonly List<AuthSession> _sessions = [.. initial];
        public bool RevokedAllForAccount { get; private set; }

        public void Add(AuthSession s) => _sessions.Add(s);

        public Task<AuthSession?> FindByIdAsync(Guid id, CancellationToken ct) =>
            Task.FromResult(_sessions.FirstOrDefault(s => s.Id == id));

        public Task<AuthSession?> FindByIdForUpdateAsync(Guid id, CancellationToken ct) =>
            FindByIdAsync(id, ct);

        public Task RevokeAllForAccountAsync(Guid accountId, DateTimeOffset now, string reason, CancellationToken ct)
        {
            RevokedAllForAccount = true;
            foreach (var s in _sessions.Where(s => s.AccountId == accountId && !s.IsRevoked))
                s.Revoke(now, reason);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<AuthSession>> ListActiveForAccountAsync(Guid accountId, DateTimeOffset now, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<AuthSession>>(
                _sessions.Where(s => s.AccountId == accountId && s.IsActive(now)).ToList());
    }

    private sealed class FakeRefreshTokenRepository(IEnumerable<RefreshToken>? initial = null) : IRefreshTokenRepository
    {
        private readonly List<RefreshToken> _tokens = [.. initial ?? []];
        public bool RevokedAllForAccount { get; private set; }

        public void Add(RefreshToken rt) => _tokens.Add(rt);

        public Task<RefreshToken?> FindByHashAsync(string hash, CancellationToken ct) =>
            Task.FromResult(_tokens.FirstOrDefault(rt => rt.TokenHash == hash));

        public Task<RefreshToken?> FindLatestBySessionIdAsync(Guid sessionId, CancellationToken ct) =>
            Task.FromResult(_tokens.Where(rt => rt.SessionId == sessionId).OrderByDescending(rt => rt.CreatedAtUtc).FirstOrDefault());

        public Task RevokeActiveTokensBySessionIdAsync(Guid sessionId, DateTimeOffset now, string reason, CancellationToken ct)
        {
            foreach (var rt in _tokens.Where(rt => rt.SessionId == sessionId && !rt.IsRevoked))
                rt.Revoke(now, reason);
            return Task.CompletedTask;
        }

        public Task RevokeAllForAccountAsync(Guid accountId, DateTimeOffset now, string reason, CancellationToken ct)
        {
            RevokedAllForAccount = true;
            foreach (var rt in _tokens.Where(rt => rt.AccountId == accountId && !rt.IsRevoked))
                rt.Revoke(now, reason);
            return Task.CompletedTask;
        }

        public Task ReloadAsync(RefreshToken refreshToken, CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class FakeAccountRepository(IEnumerable<Account> initial) : IAccountRepository
    {
        private readonly List<Account> _accounts = [.. initial];

        public Task<Account?> FindByIdAsync(Guid id, CancellationToken ct) =>
            Task.FromResult(_accounts.FirstOrDefault(a => a.Id == id));

        public Task<Account?> FindByIdForUpdateAsync(Guid id, CancellationToken ct) =>
            FindByIdAsync(id, ct);

        public Task<bool> EmailExistsAsync(string e, CancellationToken ct) => Task.FromResult(false);
        public Task<Account?> FindByEmailAsync(string e, CancellationToken ct) => Task.FromResult<Account?>(null);
        public Task<Account?> FindByEmailForUpdateAsync(string e, CancellationToken ct) => Task.FromResult<Account?>(null);
        public void Add(Account a) => _accounts.Add(a);
        public Task<SaveAccountResult> SaveNewAccountAsync(CancellationToken ct) => Task.FromResult(SaveAccountResult.Success);
        public Task<bool> TrySaveChangesAsync(CancellationToken ct) => Task.FromResult(true);
        public Task SaveChangesAsync(CancellationToken ct) => Task.CompletedTask;

        public async Task<TResult> ExecuteInTransactionAsync<TResult>(Func<Task<TResult>> action, CancellationToken ct) =>
            await action();
    }
}
