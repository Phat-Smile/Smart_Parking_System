using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ParkingSystem.BuildingBlocks.Results;
using ParkingSystem.BuildingBlocks.Time;
using ParkingSystem.Identity.Application.Accounts;
using ParkingSystem.Identity.Application.Accounts.AdminAccounts;
using ParkingSystem.Identity.Application.Accounts.Sessions;
using ParkingSystem.Identity.Application.Accounts.TokenRefresh;
using ParkingSystem.Identity.Application.Accounts.UnlockAccount;
using ParkingSystem.Identity.Application.Common.Security;
using ParkingSystem.Identity.Application.Profiles;
using ParkingSystem.Identity.Domain.Accounts;
using ParkingSystem.Identity.Domain.Profiles;
using ParkingSystem.Identity.Infrastructure.Security;
using ParkingSystem.Identity.Persistence;
using ParkingSystem.Testing;
using Xunit;

namespace ParkingSystem.Tests.Identity.IntegrationTests;

// Handler thật trên PostgreSQL cho màn thiết bị đang đăng nhập và màn Admin quản lý người dùng.
public sealed class SessionsAndAdminAccountsIntegrationTests(PostgreSqlContainerFixture postgres) : IClassFixture<PostgreSqlContainerFixture>
{
    private static readonly DateTimeOffset Now = new(2026, 10, 10, 9, 0, 0, TimeSpan.Zero);
    private static readonly Guid AdminId = Guid.NewGuid();
    private readonly RefreshTokenGenerator _tokens = new();

    [Fact]
    public async Task Revoking_one_device_stops_only_that_device()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var provider = await CreateProviderAsync(ct);
        var accountId = await SeedAccountAsync(provider, "devices@example.com", null, AccountRole.Customer, AccountStatus.EmailVerified, ct);
        var (phone, phoneToken) = await SeedSessionAsync(provider, accountId, "Mozilla/5.0 (iPhone; CPU iPhone OS 18_0 like Mac OS X) Safari/604.1", ct);
        var (laptop, laptopToken) = await SeedSessionAsync(provider, accountId, "Mozilla/5.0 (Windows NT 10.0) Chrome/141.0 Safari/537.36", ct);

        var listed = await SendAsync(provider, s => new ListSessionsQueryHandler(s.GetRequiredService<IAuthSessionRepository>(), new FixedClock())
            .Handle(new ListSessionsQuery(accountId, laptop), ct));
        Assert.Equal(2, listed.Value!.Count);
        Assert.True(Assert.Single(listed.Value, x => x.Id == laptop).IsCurrent);
        Assert.Equal("Safari trên iOS", Assert.Single(listed.Value, x => x.Id == phone).Device);

        var revoked = await SendAsync(provider, s => RevokeHandler(s).Handle(new RevokeSessionCommand(accountId, phone), ct));
        Assert.True(revoked.IsSuccess);

