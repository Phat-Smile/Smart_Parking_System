using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ParkingSystem.BuildingBlocks.Results;
using ParkingSystem.BuildingBlocks.Time;
using ParkingSystem.Identity.Application.Accounts;
using ParkingSystem.Identity.Application.Accounts.Login;
using ParkingSystem.Identity.Application.Accounts.LogoutAll;
using ParkingSystem.Identity.Application.Accounts.RegisterAccount;
using ParkingSystem.Identity.Application.Accounts.TokenRefresh;
using ParkingSystem.Identity.Application.Common.Messaging.Outbox;
using ParkingSystem.Identity.Application.Common.Security;
using ParkingSystem.Identity.Application.Profiles;
using ParkingSystem.Identity.Domain.Accounts;
using ParkingSystem.Identity.Infrastructure.Security;
using ParkingSystem.Identity.Persistence;
using ParkingSystem.Testing;
using Xunit;

namespace ParkingSystem.Tests.Identity.IntegrationTests;

// Races the workbook asks for that the other concurrency tests do not cover: concurrent wrong passwords,
// concurrent registrations of one email and logout-all against a refresh. Each call runs the real handler
// on its own DbContext/connection, so PostgreSQL row locks and unique indexes decide the outcome.
public sealed class AuthConcurrencyIntegrationTests(PostgreSqlContainerFixture postgres) : IClassFixture<PostgreSqlContainerFixture>
{
    private static readonly DateTimeOffset Now = new(2026, 10, 9, 12, 0, 0, TimeSpan.Zero);
    private readonly RefreshTokenGenerator _tokenGenerator = new();

    [Fact]
    public async Task Concurrent_wrong_passwords_are_all_counted_and_lock_the_account()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var provider = await CreateProviderAsync(cancellationToken);
        var accountId = await SeedVerifiedAccountAsync(provider, "login-race@example.com", cancellationToken);

        var results = await Task.WhenAll(Enumerable.Range(0, Account.MaxFailedAttemptsBeforeLockout)
            .Select(_ => LoginAsync(provider, "login-race@example.com", cancellationToken)));

        // The FOR UPDATE lock serialises the attempts: none is lost and only the last one reports the lockout.
        Assert.Equal(Account.MaxFailedAttemptsBeforeLockout - 1, results.Count(r => r.Error?.Code == LoginErrors.InvalidCredentials.Code));
        Assert.Single(results, r => r.Error?.Code == LoginErrors.AccountTemporarilyLocked.Code);

