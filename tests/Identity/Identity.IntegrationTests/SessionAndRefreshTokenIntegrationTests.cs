using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ParkingSystem.Identity.Application.Accounts;
using ParkingSystem.Identity.Domain.Accounts;
using ParkingSystem.Identity.Persistence;
using ParkingSystem.Testing;
using Xunit;

namespace ParkingSystem.Tests.Identity.IntegrationTests;

public sealed class SessionAndRefreshTokenIntegrationTests(PostgreSqlContainerFixture postgres) : IClassFixture<PostgreSqlContainerFixture>
{
    private static readonly DateTimeOffset InitialTime = new(2026, 10, 7, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Sessions_and_refresh_tokens_persist_and_link_correctly_in_postgresql()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var services = new ServiceCollection();
        services.AddPersistence(PostgreSqlTestConfiguration.Create(postgres.ConnectionString));
        await using var serviceProvider = services.BuildServiceProvider(validateScopes: true);

        await using (var migrationScope = serviceProvider.CreateAsyncScope())
        {
            var db = migrationScope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            await db.Database.MigrateAsync(cancellationToken);
        }

        var accountId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var tokenId = Guid.NewGuid();

        await using (var scope = serviceProvider.CreateAsyncScope())
        {
            var accounts = scope.ServiceProvider.GetRequiredService<IAccountRepository>();
            var sessions = scope.ServiceProvider.GetRequiredService<IAuthSessionRepository>();
            var refreshTokens = scope.ServiceProvider.GetRequiredService<IRefreshTokenRepository>();

            var account = Account.Register(accountId, "session-test@example.com", "hash", null, InitialTime);
            account.VerifyEmail();
            accounts.Add(account);

            var session = AuthSession.Create(sessionId, accountId, "Test Device", "127.0.0.1", InitialTime, InitialTime.AddDays(30));
            sessions.Add(session);

            var token = RefreshToken.Issue(tokenId, sessionId, accountId, "token-hash-1", InitialTime, InitialTime.AddDays(7));
            refreshTokens.Add(token);

            await accounts.SaveChangesAsync(cancellationToken);
        }

        // Verify loaded from PostgreSQL
        await using (var scope = serviceProvider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            var storedSession = await db.AuthSessions.AsNoTracking().SingleAsync(s => s.Id == sessionId, cancellationToken);
            Assert.Equal(accountId, storedSession.AccountId);
            Assert.Equal("Test Device", storedSession.DeviceInfo);
            Assert.Equal("127.0.0.1", storedSession.IpAddress);
            Assert.False(storedSession.IsRevoked);

            var storedToken = await db.RefreshTokens.AsNoTracking().SingleAsync(t => t.Id == tokenId, cancellationToken);
            Assert.Equal(sessionId, storedToken.SessionId);
            Assert.Equal(accountId, storedToken.AccountId);
            Assert.Equal("token-hash-1", storedToken.TokenHash);
            Assert.False(storedToken.IsRevoked);
        }
    }

    [Fact]
    public async Task FindByIdForUpdateAsync_successfully_reads_and_locks_session_row()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var services = new ServiceCollection();
        services.AddPersistence(PostgreSqlTestConfiguration.Create(postgres.ConnectionString));
        await using var serviceProvider = services.BuildServiceProvider(validateScopes: true);

        await using (var migrationScope = serviceProvider.CreateAsyncScope())
        {
            var db = migrationScope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            await db.Database.MigrateAsync(cancellationToken);
        }

        var accountId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();

        await using (var scope = serviceProvider.CreateAsyncScope())
        {
            var accounts = scope.ServiceProvider.GetRequiredService<IAccountRepository>();
            var sessions = scope.ServiceProvider.GetRequiredService<IAuthSessionRepository>();

            var account = Account.Register(accountId, "lock-test@example.com", "hash", null, InitialTime);
            accounts.Add(account);

            var session = AuthSession.Create(sessionId, accountId, null, null, InitialTime, InitialTime.AddDays(30));
            sessions.Add(session);

            await accounts.SaveChangesAsync(cancellationToken);
        }