        Assert.Equal(RefreshTokenErrors.SessionRevoked.Code, (await RefreshAsync(provider, phoneToken, ct)).Error?.Code);
        Assert.True((await RefreshAsync(provider, laptopToken, ct)).IsSuccess);
    }

    [Fact]
    public async Task Another_users_session_looks_missing_and_stays_active()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var provider = await CreateProviderAsync(ct);
        var owner = await SeedAccountAsync(provider, "victim@example.com", null, AccountRole.Customer, AccountStatus.EmailVerified, ct);
        var attacker = await SeedAccountAsync(provider, "attacker@example.com", null, AccountRole.Customer, AccountStatus.EmailVerified, ct);
        var (victimSession, victimToken) = await SeedSessionAsync(provider, owner, "curl/8.9", ct);

        var result = await SendAsync(provider, s => RevokeHandler(s).Handle(new RevokeSessionCommand(attacker, victimSession), ct));

        Assert.Equal(SessionErrors.SessionNotFound.Code, result.Error?.Code);
        Assert.True((await RefreshAsync(provider, victimToken, ct)).IsSuccess);
    }

    [Fact]
    public async Task Search_filters_by_name_role_and_status_and_pages_in_one_query()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var provider = await CreateProviderAsync(ct);
        var marker = Guid.NewGuid().ToString("N")[..8];
        await SeedAccountAsync(provider, $"a-{marker}@example.com", "Trần Thị Bình", AccountRole.Staff, AccountStatus.EmailVerified, ct);
        await SeedAccountAsync(provider, $"b-{marker}@example.com", "Lê Văn Cường", AccountRole.Customer, AccountStatus.EmailVerified, ct);
        await SeedAccountAsync(provider, $"c-{marker}@example.com", null, AccountRole.Customer, AccountStatus.Suspended, ct);
        await SeedAccountAsync(provider, $"d_{marker}@example.com", null, AccountRole.Customer, AccountStatus.EmailVerified, ct);

        var byEmail = await SearchAsync(provider, new SearchAccountsQuery(marker.ToUpperInvariant(), null, null, 1, 2), ct);
        Assert.Equal(4, byEmail.TotalCount);
        Assert.Equal(2, byEmail.Items.Count);

        var byName = await SearchAsync(provider, new SearchAccountsQuery("thị bình", null, null), ct);
        Assert.Contains(byName.Items, x => x.Email == $"a-{marker}@example.com" && x.FullName == "Trần Thị Bình");

        var staff = await SearchAsync(provider, new SearchAccountsQuery(marker, "staff", null), ct);
        Assert.Equal(nameof(AccountRole.Staff), Assert.Single(staff.Items).Role);

        var suspended = await SearchAsync(provider, new SearchAccountsQuery(marker, null, "Suspended"), ct);
        Assert.Equal($"c-{marker}@example.com", Assert.Single(suspended.Items).Email);

        // "_" là ký tự đại diện trong LIKE; phải được hiểu đúng là ký tự gạch dưới.
        var underscore = await SearchAsync(provider, new SearchAccountsQuery($"d_{marker}", null, null), ct);
        Assert.Equal($"d_{marker}@example.com", Assert.Single(underscore.Items).Email);
    }

    [Fact]
    public async Task Suspending_revokes_every_session_and_activating_restores_the_account()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var provider = await CreateProviderAsync(ct);
        var accountId = await SeedAccountAsync(provider, "suspend-me@example.com", null, AccountRole.Staff, AccountStatus.EmailVerified, ct);
        var (_, token) = await SeedSessionAsync(provider, accountId, "curl/8.9", ct);

        var suspended = await ChangeStatusAsync(provider, accountId, AccountStatusChange.Suspend, ct);
        var again = await ChangeStatusAsync(provider, accountId, AccountStatusChange.Suspend, ct);

        Assert.Equal(nameof(AccountStatus.Suspended), suspended.Value!.Status);
        Assert.True(again.IsSuccess);
        Assert.Equal(RefreshTokenErrors.SessionRevoked.Code, (await RefreshAsync(provider, token, ct)).Error?.Code);

        var activated = await ChangeStatusAsync(provider, accountId, AccountStatusChange.Activate, ct);
        Assert.Equal(nameof(AccountStatus.EmailVerified), activated.Value!.Status);
    }

    [Fact]
    public async Task Admin_cannot_change_self_other_admins_or_activate_an_unverified_account()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var provider = await CreateProviderAsync(ct);
        var otherAdmin = await SeedAccountAsync(provider, "admin2@example.com", null, AccountRole.SystemAdmin, AccountStatus.EmailVerified, ct);
        var pending = await SeedAccountAsync(provider, "pending-activate@example.com", null, AccountRole.Customer, AccountStatus.PendingEmailVerification, ct);

        var self = await SendAsync(provider, s => StatusHandler(s).Handle(new ChangeAccountStatusCommand(otherAdmin, otherAdmin, AccountStatusChange.Suspend), ct));
        Assert.Equal(ChangeAccountStatusErrors.CannotChangeOwnStatus.Code, self.Error?.Code);
        Assert.Equal(ChangeAccountStatusErrors.CannotChangeAdminStatus.Code, (await ChangeStatusAsync(provider, otherAdmin, AccountStatusChange.Suspend, ct)).Error?.Code);
        Assert.Equal(ChangeAccountStatusErrors.AccountNotVerified.Code, (await ChangeStatusAsync(provider, pending, AccountStatusChange.Activate, ct)).Error?.Code);
        Assert.Equal(UnlockAccountErrors.AccountNotFound.Code, (await ChangeStatusAsync(provider, Guid.NewGuid(), AccountStatusChange.Suspend, ct)).Error?.Code);

        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        Assert.Equal(AccountStatus.EmailVerified, (await db.Accounts.AsNoTracking().SingleAsync(a => a.Id == otherAdmin, ct)).Status);
    }

    private async Task<ServiceProvider> CreateProviderAsync(CancellationToken ct)
    {
        var services = new ServiceCollection();
        services.AddPersistence(PostgreSqlTestConfiguration.Create(postgres.ConnectionString));
        services.AddScoped<IAuthSessionManager>(s => new AuthSessionManager(
            s.GetRequiredService<IAuthSessionRepository>(), s.GetRequiredService<IRefreshTokenRepository>(),
            s.GetRequiredService<IAccountRepository>(), new FixedClock()));
        var provider = services.BuildServiceProvider(validateScopes: true);

        await using var scope = provider.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IdentityDbContext>().Database.MigrateAsync(ct);
        return provider;
    }

    private static async Task<Guid> SeedAccountAsync(ServiceProvider provider, string email, string? fullName, AccountRole role, AccountStatus status, CancellationToken ct)
    {
        await using var scope = provider.CreateAsyncScope();
        var accounts = scope.ServiceProvider.GetRequiredService<IAccountRepository>();
        var account = Account.Register(Guid.NewGuid(), email, "hash", null, Now);
        if (status != AccountStatus.PendingEmailVerification)
            account.VerifyEmail();
        if (status == AccountStatus.Suspended)
            account.Suspend();
        account.AssignRole(role);
        accounts.Add(account);
        if (fullName is not null)
            scope.ServiceProvider.GetRequiredService<IProfileRepository>().Add(Profile.Create(account.Id, fullName, Now));
        await accounts.SaveChangesAsync(ct);
        return account.Id;
    }

    private async Task<(Guid SessionId, string RawToken)> SeedSessionAsync(ServiceProvider provider, Guid accountId, string userAgent, CancellationToken ct)
    {
        await using var scope = provider.CreateAsyncScope();
        var s = scope.ServiceProvider;
        var session = AuthSession.Create(Guid.NewGuid(), accountId, userAgent, "113.161.25.77", Now, Now.AddDays(AuthSession.DefaultAbsoluteLifetimeDays));
        s.GetRequiredService<IAuthSessionRepository>().Add(session);
        var (raw, hash) = _tokens.GenerateToken();
        s.GetRequiredService<IRefreshTokenRepository>().Add(RefreshToken.Issue(Guid.NewGuid(), session.Id, accountId, hash, Now, session.CalculateNextTokenExpiration(Now)));
        await s.GetRequiredService<IAccountRepository>().SaveChangesAsync(ct);
        return (session.Id, raw);
    }

    private static async Task<T> SendAsync<T>(ServiceProvider provider, Func<IServiceProvider, Task<T>> call)
    {
        await using var scope = provider.CreateAsyncScope();
        return await call(scope.ServiceProvider);
    }

    private static RevokeSessionCommandHandler RevokeHandler(IServiceProvider s) =>
        new(s.GetRequiredService<IAuthSessionRepository>(), s.GetRequiredService<IAuthSessionManager>());

    private static ChangeAccountStatusCommandHandler StatusHandler(IServiceProvider s) =>
        new(s.GetRequiredService<IAccountRepository>(), s.GetRequiredService<IAuthSessionRepository>(),
            s.GetRequiredService<IRefreshTokenRepository>(), new FixedClock());

    private static Task<Result<ChangedAccountStatus>> ChangeStatusAsync(ServiceProvider provider, Guid accountId, AccountStatusChange change, CancellationToken ct) =>
        SendAsync(provider, s => StatusHandler(s).Handle(new ChangeAccountStatusCommand(AdminId, accountId, change), ct));

    private static async Task<AccountPage> SearchAsync(ServiceProvider provider, SearchAccountsQuery query, CancellationToken ct) =>
        (await SendAsync(provider, s => new SearchAccountsQueryHandler(s.GetRequiredService<IAccountDirectory>(), new FixedClock()).Handle(query, ct))).Value!;

    private Task<Result<RefreshTokenResponse>> RefreshAsync(ServiceProvider provider, string rawToken, CancellationToken ct) =>
        SendAsync(provider, s => new RefreshTokenCommandHandler(
            s.GetRequiredService<IAccountRepository>(), s.GetRequiredService<IAuthSessionRepository>(),
            s.GetRequiredService<IRefreshTokenRepository>(), new StubJwtTokenIssuer(), _tokens, new FixedClock())
            .Handle(new RefreshTokenCommand(rawToken), ct));

    private sealed class StubJwtTokenIssuer : IJwtTokenIssuer
    {
        public int AccessTokenExpirationSeconds => 900;

        public string IssueAccessToken(Account account, Guid sessionId, DateTimeOffset nowUtc) => "access-token";
    }

    private sealed class FixedClock : IClock
    {
        public DateTimeOffset UtcNow => Now;
    }
}