        await using var scope = provider.CreateAsyncScope();
        var account = await scope.ServiceProvider.GetRequiredService<IdentityDbContext>().Accounts.AsNoTracking()
            .SingleAsync(a => a.Id == accountId, cancellationToken);
        Assert.Equal(Account.MaxFailedAttemptsBeforeLockout, account.FailedLoginAttempts);
        Assert.Equal(Now.AddMinutes(Account.LockoutDurationMinutes), account.LockoutEndUtc);
    }

    [Fact]
    public async Task Concurrent_registrations_of_one_email_create_one_account_one_code_and_one_event()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var provider = await CreateProviderAsync(cancellationToken);
        const string email = "register-race@example.com";

        var results = await Task.WhenAll(
            RegisterAsync(provider, email, cancellationToken),
            RegisterAsync(provider, email.ToUpperInvariant(), cancellationToken));

        // Whether the loser sees the committed row or hits the unique index, it gets the same 409 code.
        Assert.Single(results, r => r.IsSuccess);
        Assert.Single(results, r => r.Error?.Code == RegisterAccountErrors.EmailAlreadyRegistered.Code);

        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var account = await db.Accounts.AsNoTracking().SingleAsync(a => a.Email == email, cancellationToken);
        Assert.Equal(1, await db.EmailVerifications.CountAsync(v => v.AccountId == account.Id, cancellationToken));
        // payload is jsonb, so it is matched in memory rather than with a SQL string function.
        var otpEvents = await db.OutboxMessages.AsNoTracking()
            .Where(m => m.EventType == "identity.user-verification-otp-issued.v1")
            .Select(m => m.Payload)
            .ToListAsync(cancellationToken);
        Assert.Single(otpEvents, payload => payload.Contains(account.Id.ToString(), StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Logout_all_racing_a_refresh_leaves_no_active_session_or_token()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var provider = await CreateProviderAsync(cancellationToken);
        var accountId = await SeedVerifiedAccountAsync(provider, "logout-all-race@example.com", cancellationToken);
        var firstToken = await SeedSessionAsync(provider, accountId, cancellationToken);
        await SeedSessionAsync(provider, accountId, cancellationToken);

        await Task.WhenAll(
            RefreshAsync(provider, firstToken, cancellationToken),
            LogoutAllAsync(provider, accountId, cancellationToken));

        // A token rotated just before logout-all must be revoked by it; after it, the refresh is refused.
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var sessions = await db.AuthSessions.AsNoTracking().Where(s => s.AccountId == accountId).ToListAsync(cancellationToken);
        var tokens = await db.RefreshTokens.AsNoTracking().Where(t => t.AccountId == accountId).ToListAsync(cancellationToken);
        Assert.Equal(2, sessions.Count);
        Assert.All(sessions, session => Assert.True(session.IsRevoked));
        Assert.All(tokens, token => Assert.True(token.IsRevoked));
    }

    private async Task<ServiceProvider> CreateProviderAsync(CancellationToken cancellationToken)
    {
        var services = new ServiceCollection();
        services.AddPersistence(PostgreSqlTestConfiguration.Create(postgres.ConnectionString));
        services.AddSingleton<IClock>(new FixedClock());
        var provider = services.BuildServiceProvider(validateScopes: true);

        await using var scope = provider.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IdentityDbContext>().Database.MigrateAsync(cancellationToken);
        return provider;
    }

    private static async Task<Guid> SeedVerifiedAccountAsync(ServiceProvider provider, string email, CancellationToken cancellationToken)
    {
        await using var scope = provider.CreateAsyncScope();
        var accounts = scope.ServiceProvider.GetRequiredService<IAccountRepository>();
        var account = Account.Register(Guid.NewGuid(), email, "hash", null, Now);
        account.VerifyEmail();
        accounts.Add(account);
        await accounts.SaveChangesAsync(cancellationToken);
        return account.Id;
    }

    private async Task<string> SeedSessionAsync(ServiceProvider provider, Guid accountId, CancellationToken cancellationToken)
    {
        await using var scope = provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var session = AuthSession.Create(Guid.NewGuid(), accountId, "Test Device", "127.0.0.1", Now, Now.AddDays(AuthSession.DefaultAbsoluteLifetimeDays));
        services.GetRequiredService<IAuthSessionRepository>().Add(session);

        var (rawToken, tokenHash) = _tokenGenerator.GenerateToken();
        services.GetRequiredService<IRefreshTokenRepository>()
            .Add(RefreshToken.Issue(Guid.NewGuid(), session.Id, accountId, tokenHash, Now, session.CalculateNextTokenExpiration(Now)));

        await services.GetRequiredService<IAccountRepository>().SaveChangesAsync(cancellationToken);
        return rawToken;
    }

    private static async Task<Result<LoginResponse>> LoginAsync(ServiceProvider provider, string email, CancellationToken cancellationToken)
    {
        await using var scope = provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var handler = new LoginCommandHandler(
            services.GetRequiredService<IAccountRepository>(),
            services.GetRequiredService<IAuthSessionRepository>(),
            services.GetRequiredService<IRefreshTokenRepository>(),
            new RejectingPasswordHasher(),
            new StubJwtTokenIssuer(),
            new RefreshTokenGenerator(),
            new FixedClock());
        return await handler.Handle(new LoginCommand(email, "Wrong@1234"), cancellationToken);
    }

    private static async Task<Result<RegisteredAccount>> RegisterAsync(ServiceProvider provider, string email, CancellationToken cancellationToken)
    {
        await using var scope = provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var handler = new RegisterAccountCommandHandler(
            services.GetRequiredService<IAccountRepository>(),
            services.GetRequiredService<IProfileRepository>(),
            services.GetRequiredService<IEmailVerificationRepository>(),
            new RejectingPasswordHasher(),
            new SecureOtpGenerator(),
            new HmacSha256OtpHasher(Options.Create(new OtpSecurityOptions { Pepper = "integration-test-pepper" })),
            services.GetRequiredService<IOutboxWriter>(),
            new FixedClock());
        return await handler.Handle(new RegisterAccountCommand(null, email, "Example@1234", null), cancellationToken);
    }

    private async Task RefreshAsync(ServiceProvider provider, string rawToken, CancellationToken cancellationToken)
    {
        await using var scope = provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var handler = new RefreshTokenCommandHandler(
            services.GetRequiredService<IAccountRepository>(),
            services.GetRequiredService<IAuthSessionRepository>(),
            services.GetRequiredService<IRefreshTokenRepository>(),
            new StubJwtTokenIssuer(),
            _tokenGenerator,
            new FixedClock());
        await handler.Handle(new RefreshTokenCommand(rawToken), cancellationToken);
    }

    private static async Task LogoutAllAsync(ServiceProvider provider, Guid accountId, CancellationToken cancellationToken)
    {
        await using var scope = provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var handler = new LogoutAllCommandHandler(new AuthSessionManager(
            services.GetRequiredService<IAuthSessionRepository>(),
            services.GetRequiredService<IRefreshTokenRepository>(),
            services.GetRequiredService<IAccountRepository>(),
            new FixedClock()));
        await handler.Handle(new LogoutAllCommand(accountId), cancellationToken);
    }

    // Every password is wrong; hashing returns a fixed placeholder because these tests never log in successfully.
    private sealed class RejectingPasswordHasher : IPasswordHasher
    {
        public string Hash(string password) => "hash";

        public bool Verify(string password, string passwordHash) => false;

        public void VerifyDummy(string password)
        {
        }
    }

    private sealed class StubJwtTokenIssuer : IJwtTokenIssuer
    {
        public int AccessTokenExpirationSeconds => 900;

        public string IssueAccessToken(Account account, DateTimeOffset nowUtc) => "access-token";
    }

    private sealed class FixedClock : IClock
    {
        public DateTimeOffset UtcNow => Now;
    }
}