        // Execute inside transaction with FOR UPDATE
        await using (var scope = serviceProvider.CreateAsyncScope())
        {
            var accounts = scope.ServiceProvider.GetRequiredService<IAccountRepository>();
            var sessions = scope.ServiceProvider.GetRequiredService<IAuthSessionRepository>();

            await accounts.ExecuteInTransactionAsync(async () =>
            {
                var lockedSession = await sessions.FindByIdForUpdateAsync(sessionId, cancellationToken);
                Assert.NotNull(lockedSession);
                Assert.Equal(sessionId, lockedSession.Id);
                lockedSession.RecordActivity(InitialTime.AddMinutes(5));
                await accounts.SaveChangesAsync(cancellationToken);
                return true;
            }, cancellationToken);
        }
    }

    [Fact]
    public async Task Scoped_revocation_revokes_only_the_specified_session_in_postgresql()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var services = new ServiceCollection();
        services.AddPersistence(PostgreSqlTestConfiguration.Create(postgres.ConnectionString));
        await using var serviceProvider = services.BuildServiceProvider(validateScopes: true);

        await using (var migrationScope = serviceProvider.CreateAsyncScope())
        {
            var db = migrationScope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            await db.Database.MigrateAsync(cancellationToken);
        }

        var accountId = Guid.NewGuid();
        var sessionLaptopId = Guid.NewGuid();
        var sessionMobileId = Guid.NewGuid();

        await using (var scope = serviceProvider.CreateAsyncScope())
        {
            var accounts = scope.ServiceProvider.GetRequiredService<IAccountRepository>();
            var sessions = scope.ServiceProvider.GetRequiredService<IAuthSessionRepository>();
            var tokens = scope.ServiceProvider.GetRequiredService<IRefreshTokenRepository>();

            var account = Account.Register(accountId, "scoped-test@example.com", "hash", null, InitialTime);
            accounts.Add(account);

            var sessionLaptop = AuthSession.Create(sessionLaptopId, accountId, "Laptop", null, InitialTime, InitialTime.AddDays(30));
            var sessionMobile = AuthSession.Create(sessionMobileId, accountId, "Mobile", null, InitialTime, InitialTime.AddDays(30));
            sessions.Add(sessionLaptop);
            sessions.Add(sessionMobile);

            var tokenLaptop = RefreshToken.Issue(Guid.NewGuid(), sessionLaptopId, accountId, "hash-laptop", InitialTime, InitialTime.AddDays(7));
            var tokenMobile = RefreshToken.Issue(Guid.NewGuid(), sessionMobileId, accountId, "hash-mobile", InitialTime, InitialTime.AddDays(7));
            tokens.Add(tokenLaptop);
            tokens.Add(tokenMobile);

            await accounts.SaveChangesAsync(cancellationToken);
        }

        // Revoke laptop session only (simulating token reuse on laptop)
        await using (var scope = serviceProvider.CreateAsyncScope())
        {
            var accounts = scope.ServiceProvider.GetRequiredService<IAccountRepository>();
            var sessions = scope.ServiceProvider.GetRequiredService<IAuthSessionRepository>();
            var tokens = scope.ServiceProvider.GetRequiredService<IRefreshTokenRepository>();

            await accounts.ExecuteInTransactionAsync(async () =>
            {
                var laptop = await sessions.FindByIdForUpdateAsync(sessionLaptopId, cancellationToken);
                Assert.NotNull(laptop);
                laptop.Revoke(InitialTime.AddMinutes(1), "ReuseDetected");
                await tokens.RevokeActiveTokensBySessionIdAsync(sessionLaptopId, InitialTime.AddMinutes(1), "FamilyCompromised", cancellationToken);
                await accounts.SaveChangesAsync(cancellationToken);
                return true;
            }, cancellationToken);
        }

        // Verify laptop is revoked, mobile is still active
        await using (var scope = serviceProvider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            var laptop = await db.AuthSessions.AsNoTracking().SingleAsync(s => s.Id == sessionLaptopId, cancellationToken);
            var mobile = await db.AuthSessions.AsNoTracking().SingleAsync(s => s.Id == sessionMobileId, cancellationToken);

            Assert.True(laptop.IsRevoked);
            Assert.Equal("ReuseDetected", laptop.RevokedReason);

            Assert.False(mobile.IsRevoked);
            Assert.Null(mobile.RevokedAtUtc);

            var laptopToken = await db.RefreshTokens.AsNoTracking().SingleAsync(t => t.SessionId == sessionLaptopId, cancellationToken);
            var mobileToken = await db.RefreshTokens.AsNoTracking().SingleAsync(t => t.SessionId == sessionMobileId, cancellationToken);

            Assert.True(laptopToken.IsRevoked);
            Assert.False(mobileToken.IsRevoked);
        }
    }

    [Fact]
    public async Task RevokeAllForAccountAsync_revokes_all_sessions_for_account_in_postgresql()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var services = new ServiceCollection();
        services.AddPersistence(PostgreSqlTestConfiguration.Create(postgres.ConnectionString));
        await using var serviceProvider = services.BuildServiceProvider(validateScopes: true);

        await using (var migrationScope = serviceProvider.CreateAsyncScope())
        {
            var db = migrationScope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            await db.Database.MigrateAsync(cancellationToken);
        }

        var accountId = Guid.NewGuid();
        var session1Id = Guid.NewGuid();
        var session2Id = Guid.NewGuid();

        await using (var scope = serviceProvider.CreateAsyncScope())
        {
            var accounts = scope.ServiceProvider.GetRequiredService<IAccountRepository>();
            var sessions = scope.ServiceProvider.GetRequiredService<IAuthSessionRepository>();

            var account = Account.Register(accountId, "revoke-all-test@example.com", "hash", null, InitialTime);
            accounts.Add(account);

            sessions.Add(AuthSession.Create(session1Id, accountId, "Device 1", null, InitialTime, InitialTime.AddDays(30)));
            sessions.Add(AuthSession.Create(session2Id, accountId, "Device 2", null, InitialTime, InitialTime.AddDays(30)));

            await accounts.SaveChangesAsync(cancellationToken);
        }

        await using (var scope = serviceProvider.CreateAsyncScope())
        {
            var sessions = scope.ServiceProvider.GetRequiredService<IAuthSessionRepository>();
            await sessions.RevokeAllForAccountAsync(accountId, InitialTime.AddMinutes(10), "LogoutAll", cancellationToken);
        }

        await using (var scope = serviceProvider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            var storedSessions = await db.AuthSessions.AsNoTracking().Where(s => s.AccountId == accountId).ToListAsync(cancellationToken);
            Assert.Equal(2, storedSessions.Count);
            Assert.All(storedSessions, s =>
            {
                Assert.True(s.IsRevoked);
                Assert.Equal("LogoutAll", s.RevokedReason);
            });
        }
    }

    [Fact]
    public async Task AccountRepository_FindByIdForUpdateAsync_successfully_reads_and_locks_account_row()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var services = new ServiceCollection();
        services.AddPersistence(PostgreSqlTestConfiguration.Create(postgres.ConnectionString));
        await using var serviceProvider = services.BuildServiceProvider(validateScopes: true);

        await using (var migrationScope = serviceProvider.CreateAsyncScope())
        {
            var db = migrationScope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            await db.Database.MigrateAsync(cancellationToken);
        }

        var accountId = Guid.NewGuid();

        await using (var scope = serviceProvider.CreateAsyncScope())
        {
            var accounts = scope.ServiceProvider.GetRequiredService<IAccountRepository>();
            var account = Account.Register(accountId, "account-lock-test@example.com", "hash", null, InitialTime);
            accounts.Add(account);
            await accounts.SaveChangesAsync(cancellationToken);
        }

        await using (var scope = serviceProvider.CreateAsyncScope())
        {
            var accounts = scope.ServiceProvider.GetRequiredService<IAccountRepository>();
            await accounts.ExecuteInTransactionAsync(async () =>
            {
                var locked = await accounts.FindByIdForUpdateAsync(accountId, cancellationToken);
                Assert.NotNull(locked);
                Assert.Equal(accountId, locked.Id);
                return true;
            }, cancellationToken);
        }
    }
}
