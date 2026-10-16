using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ParkingSystem.BuildingBlocks.Time;
using ParkingSystem.Identity.Application.Accounts;
using ParkingSystem.Identity.Application.Accounts.Logout;
using ParkingSystem.Identity.Application.Accounts.TokenRefresh;
using ParkingSystem.Identity.Application.Common.Security;
using ParkingSystem.Identity.Domain.Accounts;
using ParkingSystem.Identity.Infrastructure.Security;
using ParkingSystem.Identity.Persistence;
using ParkingSystem.Testing;
using Xunit;

namespace ParkingSystem.Tests.Identity.IntegrationTests;

// Runs the real handlers against PostgreSQL, each call on its own DbContext/connection,
// so the FOR UPDATE row locks are what actually serialises the requests.
public sealed class RefreshTokenConcurrencyIntegrationTests(PostgreSqlContainerFixture postgres) : IClassFixture<PostgreSqlContainerFixture>
{
    private static readonly DateTimeOffset Now = new(2026, 10, 8, 12, 0, 0, TimeSpan.Zero);
    private readonly RefreshTokenGenerator _tokenGenerator = new();

    [Fact]
    public async Task Two_concurrent_refreshes_with_the_same_token_yield_one_rotation_and_revoke_the_session()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var provider = await CreateProviderAsync(cancellationToken);
        var (sessionId, rawToken) = await SeedSessionAsync(provider, "refresh-race@example.com", cancellationToken);

        var results = await Task.WhenAll(
            RefreshAsync(provider, rawToken, cancellationToken),
            RefreshAsync(provider, rawToken, cancellationToken));

        // No grace window (US-AUTH-005 spec): the second presentation of the rotated token is reuse.
        Assert.Single(results, result => result.IsSuccess);
        Assert.Single(results, result => result.Error?.Code == RefreshTokenErrors.TokenReuseDetected.Code);
        await AssertSessionFullyRevokedAsync(provider, sessionId, cancellationToken);
    }

    [Fact]
    public async Task Concurrent_refresh_and_logout_never_leave_an_active_token_behind()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var provider = await CreateProviderAsync(cancellationToken);
        var (sessionId, rawToken) = await SeedSessionAsync(provider, "refresh-logout-race@example.com", cancellationToken);

        await Task.WhenAll(
            RefreshAsync(provider, rawToken, cancellationToken),
            LogoutAsync(provider, rawToken, cancellationToken));

        // Whichever wins the lock, logout must not leave the freshly rotated token usable.
        await AssertSessionFullyRevokedAsync(provider, sessionId, cancellationToken);
    }

    private async Task<ServiceProvider> CreateProviderAsync(CancellationToken cancellationToken)
    {
        var services = new ServiceCollection();
        services.AddPersistence(PostgreSqlTestConfiguration.Create(postgres.ConnectionString));
        var provider = services.BuildServiceProvider(validateScopes: true);

        await using var scope = provider.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IdentityDbContext>().Database.MigrateAsync(cancellationToken);
        return provider;
    }

    private async Task<(Guid SessionId, string RawToken)> SeedSessionAsync(
        ServiceProvider provider, string email, CancellationToken cancellationToken)
    {
        await using var scope = provider.CreateAsyncScope();
        var accounts = scope.ServiceProvider.GetRequiredService<IAccountRepository>();

        var account = Account.Register(Guid.NewGuid(), email, "hash", null, Now);
        account.VerifyEmail();
        accounts.Add(account);

        var session = AuthSession.Create(Guid.NewGuid(), account.Id, "Test Device", "127.0.0.1", Now, Now.AddDays(AuthSession.DefaultAbsoluteLifetimeDays));
        scope.ServiceProvider.GetRequiredService<IAuthSessionRepository>().Add(session);

        var (rawToken, tokenHash) = _tokenGenerator.GenerateToken();
        scope.ServiceProvider.GetRequiredService<IRefreshTokenRepository>()
            .Add(RefreshToken.Issue(Guid.NewGuid(), session.Id, account.Id, tokenHash, Now, session.CalculateNextTokenExpiration(Now)));

        await accounts.SaveChangesAsync(cancellationToken);
        return (session.Id, rawToken);
    }

    private async Task<ParkingSystem.BuildingBlocks.Results.Result<RefreshTokenResponse>> RefreshAsync(
        ServiceProvider provider, string rawToken, CancellationToken cancellationToken)
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
        return await handler.Handle(new RefreshTokenCommand(rawToken), cancellationToken);
    }

    private async Task LogoutAsync(ServiceProvider provider, string rawToken, CancellationToken cancellationToken)
    {
        await using var scope = provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var handler = new LogoutCommandHandler(
            services.GetRequiredService<IAccountRepository>(),
            services.GetRequiredService<IAuthSessionRepository>(),
            services.GetRequiredService<IRefreshTokenRepository>(),
            _tokenGenerator,
            new FixedClock());
        await handler.Handle(new LogoutCommand(rawToken), cancellationToken);
    }

    private static async Task AssertSessionFullyRevokedAsync(ServiceProvider provider, Guid sessionId, CancellationToken cancellationToken)
    {
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();

        var session = await db.AuthSessions.AsNoTracking().SingleAsync(s => s.Id == sessionId, cancellationToken);
        Assert.True(session.IsRevoked);

        var tokens = await db.RefreshTokens.AsNoTracking().Where(t => t.SessionId == sessionId).ToListAsync(cancellationToken);
        Assert.NotEmpty(tokens);
        Assert.All(tokens, token => Assert.True(token.IsRevoked));
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
