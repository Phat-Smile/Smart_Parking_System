using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ParkingSystem.BuildingBlocks.Results;
using ParkingSystem.BuildingBlocks.Time;
using ParkingSystem.Identity.Application.Accounts;
using ParkingSystem.Identity.Application.Accounts.GoogleLogin;
using ParkingSystem.Identity.Application.Accounts.Login;
using ParkingSystem.Identity.Application.Common.Security;
using ParkingSystem.Identity.Application.Profiles;
using ParkingSystem.Identity.Domain.Accounts;
using ParkingSystem.Identity.Infrastructure.Security;
using ParkingSystem.Identity.Persistence;
using ParkingSystem.Testing;
using Xunit;

namespace ParkingSystem.Tests.Identity.IntegrationTests;

// US-AUTH-002 (G1–G4) against PostgreSQL; the Google token check is replaced by a stub returning a fixed identity.
public sealed class GoogleLoginIntegrationTests(PostgreSqlContainerFixture postgres) : IClassFixture<PostgreSqlContainerFixture>
{
    private static readonly DateTimeOffset Now = new(2026, 10, 9, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task First_google_sign_in_creates_a_verified_customer_without_password_and_a_profile()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var provider = await CreateProviderAsync(ct);
        var google = new GoogleIdentity("sub-new", "New.User@gmail.com", "Nguyễn Văn An");

        var result = await GoogleLoginAsync(provider, google, ct);

        Assert.True(result.IsSuccess);
        Assert.Equal("Customer", result.Value!.Account.Role);
        Assert.Equal("EmailVerified", result.Value.Account.Status);
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var account = await db.Accounts.AsNoTracking().SingleAsync(a => a.Email == "new.user@gmail.com", ct);
        Assert.Null(account.PasswordHash);
        Assert.Equal("sub-new", account.GoogleSubject);
        var profile = await db.Profiles.AsNoTracking().SingleAsync(p => p.AccountId == account.Id, ct);
        Assert.Equal("Nguyễn Văn An", profile.FullName);
        Assert.Single(await db.AuthSessions.AsNoTracking().Where(s => s.AccountId == account.Id).ToListAsync(ct));
    }

    [Fact]
    public async Task Existing_pending_email_account_is_linked_verified_and_keeps_its_password()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var provider = await CreateProviderAsync(ct);
        var existing = await SeedAsync(provider, "linked@gmail.com", a => { }, ct);

        var result = await GoogleLoginAsync(provider, new GoogleIdentity("sub-linked", "linked@gmail.com", "Linked"), ct);

