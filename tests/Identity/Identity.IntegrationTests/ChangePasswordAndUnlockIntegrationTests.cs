using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ParkingSystem.BuildingBlocks.Results;
using ParkingSystem.BuildingBlocks.Time;
using ParkingSystem.Identity.Application.Accounts;
using ParkingSystem.Identity.Application.Accounts.ChangePassword;
using ParkingSystem.Identity.Application.Accounts.Login;
using ParkingSystem.Identity.Application.Accounts.UnlockAccount;
using ParkingSystem.Identity.Application.Common.Auditing;
using ParkingSystem.Identity.Application.Common.Security;
using ParkingSystem.Identity.Domain.Accounts;
using ParkingSystem.Identity.Persistence;
using ParkingSystem.Testing;
using Xunit;

namespace ParkingSystem.Tests.Identity.IntegrationTests;

// US-AUTH-009 and the admin unlock of US-AUTH-008 against PostgreSQL (decided 2026-10-09).
public sealed class ChangePasswordAndUnlockIntegrationTests(PostgreSqlContainerFixture postgres) : IClassFixture<PostgreSqlContainerFixture>
{
    private static readonly DateTimeOffset Now = new(2026, 10, 9, 12, 0, 0, TimeSpan.Zero);
    private const string Current = "Old@12345";
    private const string NewPassword = "Xyz@67890";

    [Fact]
    public async Task Changing_the_password_revokes_every_session_and_clears_failed_attempts()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var provider = await CreateProviderAsync(ct);
        var account = await SeedAsync(provider, "change-ok@example.com", ct, failedAttempts: 1, sessions: 2);

        var result = await ChangeAsync(provider, account.Id, Current, ct);

