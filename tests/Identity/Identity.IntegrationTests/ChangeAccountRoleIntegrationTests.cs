using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ParkingSystem.BuildingBlocks.Results;
using ParkingSystem.BuildingBlocks.Time;
using ParkingSystem.Identity.Application.Accounts;
using ParkingSystem.Identity.Application.Accounts.ChangeAccountRole;
using ParkingSystem.Identity.Application.Accounts.UnlockAccount;
using ParkingSystem.Identity.Domain.Accounts;
using ParkingSystem.Identity.Infrastructure.Security;
using ParkingSystem.Identity.Persistence;
using ParkingSystem.Testing;
using Xunit;

namespace ParkingSystem.Tests.Identity.IntegrationTests;

// Handler thật trên PostgreSQL: đổi vai trò phải lưu cùng lúc với việc thu hồi mọi phiên.
public sealed class ChangeAccountRoleIntegrationTests(PostgreSqlContainerFixture postgres) : IClassFixture<PostgreSqlContainerFixture>
{
    private static readonly DateTimeOffset Now = new(2026, 10, 9, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid AdminId = Guid.NewGuid();

    [Fact]
    public async Task Changing_the_role_saves_it_and_revokes_every_session_and_refresh_token()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var provider = await CreateProviderAsync(cancellationToken);
        var accountId = await SeedAccountWithSessionsAsync(provider, "role-change@example.com", sessions: 2, cancellationToken);

        var result = await ChangeRoleAsync(provider, AdminId, accountId, "staff", cancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(nameof(AccountRole.Staff), result.Value!.Role);
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        Assert.Equal(AccountRole.Staff, (await db.Accounts.AsNoTracking().SingleAsync(a => a.Id == accountId, cancellationToken)).Role);
        var sessions = await db.AuthSessions.AsNoTracking().Where(s => s.AccountId == accountId).ToListAsync(cancellationToken);
        var tokens = await db.RefreshTokens.AsNoTracking().Where(t => t.AccountId == accountId).ToListAsync(cancellationToken);
        Assert.All(sessions, s => Assert.Equal(ChangeAccountRoleCommandHandler.RevokedReason, s.RevokedReason));
        Assert.All(tokens, t => Assert.Equal(ChangeAccountRoleCommandHandler.RevokedReason, t.RevokedReason));
        Assert.Equal(2, tokens.Count);
    }

    [Fact]
    public async Task Same_role_again_changes_nothing_and_keeps_the_sessions()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var provider = await CreateProviderAsync(cancellationToken);
        var accountId = await SeedAccountWithSessionsAsync(provider, "role-same@example.com", sessions: 1, cancellationToken);

        var result = await ChangeRoleAsync(provider, AdminId, accountId, nameof(AccountRole.Customer), cancellationToken);

        Assert.True(result.IsSuccess);
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        Assert.False(await db.AuthSessions.AnyAsync(s => s.AccountId == accountId && s.RevokedAtUtc != null, cancellationToken));
    }

    [Fact]
    public async Task An_admin_cannot_change_their_own_role_and_nothing_is_saved()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var provider = await CreateProviderAsync(cancellationToken);
        var adminId = await SeedAccountWithSessionsAsync(provider, "role-self@example.com", sessions: 1, cancellationToken, AccountRole.SystemAdmin);

        var result = await ChangeRoleAsync(provider, adminId, adminId, nameof(AccountRole.Customer), cancellationToken);

        Assert.Equal(ChangeAccountRoleErrors.CannotChangeOwnRole.Code, result.Error?.Code);
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        Assert.Equal(AccountRole.SystemAdmin, (await db.Accounts.AsNoTracking().SingleAsync(a => a.Id == adminId, cancellationToken)).Role);
        Assert.False(await db.AuthSessions.AnyAsync(s => s.AccountId == adminId && s.RevokedAtUtc != null, cancellationToken));
    }

    [Fact]
    public async Task Unknown_account_is_reported_as_not_found()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var provider = await CreateProviderAsync(cancellationToken);

        var result = await ChangeRoleAsync(provider, AdminId, Guid.NewGuid(), nameof(AccountRole.Staff), cancellationToken);

        Assert.Equal(UnlockAccountErrors.AccountNotFound.Code, result.Error?.Code);
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

    private static async Task<Guid> SeedAccountWithSessionsAsync(
        ServiceProvider provider, string email, int sessions, CancellationToken cancellationToken, AccountRole role = AccountRole.Customer)
    {
        await using var scope = provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var account = Account.Register(Guid.NewGuid(), email, "hash", null, Now);
        account.VerifyEmail();
        account.AssignRole(role);
        services.GetRequiredService<IAccountRepository>().Add(account);

        var tokenGenerator = new RefreshTokenGenerator();
        for (var i = 0; i < sessions; i++)
        {
            var session = AuthSession.Create(Guid.NewGuid(), account.Id, "Test Device", "127.0.0.1", Now, Now.AddDays(AuthSession.DefaultAbsoluteLifetimeDays));
            services.GetRequiredService<IAuthSessionRepository>().Add(session);
            var (_, tokenHash) = tokenGenerator.GenerateToken();
            services.GetRequiredService<IRefreshTokenRepository>()
                .Add(RefreshToken.Issue(Guid.NewGuid(), session.Id, account.Id, tokenHash, Now, session.CalculateNextTokenExpiration(Now)));
        }

        await services.GetRequiredService<IAccountRepository>().SaveChangesAsync(cancellationToken);
        return account.Id;
    }

    private static async Task<Result<ChangedAccountRole>> ChangeRoleAsync(
        ServiceProvider provider, Guid adminId, Guid accountId, string role, CancellationToken cancellationToken)
    {
        await using var scope = provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var handler = new ChangeAccountRoleCommandHandler(
            services.GetRequiredService<IAccountRepository>(),
            services.GetRequiredService<IAuthSessionRepository>(),
            services.GetRequiredService<IRefreshTokenRepository>(),
            new FixedClock());
        return await handler.Handle(new ChangeAccountRoleCommand(adminId, accountId, role), cancellationToken);
    }

    private sealed class FixedClock : IClock
    {
        public DateTimeOffset UtcNow => Now;
    }
}