        Assert.True(result.IsSuccess);
        await using var scope = provider.CreateAsyncScope();
        var stored = await scope.ServiceProvider.GetRequiredService<IdentityDbContext>().Accounts.AsNoTracking().SingleAsync(a => a.Id == existing.Id, ct);
        Assert.Equal(AccountStatus.EmailVerified, stored.Status);
        Assert.Equal("sub-linked", stored.GoogleSubject);
        Assert.Equal("hash", stored.PasswordHash);
    }

    [Fact]
    public async Task Later_sign_ins_find_the_account_by_google_subject_even_if_the_google_email_changed()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var provider = await CreateProviderAsync(ct);
        await GoogleLoginAsync(provider, new GoogleIdentity("sub-stable", "old.address@gmail.com", "A"), ct);

        var again = await GoogleLoginAsync(provider, new GoogleIdentity("sub-stable", "renamed@gmail.com", "A"), ct);

        Assert.True(again.IsSuccess);
        Assert.Equal("old.address@gmail.com", again.Value!.Account.Email);
    }

    [Theory]
    [InlineData("suspended", "identity.account_suspended")]
    [InlineData("disabled", "identity.account_disabled")]
    [InlineData("locked", "identity.account_temporarily_locked")]
    [InlineData("other-google", "identity.invalid_google_token")]
    public async Task Blocked_accounts_cannot_sign_in_with_google(string state, string expectedCode)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var provider = await CreateProviderAsync(ct);
        var email = $"blocked-{state}@gmail.com";
        await SeedAsync(provider, email, account =>
        {
            account.VerifyEmail();
            if (state == "suspended") account.Suspend();
            if (state == "disabled") account.Disable();
            if (state == "locked")
                for (var i = 0; i < Account.MaxFailedAttemptsBeforeLockout; i++) account.RecordFailedLogin(Now);
            if (state == "other-google") account.LinkGoogle("sub-someone-else");
        }, ct);

        var result = await GoogleLoginAsync(provider, new GoogleIdentity($"sub-{state}", email, "B"), ct);

        Assert.Equal(expectedCode, result.Error!.Code);
    }

    [Fact]
    public async Task Concurrent_first_sign_ins_create_one_account_and_both_succeed()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var provider = await CreateProviderAsync(ct);
        var google = new GoogleIdentity("sub-race", "race@gmail.com", "Race");

        var results = await Task.WhenAll(GoogleLoginAsync(provider, google, ct), GoogleLoginAsync(provider, google, ct));

        Assert.All(results, r => Assert.True(r.IsSuccess));
        await using var scope = provider.CreateAsyncScope();
        Assert.Single(await scope.ServiceProvider.GetRequiredService<IdentityDbContext>().Accounts.AsNoTracking().Where(a => a.Email == "race@gmail.com").ToListAsync(ct));
    }

    [Fact]
    public async Task Invalid_token_and_switched_off_google_are_reported()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var provider = await CreateProviderAsync(ct);

        var invalid = await GoogleLoginAsync(provider, null, ct);
        var off = await GoogleLoginAsync(provider, null, ct, configured: false);

        Assert.Equal(GoogleLoginErrors.InvalidGoogleToken.Code, invalid.Error!.Code);
        Assert.Equal(GoogleLoginErrors.GoogleLoginUnavailable.Code, off.Error!.Code);
    }

    [Fact]
    public async Task Password_login_on_a_google_only_account_answers_like_a_wrong_password()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var provider = await CreateProviderAsync(ct);
        await GoogleLoginAsync(provider, new GoogleIdentity("sub-nopass", "nopass@gmail.com", "C"), ct);

        await using var scope = provider.CreateAsyncScope();
        var s = scope.ServiceProvider;
        var login = new LoginCommandHandler(
            s.GetRequiredService<IAccountRepository>(),
            s.GetRequiredService<IAuthSessionRepository>(),
            s.GetRequiredService<IRefreshTokenRepository>(),
            new BCryptPasswordHasher(),
            new StubJwtTokenIssuer(),
            new RefreshTokenGenerator(),
            new FixedClock());
        var result = await login.Handle(new LoginCommand("nopass@gmail.com", "Any@12345"), ct);

        Assert.Equal(LoginErrors.InvalidCredentials.Code, result.Error!.Code);
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

    private static async Task<Account> SeedAsync(ServiceProvider provider, string email, Action<Account> configure, CancellationToken cancellationToken)
    {
        await using var scope = provider.CreateAsyncScope();
        var accounts = scope.ServiceProvider.GetRequiredService<IAccountRepository>();
        var account = Account.Register(Guid.NewGuid(), email, "hash", null, Now.AddDays(-1));
        configure(account);
        accounts.Add(account);
        await accounts.SaveChangesAsync(cancellationToken);
        return account;
    }

    private static async Task<Result<LoginResponse>> GoogleLoginAsync(
        ServiceProvider provider, GoogleIdentity? identity, CancellationToken cancellationToken, bool configured = true)
    {
        await using var scope = provider.CreateAsyncScope();
        var s = scope.ServiceProvider;
        var handler = new GoogleLoginCommandHandler(
            new StubGoogleValidator(identity, configured),
            s.GetRequiredService<IAccountRepository>(),
            s.GetRequiredService<IGoogleAccountRepository>(),
            s.GetRequiredService<IProfileRepository>(),
            s.GetRequiredService<IAuthSessionRepository>(),
            s.GetRequiredService<IRefreshTokenRepository>(),
            new RefreshTokenGenerator(),
            new StubJwtTokenIssuer(),
            new FixedClock());
        return await handler.Handle(new GoogleLoginCommand("google-id-token", "test-device", "127.0.0.1"), cancellationToken);
    }

    private sealed class StubGoogleValidator(GoogleIdentity? identity, bool configured) : IGoogleIdTokenValidator
    {
        public bool IsConfigured => configured;
        public Task<GoogleIdentity?> ValidateAsync(string idToken, CancellationToken cancellationToken) => Task.FromResult(identity);
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