        Assert.True(result.IsSuccess);
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var stored = await db.Accounts.AsNoTracking().SingleAsync(a => a.Id == account.Id, ct);
        Assert.Equal(PlainPasswordHasher.Hash(NewPassword), stored.PasswordHash);
        Assert.Equal(0, stored.FailedLoginAttempts);
        Assert.All(await db.AuthSessions.AsNoTracking().Where(s => s.AccountId == account.Id).ToListAsync(ct), s => Assert.True(s.IsRevoked));
        Assert.All(await db.RefreshTokens.AsNoTracking().Where(t => t.AccountId == account.Id).ToListAsync(ct), t => Assert.True(t.IsRevoked));
    }

    [Fact]
    public async Task Three_wrong_current_passwords_lock_the_account_and_the_lock_is_committed()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var provider = await CreateProviderAsync(ct);
        var account = await SeedAsync(provider, "change-wrong@example.com", ct);

        var first = await ChangeAsync(provider, account.Id, "Wrong@111", ct);
        var second = await ChangeAsync(provider, account.Id, "Wrong@222", ct);
        var third = await ChangeAsync(provider, account.Id, "Wrong@333", ct);
        var rightButLocked = await ChangeAsync(provider, account.Id, Current, ct);

        Assert.Equal(ChangePasswordErrors.InvalidCurrentPassword.Code, first.Error!.Code);
        Assert.Equal(ChangePasswordErrors.InvalidCurrentPassword.Code, second.Error!.Code);
        Assert.Equal(LoginErrors.AccountTemporarilyLocked.Code, third.Error!.Code);
        Assert.Equal(LoginErrors.AccountTemporarilyLocked.Code, rightButLocked.Error!.Code);

        await using var scope = provider.CreateAsyncScope();
        var stored = await scope.ServiceProvider.GetRequiredService<IdentityDbContext>().Accounts.AsNoTracking().SingleAsync(a => a.Id == account.Id, ct);
        Assert.Equal(Now.AddMinutes(Account.LockoutDurationMinutes), stored.LockoutEndUtc);
        Assert.Equal(PlainPasswordHasher.Hash(Current), stored.PasswordHash);
    }

    [Fact]
    public async Task A_google_account_without_password_cannot_change_it_and_suspended_accounts_are_refused()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var provider = await CreateProviderAsync(ct);
        Guid googleId, suspendedId;
        await using (var scope = provider.CreateAsyncScope())
        {
            var accounts = scope.ServiceProvider.GetRequiredService<IAccountRepository>();
            var google = Account.RegisterWithGoogle(Guid.NewGuid(), "change-google@gmail.com", "sub-change", Now);
            var suspended = Account.Register(Guid.NewGuid(), "change-suspended@example.com", PlainPasswordHasher.Hash(Current), null, Now);
            suspended.VerifyEmail();
            suspended.Suspend();
            accounts.Add(google);
            accounts.Add(suspended);
            await accounts.SaveChangesAsync(ct);
            (googleId, suspendedId) = (google.Id, suspended.Id);
        }

        var forGoogle = await ChangeAsync(provider, googleId, "Any@12345", ct);
        var forSuspended = await ChangeAsync(provider, suspendedId, Current, ct);

        Assert.Equal(ChangePasswordErrors.InvalidCurrentPassword.Code, forGoogle.Error!.Code);
        Assert.Equal(LoginErrors.AccountSuspended.Code, forSuspended.Error!.Code);
    }

    [Fact]
    public async Task Admin_unlock_clears_the_lockout_so_login_works_again_and_is_idempotent()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var provider = await CreateProviderAsync(ct);
        var account = await SeedAsync(provider, "unlock@example.com", ct, failedAttempts: Account.MaxFailedAttemptsBeforeLockout);

        var unlocked = await UnlockAsync(provider, account.Id, ct);
        var again = await UnlockAsync(provider, account.Id, ct);
        var unknown = await UnlockAsync(provider, Guid.NewGuid(), ct);

        Assert.True(unlocked.IsSuccess && again.IsSuccess);
        Assert.Equal("Đã mở khóa đăng nhập. Người dùng có thể đăng nhập lại ngay.", unlocked.Value!.Message);
        Assert.Equal("Tài khoản không bị khóa đăng nhập; số lần nhập sai đã được đặt lại.", again.Value!.Message);
        Assert.Equal(UnlockAccountErrors.AccountNotFound.Code, unknown.Error!.Code);
        await using var scope = provider.CreateAsyncScope();
        var s = scope.ServiceProvider;
        var stored = await s.GetRequiredService<IdentityDbContext>().Accounts.AsNoTracking().SingleAsync(a => a.Id == account.Id, ct);
        Assert.Equal(0, stored.FailedLoginAttempts);
        Assert.Null(stored.LockoutEndUtc);

        var login = await new LoginCommandHandler(
            s.GetRequiredService<IAccountRepository>(),
            s.GetRequiredService<IAuthSessionRepository>(),
            s.GetRequiredService<IRefreshTokenRepository>(),
            new PlainPasswordHasher(),
            new StubJwtTokenIssuer(),
            new ParkingSystem.Identity.Infrastructure.Security.RefreshTokenGenerator(),
            new FixedClock()).Handle(new LoginCommand("unlock@example.com", Current), ct);
        Assert.True(login.IsSuccess);
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

    private static async Task<Account> SeedAsync(ServiceProvider provider, string email, CancellationToken cancellationToken, int failedAttempts = 0, int sessions = 0)
    {
        await using var scope = provider.CreateAsyncScope();
        var s = scope.ServiceProvider;
        var accounts = s.GetRequiredService<IAccountRepository>();
        var account = Account.Register(Guid.NewGuid(), email, PlainPasswordHasher.Hash(Current), null, Now.AddDays(-1));
        account.VerifyEmail();
        for (var i = 0; i < failedAttempts; i++)
            account.RecordFailedLogin(Now);
        accounts.Add(account);
        for (var i = 0; i < sessions; i++)
        {
            var session = AuthSession.Create(Guid.NewGuid(), account.Id, "device", "127.0.0.1", Now.AddHours(-1), Now.AddDays(30));
            s.GetRequiredService<IAuthSessionRepository>().Add(session);
            s.GetRequiredService<IRefreshTokenRepository>()
                .Add(RefreshToken.Issue(Guid.NewGuid(), session.Id, account.Id, $"hash-{Guid.NewGuid():N}", Now.AddHours(-1), Now.AddDays(7)));
        }

        await accounts.SaveChangesAsync(cancellationToken);
        return account;
    }

    private static async Task<Result<PasswordChangedResult>> ChangeAsync(ServiceProvider provider, Guid accountId, string current, CancellationToken cancellationToken)
    {
        await using var scope = provider.CreateAsyncScope();
        var s = scope.ServiceProvider;
        return await new ChangePasswordCommandHandler(
            s.GetRequiredService<IAccountRepository>(),
            s.GetRequiredService<IAuthSessionRepository>(),
            s.GetRequiredService<IRefreshTokenRepository>(),
            new PlainPasswordHasher(),
            new FixedClock()).Handle(new ChangePasswordCommand(accountId, current, NewPassword), cancellationToken);
    }

    private static async Task<Result<UnlockedAccount>> UnlockAsync(ServiceProvider provider, Guid accountId, CancellationToken cancellationToken)
    {
        await using var scope = provider.CreateAsyncScope();
        return await new UnlockAccountCommandHandler(
            scope.ServiceProvider.GetRequiredService<IAccountRepository>(),
            new FixedClock(),
            scope.ServiceProvider.GetRequiredService<IIdentityAuditLog>())
            .Handle(new UnlockAccountCommand(accountId), cancellationToken);
    }

    private sealed class PlainPasswordHasher : IPasswordHasher
    {
        public static string Hash(string password) => "plain:" + password;
        string IPasswordHasher.Hash(string password) => Hash(password);
        public bool Verify(string password, string passwordHash) => Hash(password) == passwordHash;
        public void VerifyDummy(string password) { }
    }

    private sealed class StubJwtTokenIssuer : IJwtTokenIssuer
    {
        public int AccessTokenExpirationSeconds => 900;
        public string IssueAccessToken(Account account, DateTimeOffset nowUtc) => "access-token";
        public string IssueAccessToken(Account account, Guid sessionId, string authenticationMethods, DateTimeOffset nowUtc) => "access-token";
    }

    private sealed class FixedClock : IClock
    {
        public DateTimeOffset UtcNow => Now;
    }
}
