using ParkingSystem.BuildingBlocks.Time;
using ParkingSystem.Identity.Application.Accounts;
using ParkingSystem.Identity.Application.Accounts.Login;
using ParkingSystem.Identity.Application.Accounts.Logout;
using ParkingSystem.Identity.Application.Accounts.LogoutAll;
using ParkingSystem.Identity.Application.Accounts.TokenRefresh;
using ParkingSystem.Identity.Application.Common.Security;
using ParkingSystem.Identity.Domain.Accounts;
using Xunit;

namespace ParkingSystem.Tests.Identity.UnitTests;

public sealed class RefreshTokenAndLogoutTests
{
    private static readonly DateTimeOffset InitialTime = new(2026, 10, 7, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Refresh_succeeds_and_rotates_token_when_valid()
    {
        var clock = new MutableClock(InitialTime);
        var account = Account.Register(Guid.NewGuid(), "user@example.com", "hash", "+84901234567", clock.UtcNow);
        account.VerifyEmail();

        var session = AuthSession.Create(Guid.NewGuid(), account.Id, "Chrome", "1.1.1.1", clock.UtcNow, clock.UtcNow.AddDays(30));
        var initialToken = RefreshToken.Issue(Guid.NewGuid(), session.Id, account.Id, "hash:token-1", clock.UtcNow, session.CalculateNextTokenExpiration(clock.UtcNow));

        var accountRepo = new FakeAccountRepository([account]);
        var sessionRepo = new FakeAuthSessionRepository([session]);
        var tokenRepo = new FakeRefreshTokenRepository([initialToken]);
        var tokenGen = new FakeTokenGenerator("token-2", "hash:token-2");
        var jwtIssuer = new FakeJwtTokenIssuer("new-jwt-token", 900);

        var handler = new RefreshTokenCommandHandler(accountRepo, sessionRepo, tokenRepo, jwtIssuer, tokenGen, clock);

        clock.UtcNow = InitialTime.AddHours(1);
        var result = await handler.Handle(new RefreshTokenCommand("token-1"), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal("new-jwt-token", result.Value!.AccessToken);
        Assert.Equal("token-2", result.Value.RefreshToken);
        Assert.Equal(900, result.Value.ExpiresIn);

        // Initial token must be revoked
        Assert.True(initialToken.IsRevoked);
        Assert.Equal("Rotated", initialToken.RevokedReason);

        // New token must be active and linked to session
        var newToken = Assert.Single(tokenRepo.Saved, t => t.Id != initialToken.Id);
        Assert.Equal("hash:token-2", newToken.TokenHash);
        Assert.Equal(session.Id, newToken.SessionId);
        Assert.Equal(initialToken.Id, initialToken.Id);
        Assert.Equal(newToken.Id, initialToken.ReplacedByTokenId);

        // Session activity must be updated
        Assert.Equal(clock.UtcNow, session.LastActivityAtUtc);
    }

    [Fact]
    public async Task Refresh_fails_with_invalid_token_when_not_found()
    {
        var clock = new MutableClock(InitialTime);
        var accountRepo = new FakeAccountRepository([]);
        var sessionRepo = new FakeAuthSessionRepository([]);
        var tokenRepo = new FakeRefreshTokenRepository([]);
        var tokenGen = new FakeTokenGenerator("raw", "hash");
        var jwtIssuer = new FakeJwtTokenIssuer("jwt", 900);

        var handler = new RefreshTokenCommandHandler(accountRepo, sessionRepo, tokenRepo, jwtIssuer, tokenGen, clock);
        var result = await handler.Handle(new RefreshTokenCommand("unknown-token"), TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal(RefreshTokenErrors.InvalidRefreshToken, result.Error);
    }

    [Fact]
    public async Task Refresh_fails_when_idle_expired()
    {
        var clock = new MutableClock(InitialTime);
        var account = Account.Register(Guid.NewGuid(), "user@example.com", "hash", null, clock.UtcNow);
        account.VerifyEmail();

        var session = AuthSession.Create(Guid.NewGuid(), account.Id, null, null, clock.UtcNow, clock.UtcNow.AddDays(30));
        var token = RefreshToken.Issue(Guid.NewGuid(), session.Id, account.Id, "hash:token-1", clock.UtcNow, clock.UtcNow.AddDays(7));

        var accountRepo = new FakeAccountRepository([account]);
        var sessionRepo = new FakeAuthSessionRepository([session]);
        var tokenRepo = new FakeRefreshTokenRepository([token]);
        var tokenGen = new FakeTokenGenerator("token-2", "hash:token-2");
        var jwtIssuer = new FakeJwtTokenIssuer("jwt", 900);

        var handler = new RefreshTokenCommandHandler(accountRepo, sessionRepo, tokenRepo, jwtIssuer, tokenGen, clock);

        // Fast-forward 8 days (> 7 days idle)
        clock.UtcNow = InitialTime.AddDays(8);
        var result = await handler.Handle(new RefreshTokenCommand("token-1"), TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal(RefreshTokenErrors.RefreshTokenExpired, result.Error);
    }

    [Fact]
    public async Task Refresh_fails_when_session_reached_absolute_lifetime()
    {
        var clock = new MutableClock(InitialTime);
        var account = Account.Register(Guid.NewGuid(), "user@example.com", "hash", null, clock.UtcNow);
        account.VerifyEmail();

        var session = AuthSession.Create(Guid.NewGuid(), account.Id, null, null, clock.UtcNow, clock.UtcNow.AddDays(30));
        var token = RefreshToken.Issue(Guid.NewGuid(), session.Id, account.Id, "hash:token-1", clock.UtcNow, clock.UtcNow.AddDays(35));

        var accountRepo = new FakeAccountRepository([account]);
        var sessionRepo = new FakeAuthSessionRepository([session]);
        var tokenRepo = new FakeRefreshTokenRepository([token]);
        var tokenGen = new FakeTokenGenerator("token-2", "hash:token-2");
        var jwtIssuer = new FakeJwtTokenIssuer("jwt", 900);

        var handler = new RefreshTokenCommandHandler(accountRepo, sessionRepo, tokenRepo, jwtIssuer, tokenGen, clock);

        // Fast-forward 31 days (> 30 days absolute lifetime)
        clock.UtcNow = InitialTime.AddDays(31);
        var result = await handler.Handle(new RefreshTokenCommand("token-1"), TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal(RefreshTokenErrors.SessionExpired, result.Error);
    }

    [Fact]
    public async Task Refresh_detects_token_reuse_and_revokes_only_the_compromised_session()
    {
        var clock = new MutableClock(InitialTime);
        var account = Account.Register(Guid.NewGuid(), "user@example.com", "hash", null, clock.UtcNow);
        account.VerifyEmail();

        // Session 1: Compromised laptop
        var session1 = AuthSession.Create(Guid.NewGuid(), account.Id, "Laptop", null, clock.UtcNow, clock.UtcNow.AddDays(30));
        var tokenA = RefreshToken.Issue(Guid.NewGuid(), session1.Id, account.Id, "hash:token-A", clock.UtcNow, clock.UtcNow.AddDays(7));
        // Token A was already rotated to Token B 1 hour ago (outside grace window)
        tokenA.Revoke(clock.UtcNow.AddHours(-1), "Rotated", Guid.NewGuid(), "hash:token-B");

        // Session 2: Active mobile phone on same account
        var session2 = AuthSession.Create(Guid.NewGuid(), account.Id, "Mobile", null, clock.UtcNow, clock.UtcNow.AddDays(30));
        var tokenX = RefreshToken.Issue(Guid.NewGuid(), session2.Id, account.Id, "hash:token-X", clock.UtcNow, clock.UtcNow.AddDays(7));

        var accountRepo = new FakeAccountRepository([account]);
        var sessionRepo = new FakeAuthSessionRepository([session1, session2]);
        var tokenRepo = new FakeRefreshTokenRepository([tokenA, tokenX]);
        var tokenGen = new FakeTokenGenerator("token-C", "hash:token-C");
        var jwtIssuer = new FakeJwtTokenIssuer("jwt", 900);

        var handler = new RefreshTokenCommandHandler(accountRepo, sessionRepo, tokenRepo, jwtIssuer, tokenGen, clock);

        // Attacker replays Token A!
        var result = await handler.Handle(new RefreshTokenCommand("token-A"), TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal(RefreshTokenErrors.TokenReuseDetected, result.Error);

        // Session 1 MUST be revoked for security
        Assert.True(session1.IsRevoked);
        Assert.Equal("ReuseDetected", session1.RevokedReason);

        // Session 2 on mobile MUST REMAIN ACTIVE (Scoped reuse detection)
        Assert.False(session2.IsRevoked);
        Assert.True(tokenX.IsActive(clock.UtcNow));
    }

    [Fact]
    public async Task Refresh_detects_token_reuse_when_rotated_token_is_presented_again()
    {
        var clock = new MutableClock(InitialTime);
        var account = Account.Register(Guid.NewGuid(), "user@example.com", "hash", null, clock.UtcNow);
        account.VerifyEmail();

        var session = AuthSession.Create(Guid.NewGuid(), account.Id, "Laptop", null, clock.UtcNow, clock.UtcNow.AddDays(30));
        var tokenA = RefreshToken.Issue(Guid.NewGuid(), session.Id, account.Id, "hash:token-A", clock.UtcNow, clock.UtcNow.AddDays(7));

        var tokenB = RefreshToken.Issue(Guid.NewGuid(), session.Id, account.Id, "hash:token-B", clock.UtcNow, clock.UtcNow.AddDays(7));
        // Token A was rotated just 2 seconds ago to Token B
        tokenA.Revoke(clock.UtcNow.AddSeconds(-2), "Rotated", tokenB.Id, "hash:token-B");

        var accountRepo = new FakeAccountRepository([account]);
        var sessionRepo = new FakeAuthSessionRepository([session]);
        var tokenRepo = new FakeRefreshTokenRepository([tokenA, tokenB]);
        var tokenGen = new FakeTokenGenerator("token-C", "hash:token-C");
        var jwtIssuer = new FakeJwtTokenIssuer("recovered-jwt", 900);

        var handler = new RefreshTokenCommandHandler(accountRepo, sessionRepo, tokenRepo, jwtIssuer, tokenGen, clock);

        // Strict Single-Use Rotation: presenting token A again triggers TokenReuseDetected
        var result = await handler.Handle(new RefreshTokenCommand("token-A"), TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal(RefreshTokenErrors.TokenReuseDetected, result.Error);
        Assert.True(session.IsRevoked);
        Assert.Equal("ReuseDetected", session.RevokedReason);
        Assert.True(tokenB.IsRevoked);
        Assert.Equal("FamilyCompromised", tokenB.RevokedReason);
    }

    [Fact]
    public async Task Refresh_succeeds_even_when_account_is_temporarily_password_locked()
    {
        var clock = new MutableClock(InitialTime);
        var account = Account.Register(Guid.NewGuid(), "user@example.com", "hash", null, clock.UtcNow);
        account.VerifyEmail();
        // Simulate 3 failed login attempts on login form
        for (var i = 0; i < 3; i++)
        {
            account.RecordFailedLogin(clock.UtcNow);
        }
        Assert.True(account.IsLockedOut(clock.UtcNow));

        var session = AuthSession.Create(Guid.NewGuid(), account.Id, null, null, clock.UtcNow, clock.UtcNow.AddDays(30));
        var token = RefreshToken.Issue(Guid.NewGuid(), session.Id, account.Id, "hash:token-1", clock.UtcNow, clock.UtcNow.AddDays(7));

        var accountRepo = new FakeAccountRepository([account]);
        var sessionRepo = new FakeAuthSessionRepository([session]);
        var tokenRepo = new FakeRefreshTokenRepository([token]);
        var tokenGen = new FakeTokenGenerator("token-2", "hash:token-2");
        var jwtIssuer = new FakeJwtTokenIssuer("jwt", 900);

        var handler = new RefreshTokenCommandHandler(accountRepo, sessionRepo, tokenRepo, jwtIssuer, tokenGen, clock);

        // Refresh MUST succeed despite password lockout!
        var result = await handler.Handle(new RefreshTokenCommand("token-1"), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Refresh_fails_when_account_is_suspended()
    {
        var clock = new MutableClock(InitialTime);
        var account = Account.Register(Guid.NewGuid(), "user@example.com", "hash", null, clock.UtcNow);
        account.VerifyEmail();
        account.Suspend();

        var session = AuthSession.Create(Guid.NewGuid(), account.Id, null, null, clock.UtcNow, clock.UtcNow.AddDays(30));
        var token = RefreshToken.Issue(Guid.NewGuid(), session.Id, account.Id, "hash:token-1", clock.UtcNow, clock.UtcNow.AddDays(7));

        var accountRepo = new FakeAccountRepository([account]);
        var sessionRepo = new FakeAuthSessionRepository([session]);
        var tokenRepo = new FakeRefreshTokenRepository([token]);
        var tokenGen = new FakeTokenGenerator("token-2", "hash:token-2");
        var jwtIssuer = new FakeJwtTokenIssuer("jwt", 900);

        var handler = new RefreshTokenCommandHandler(accountRepo, sessionRepo, tokenRepo, jwtIssuer, tokenGen, clock);
        var result = await handler.Handle(new RefreshTokenCommand("token-1"), TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal(RefreshTokenErrors.AccountSuspended, result.Error);
        Assert.True(session.IsRevoked);
        Assert.Equal("AccountSuspended", session.RevokedReason);
        Assert.True(token.IsRevoked);
        Assert.Equal("AccountSuspended", token.RevokedReason);
    }

    [Fact]
    public async Task Refresh_fails_when_account_is_disabled()
    {
        var clock = new MutableClock(InitialTime);
        var account = Account.Register(Guid.NewGuid(), "user@example.com", "hash", null, clock.UtcNow);
        account.VerifyEmail();
        account.Disable();

        var session = AuthSession.Create(Guid.NewGuid(), account.Id, null, null, clock.UtcNow, clock.UtcNow.AddDays(30));
        var token = RefreshToken.Issue(Guid.NewGuid(), session.Id, account.Id, "hash:token-1", clock.UtcNow, clock.UtcNow.AddDays(7));

        var accountRepo = new FakeAccountRepository([account]);
        var sessionRepo = new FakeAuthSessionRepository([session]);
        var tokenRepo = new FakeRefreshTokenRepository([token]);
        var tokenGen = new FakeTokenGenerator("token-2", "hash:token-2");
        var jwtIssuer = new FakeJwtTokenIssuer("jwt", 900);

        var handler = new RefreshTokenCommandHandler(accountRepo, sessionRepo, tokenRepo, jwtIssuer, tokenGen, clock);
        var result = await handler.Handle(new RefreshTokenCommand("token-1"), TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal(RefreshTokenErrors.AccountDisabled, result.Error);
        Assert.True(session.IsRevoked);
        Assert.Equal("AccountDisabled", session.RevokedReason);
        Assert.True(token.IsRevoked);
        Assert.Equal("AccountDisabled", token.RevokedReason);
    }

    [Fact]
    public async Task Refresh_fails_when_account_is_not_email_verified()
    {
        var clock = new MutableClock(InitialTime);
        var account = Account.Register(Guid.NewGuid(), "user@example.com", "hash", null, clock.UtcNow);

        var session = AuthSession.Create(Guid.NewGuid(), account.Id, null, null, clock.UtcNow, clock.UtcNow.AddDays(30));
        var token = RefreshToken.Issue(Guid.NewGuid(), session.Id, account.Id, "hash:token-1", clock.UtcNow, clock.UtcNow.AddDays(7));

        var accountRepo = new FakeAccountRepository([account]);
        var sessionRepo = new FakeAuthSessionRepository([session]);
        var tokenRepo = new FakeRefreshTokenRepository([token]);
        var tokenGen = new FakeTokenGenerator("token-2", "hash:token-2");
        var jwtIssuer = new FakeJwtTokenIssuer("jwt", 900);

        var handler = new RefreshTokenCommandHandler(accountRepo, sessionRepo, tokenRepo, jwtIssuer, tokenGen, clock);
        var result = await handler.Handle(new RefreshTokenCommand("token-1"), TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal(RefreshTokenErrors.EmailNotVerified, result.Error);
        Assert.True(session.IsRevoked);
        Assert.Equal("EmailNotVerified", session.RevokedReason);
        Assert.True(token.IsRevoked);
        Assert.Equal("EmailNotVerified", token.RevokedReason);
    }

    [Fact]
    public async Task Logout_revokes_session_and_token_idempotently()
    {
        var clock = new MutableClock(InitialTime);
        var session = AuthSession.Create(Guid.NewGuid(), Guid.NewGuid(), null, null, clock.UtcNow, clock.UtcNow.AddDays(30));
        var token = RefreshToken.Issue(Guid.NewGuid(), session.Id, session.AccountId, "hash:token-1", clock.UtcNow, clock.UtcNow.AddDays(7));

        var accountRepo = new FakeAccountRepository([]);
        var sessionRepo = new FakeAuthSessionRepository([session]);
        var tokenRepo = new FakeRefreshTokenRepository([token]);
        var tokenGen = new FakeTokenGenerator("token-1", "hash:token-1");

        var handler = new LogoutCommandHandler(accountRepo, sessionRepo, tokenRepo, tokenGen, clock);
        var result = await handler.Handle(new LogoutCommand("token-1"), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal("Logged out successfully.", result.Value!.Message);
        Assert.True(session.IsRevoked);
        Assert.Equal("Logout", session.RevokedReason);
        Assert.True(token.IsRevoked);

        // Calling logout again returns 200 OK (Idempotent)
        var secondResult = await handler.Handle(new LogoutCommand("token-1"), TestContext.Current.CancellationToken);
        Assert.True(secondResult.IsSuccess);
    }

    [Fact]
    public async Task LogoutAll_revokes_all_sessions_for_account()
    {
        var accountId = Guid.NewGuid();
        var clock = new MutableClock(InitialTime);
        var session1 = AuthSession.Create(Guid.NewGuid(), accountId, "Laptop", null, clock.UtcNow, clock.UtcNow.AddDays(30));
        var session2 = AuthSession.Create(Guid.NewGuid(), accountId, "Mobile", null, clock.UtcNow, clock.UtcNow.AddDays(30));
        var token1 = RefreshToken.Issue(Guid.NewGuid(), session1.Id, accountId, "hash:token-1", clock.UtcNow, clock.UtcNow.AddDays(7));
        var token2 = RefreshToken.Issue(Guid.NewGuid(), session2.Id, accountId, "hash:token-2", clock.UtcNow, clock.UtcNow.AddDays(7));

        var sessionRepo = new FakeAuthSessionRepository([session1, session2]);
        var tokenRepo = new FakeRefreshTokenRepository([token1, token2]);
        var accountRepo = new FakeAccountRepository([]);

        var sessionManager = new AuthSessionManager(sessionRepo, tokenRepo, accountRepo, clock);
        var handler = new LogoutAllCommandHandler(sessionManager);

        var result = await handler.Handle(new LogoutAllCommand(accountId), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal("All active sessions have been logged out successfully.", result.Value!.Message);
        Assert.True(session1.IsRevoked);
        Assert.Equal("LogoutAll", session1.RevokedReason);
        Assert.True(session2.IsRevoked);
        Assert.Equal("LogoutAll", session2.RevokedReason);
        Assert.True(token1.IsRevoked);
        Assert.Equal("LogoutAll", token1.RevokedReason);
        Assert.True(token2.IsRevoked);
        Assert.Equal("LogoutAll", token2.RevokedReason);
    }

    private sealed class MutableClock(DateTimeOffset utcNow) : IClock
    {
        public DateTimeOffset UtcNow { get; set; } = utcNow;
    }

    private sealed class FakeTokenGenerator(string rawToken, string tokenHash) : IRefreshTokenGenerator
    {
        public (string RawToken, string TokenHash) GenerateToken() => (rawToken, tokenHash);

        public string HashToken(string rawToken) => "hash:" + rawToken.Trim();
    }

    private sealed class FakeJwtTokenIssuer(string token, int expirationSeconds) : IJwtTokenIssuer
    {
        public int AccessTokenExpirationSeconds => expirationSeconds;

        public string IssueAccessToken(Account account, DateTimeOffset nowUtc) => token;
    }

    private sealed class FakeAccountRepository(IEnumerable<Account> initial) : IAccountRepository
    {
        private readonly List<Account> accounts = [..initial];

        public Task<bool> EmailExistsAsync(string normalizedEmail, CancellationToken cancellationToken) =>
            Task.FromResult(accounts.Any(a => a.Email == normalizedEmail));

        public Task<Account?> FindByEmailAsync(string normalizedEmail, CancellationToken cancellationToken) =>
            Task.FromResult(accounts.FirstOrDefault(a => a.Email == normalizedEmail));

        public Task<Account?> FindByEmailForUpdateAsync(string normalizedEmail, CancellationToken cancellationToken) =>
            FindByEmailAsync(normalizedEmail, cancellationToken);

        public Task<Account?> FindByIdAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult(accounts.FirstOrDefault(a => a.Id == id));

        public void Add(Account account) => accounts.Add(account);

        public Task<bool> TrySaveChangesAsync(CancellationToken cancellationToken) => Task.FromResult(true);

        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<TResult> ExecuteInTransactionAsync<TResult>(Func<Task<TResult>> action, CancellationToken cancellationToken) =>
            action();
    }

    private sealed class FakeAuthSessionRepository(IEnumerable<AuthSession>? initial = null) : IAuthSessionRepository
    {
        public List<AuthSession> Saved { get; } = [.. initial ?? []];

        public void Add(AuthSession session) => Saved.Add(session);

        public Task<AuthSession?> FindByIdAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult(Saved.FirstOrDefault(s => s.Id == id));

        public Task<AuthSession?> FindByIdForUpdateAsync(Guid id, CancellationToken cancellationToken) =>
            FindByIdAsync(id, cancellationToken);

        public Task RevokeAllForAccountAsync(Guid accountId, DateTimeOffset nowUtc, string reason, CancellationToken cancellationToken)
        {
            foreach (var s in Saved.Where(s => s.AccountId == accountId && !s.IsRevoked))
            {
                s.Revoke(nowUtc, reason);
            }
            return Task.CompletedTask;
        }
    }

    private sealed class FakeRefreshTokenRepository(IEnumerable<RefreshToken>? initial = null) : IRefreshTokenRepository
    {
        public List<RefreshToken> Saved { get; } = [.. initial ?? []];

        public void Add(RefreshToken refreshToken) => Saved.Add(refreshToken);

        public Task<RefreshToken?> FindByHashAsync(string tokenHash, CancellationToken cancellationToken) =>
            Task.FromResult(Saved.FirstOrDefault(rt => rt.TokenHash == tokenHash));

        public Task<RefreshToken?> FindLatestBySessionIdAsync(Guid sessionId, CancellationToken cancellationToken) =>
            Task.FromResult(Saved.Where(rt => rt.SessionId == sessionId && !rt.IsRevoked).OrderByDescending(rt => rt.CreatedAtUtc).FirstOrDefault());

        public Task RevokeActiveTokensBySessionIdAsync(Guid sessionId, DateTimeOffset nowUtc, string reason, CancellationToken cancellationToken)
        {
            foreach (var rt in Saved.Where(rt => rt.SessionId == sessionId && !rt.IsRevoked))
            {
                rt.Revoke(nowUtc, reason);
            }
            return Task.CompletedTask;
        }

        public Task RevokeAllForAccountAsync(Guid accountId, DateTimeOffset nowUtc, string reason, CancellationToken cancellationToken)
        {
            foreach (var rt in Saved.Where(rt => rt.AccountId == accountId && !rt.IsRevoked))
            {
                rt.Revoke(nowUtc, reason);
            }
            return Task.CompletedTask;
        }
    }
}
