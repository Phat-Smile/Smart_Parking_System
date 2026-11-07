using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ParkingSystem.BuildingBlocks.Results;
using ParkingSystem.BuildingBlocks.Time;
using ParkingSystem.Identity.Application.Accounts;
using ParkingSystem.Identity.Application.Accounts.Login;
using ParkingSystem.Identity.Application.Accounts.Mfa;
using ParkingSystem.Identity.Application.Accounts.TokenRefresh;
using ParkingSystem.Identity.Application.Common.Security;
using ParkingSystem.Identity.Domain.Accounts;
using ParkingSystem.Identity.Infrastructure.Security;
using ParkingSystem.Identity.Persistence;
using ParkingSystem.Testing;
using Xunit;

namespace ParkingSystem.Tests.Identity.IntegrationTests;

// US-AUTH-007 trên PostgreSQL thật: bật MFA, đăng nhập 2 bước, chống dùng lại mã, mã khôi phục, khoá khi đoán mã.
public sealed class MfaIntegrationTests(PostgreSqlContainerFixture postgres) : IClassFixture<PostgreSqlContainerFixture>
{
    private const string Password = "Correct@123";
    private readonly MutableClock _clock = new(new DateTimeOffset(2026, 10, 10, 10, 0, 0, TimeSpan.Zero));
    private readonly RefreshTokenGenerator _tokens = new();
    private readonly TotpService _totp = new();

    [Fact]
    public async Task Enabling_needs_a_correct_first_code_returns_ten_recovery_codes_and_signs_out_every_device()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var provider = await CreateProviderAsync(ct);
        var accountId = await SeedAccountAsync(provider, "mfa-enable@example.com", AccountRole.Customer, ct);
        await LoginAsync(provider, "mfa-enable@example.com", ct);

        var setup = (await SendAsync(provider, s => SetupHandler(s).Handle(new SetupMfaCommand(accountId), ct))).Value!;
        var wrong = await SendAsync(provider, s => ConfirmHandler(s).Handle(new ConfirmMfaCommand(accountId, WrongCode(setup.Secret)), ct));
        var right = await SendAsync(provider, s => ConfirmHandler(s).Handle(new ConfirmMfaCommand(accountId, CodeNow(setup.Secret)), ct));

        Assert.Equal(MfaErrors.InvalidCode.Code, wrong.Error?.Code);
        Assert.Equal(10, right.Value!.RecoveryCodes.Count);
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        Assert.All(await db.AuthSessions.AsNoTracking().Where(x => x.AccountId == accountId).ToListAsync(ct), x => Assert.True(x.IsRevoked));
        Assert.Equal(0, (await db.Accounts.AsNoTracking().SingleAsync(a => a.Id == accountId, ct)).FailedLoginAttempts);
    }

    [Fact]
    public async Task Login_stops_at_the_second_step_and_a_code_opens_a_session_marked_mfa_that_refresh_keeps()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var provider = await CreateProviderAsync(ct);
        var (accountId, secret, _) = await SeedWithMfaAsync(provider, "mfa-login@example.com", AccountRole.Customer, ct);

        var first = (await LoginAsync(provider, "mfa-login@example.com", ct)).Value!;
        Assert.Null(first.Session);
        await using (var scope = provider.CreateAsyncScope())
            Assert.False(await scope.ServiceProvider.GetRequiredService<IdentityDbContext>().AuthSessions.AnyAsync(x => x.AccountId == accountId && x.RevokedAtUtc == null, ct));

        _clock.Advance(TimeSpan.FromSeconds(30)); // bước mới, khác bước đã dùng lúc bật MFA
        var done = await CompleteAsync(provider, first.Mfa!.MfaToken, CodeNow(secret), null, ct);
        Assert.True(done.IsSuccess);

        await using (var scope = provider.CreateAsyncScope())
        {
            var session = await scope.ServiceProvider.GetRequiredService<IdentityDbContext>().AuthSessions.AsNoTracking()
                .SingleAsync(x => x.AccountId == accountId && x.RevokedAtUtc == null, ct);
            Assert.Equal("pwd mfa", session.AuthenticationMethods);
        }

        var refreshed = await SendAsync(provider, s => RefreshHandler(s).Handle(new RefreshTokenCommand(done.Value!.RefreshToken), ct));
        Assert.Equal("amr:pwd mfa", refreshed.Value!.AccessToken);
    }

    [Fact]
    public async Task The_same_code_cannot_be_used_twice_and_a_spent_challenge_is_refused()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var provider = await CreateProviderAsync(ct);
        var (_, secret, _) = await SeedWithMfaAsync(provider, "mfa-replay@example.com", AccountRole.Customer, ct);
        _clock.Advance(TimeSpan.FromSeconds(30));
        var code = CodeNow(secret);

        var a = (await LoginAsync(provider, "mfa-replay@example.com", ct)).Value!.Mfa!.MfaToken;
        Assert.True((await CompleteAsync(provider, a, code, null, ct)).IsSuccess);
        Assert.Equal(MfaErrors.ChallengeInvalid.Code, (await CompleteAsync(provider, a, code, null, ct)).Error?.Code);

        var b = (await LoginAsync(provider, "mfa-replay@example.com", ct)).Value!.Mfa!.MfaToken;
        Assert.Equal(MfaErrors.InvalidCode.Code, (await CompleteAsync(provider, b, code, null, ct)).Error?.Code);
    }

    [Fact]
    public async Task A_recovery_code_works_once()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var provider = await CreateProviderAsync(ct);
        var (_, _, recovery) = await SeedWithMfaAsync(provider, "mfa-recovery@example.com", AccountRole.Customer, ct);

        var a = (await LoginAsync(provider, "mfa-recovery@example.com", ct)).Value!.Mfa!.MfaToken;
        Assert.True((await CompleteAsync(provider, a, null, recovery[0].ToLowerInvariant(), ct)).IsSuccess);

        var b = (await LoginAsync(provider, "mfa-recovery@example.com", ct)).Value!.Mfa!.MfaToken;
        Assert.Equal(MfaErrors.InvalidCode.Code, (await CompleteAsync(provider, b, null, recovery[0], ct)).Error?.Code);
    }

    [Fact]
    public async Task Wrong_codes_count_towards_the_lockout_and_a_correct_password_does_not_reset_the_count()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var provider = await CreateProviderAsync(ct);
        var (accountId, secret, _) = await SeedWithMfaAsync(provider, "mfa-lock@example.com", AccountRole.Customer, ct);

        // Đăng nhập lại mỗi lần để lấy challenge mới: nếu mật khẩu đúng xoá bộ đếm thì sẽ đoán mã vô hạn được.
        Result<LoginResponse>? last = null;
        for (var i = 0; i < Account.MaxFailedAttemptsBeforeLockout; i++)
        {
            var token = (await LoginAsync(provider, "mfa-lock@example.com", ct)).Value!.Mfa!.MfaToken;
            last = await CompleteAsync(provider, token, WrongCode(secret), null, ct);
        }

        Assert.Equal(LoginErrors.AccountTemporarilyLocked.Code, last!.Error?.Code);
        Assert.Equal(LoginErrors.AccountTemporarilyLocked.Code, (await LoginAsync(provider, "mfa-lock@example.com", ct)).Error?.Code);
        await using var scope = provider.CreateAsyncScope();
        Assert.Equal(Account.MaxFailedAttemptsBeforeLockout,
            (await scope.ServiceProvider.GetRequiredService<IdentityDbContext>().Accounts.AsNoTracking().SingleAsync(a => a.Id == accountId, ct)).FailedLoginAttempts);
    }

    [Fact]
    public async Task Two_concurrent_second_steps_with_the_same_challenge_open_only_one_session()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var provider = await CreateProviderAsync(ct);
        var (accountId, secret, _) = await SeedWithMfaAsync(provider, "mfa-race@example.com", AccountRole.Customer, ct);
        _clock.Advance(TimeSpan.FromSeconds(30));
        var token = (await LoginAsync(provider, "mfa-race@example.com", ct)).Value!.Mfa!.MfaToken;
        var code = CodeNow(secret);

        var results = await Task.WhenAll(CompleteAsync(provider, token, code, null, ct), CompleteAsync(provider, token, code, null, ct));

        Assert.Single(results, r => r.IsSuccess);
        await using var scope = provider.CreateAsyncScope();
        Assert.Equal(1, await scope.ServiceProvider.GetRequiredService<IdentityDbContext>().AuthSessions.CountAsync(x => x.AccountId == accountId && x.RevokedAtUtc == null, ct));
    }

    [Fact]
    public async Task An_admin_cannot_disable_mfa_but_a_customer_can_with_password_and_code()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var provider = await CreateProviderAsync(ct);
        var (adminId, adminSecret, _) = await SeedWithMfaAsync(provider, "mfa-admin@example.com", AccountRole.SystemAdmin, ct);
        var (customerId, customerSecret, _) = await SeedWithMfaAsync(provider, "mfa-off@example.com", AccountRole.Customer, ct);
        _clock.Advance(TimeSpan.FromSeconds(30));

        var admin = await SendAsync(provider, s => DisableHandler(s).Handle(new DisableMfaCommand(adminId, Password, CodeNow(adminSecret)), ct));
        var wrongPassword = await SendAsync(provider, s => DisableHandler(s).Handle(new DisableMfaCommand(customerId, "Wrong@123", CodeNow(customerSecret)), ct));
        var customer = await SendAsync(provider, s => DisableHandler(s).Handle(new DisableMfaCommand(customerId, Password, CodeNow(customerSecret)), ct));

        Assert.Equal(MfaErrors.RequiredForAdmin.Code, admin.Error?.Code);
        Assert.Equal("identity.invalid_current_password", wrongPassword.Error?.Code);
        Assert.True(customer.IsSuccess);
        Assert.True((await LoginAsync(provider, "mfa-off@example.com", ct)).Value!.Session is not null);
    }

    private async Task<ServiceProvider> CreateProviderAsync(CancellationToken ct)
    {
        var services = new ServiceCollection();
        services.AddPersistence(PostgreSqlTestConfiguration.Create(postgres.ConnectionString));
        var provider = services.BuildServiceProvider(validateScopes: true);
        await using var scope = provider.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IdentityDbContext>().Database.MigrateAsync(ct);
        return provider;
    }

    private static async Task<Guid> SeedAccountAsync(ServiceProvider provider, string email, AccountRole role, CancellationToken ct)
    {
        await using var scope = provider.CreateAsyncScope();
        var accounts = scope.ServiceProvider.GetRequiredService<IAccountRepository>();
        var account = Account.Register(Guid.NewGuid(), email, PlainHasher.HashOf(Password), null, DateTimeOffset.UtcNow);
        account.VerifyEmail();
        account.AssignRole(role);
        accounts.Add(account);
        await accounts.SaveChangesAsync(ct);
        return account.Id;
    }

    private async Task<(Guid AccountId, string Secret, IReadOnlyList<string> Recovery)> SeedWithMfaAsync(ServiceProvider provider, string email, AccountRole role, CancellationToken ct)
    {
        var accountId = await SeedAccountAsync(provider, email, role, ct);
        var setup = (await SendAsync(provider, s => SetupHandler(s).Handle(new SetupMfaCommand(accountId), ct))).Value!;
        var enabled = (await SendAsync(provider, s => ConfirmHandler(s).Handle(new ConfirmMfaCommand(accountId, CodeNow(setup.Secret)), ct))).Value!;
        return (accountId, setup.Secret, enabled.RecoveryCodes);
    }

    private string CodeNow(string secret) =>
        TotpService.ComputeCode(TotpService.Base32Decode(secret), _clock.UtcNow.ToUnixTimeSeconds() / TotpService.StepSeconds);

    // Mã của bước rất xa hiện tại: chắc chắn sai mà vẫn đúng định dạng 6 số.
    private string WrongCode(string secret) =>
        TotpService.ComputeCode(TotpService.Base32Decode(secret), _clock.UtcNow.ToUnixTimeSeconds() / TotpService.StepSeconds + 100);

    private static async Task<T> SendAsync<T>(ServiceProvider provider, Func<IServiceProvider, Task<T>> call)
    {
        await using var scope = provider.CreateAsyncScope();
        return await call(scope.ServiceProvider);
    }

    private Task<Result<LoginOutcome>> LoginAsync(ServiceProvider provider, string email, CancellationToken ct) =>
        SendAsync(provider, s => new LoginCommandHandler(
            s.GetRequiredService<IAccountRepository>(), s.GetRequiredService<IAuthSessionRepository>(), s.GetRequiredService<IRefreshTokenRepository>(),
            new PlainHasher(), new AmrJwtIssuer(), _tokens, _clock, new MfaLoginGate(s.GetRequiredService<IMfaRepository>()))
            .Handle(new LoginCommand(email, Password, "test-device", "127.0.0.1"), ct));

    private Task<Result<LoginResponse>> CompleteAsync(ServiceProvider provider, string token, string? code, string? recovery, CancellationToken ct) =>
        SendAsync(provider, s => new CompleteMfaLoginCommandHandler(
            s.GetRequiredService<IAccountRepository>(), s.GetRequiredService<IMfaRepository>(), s.GetRequiredService<IAuthSessionRepository>(),
            s.GetRequiredService<IRefreshTokenRepository>(), _tokens, new AmrJwtIssuer(), _totp, new PrefixProtector(), _clock)
            .Handle(new CompleteMfaLoginCommand(token, code, recovery), ct));

    private SetupMfaCommandHandler SetupHandler(IServiceProvider s) =>
        new(s.GetRequiredService<IAccountRepository>(), s.GetRequiredService<IMfaRepository>(), _totp, new PrefixProtector(), _clock);

    private ConfirmMfaCommandHandler ConfirmHandler(IServiceProvider s) =>
        new(s.GetRequiredService<IAccountRepository>(), s.GetRequiredService<IMfaRepository>(), s.GetRequiredService<IAuthSessionRepository>(),
            s.GetRequiredService<IRefreshTokenRepository>(), _totp, new PrefixProtector(), _clock);

    private DisableMfaCommandHandler DisableHandler(IServiceProvider s) =>
        new(s.GetRequiredService<IAccountRepository>(), s.GetRequiredService<IMfaRepository>(), new PlainHasher(), _totp, new PrefixProtector(), _clock);

    private RefreshTokenCommandHandler RefreshHandler(IServiceProvider s) =>
        new(s.GetRequiredService<IAccountRepository>(), s.GetRequiredService<IAuthSessionRepository>(), s.GetRequiredService<IRefreshTokenRepository>(),
            new AmrJwtIssuer(), _tokens, _clock);

    // Mã hoá thật dùng ASP.NET Data Protection (kiểm khi chạy thật); ở đây chỉ cần secret không lưu nguyên văn.
    private sealed class PrefixProtector : IMfaSecretProtector
    {
        public string Protect(string secret) => "protected:" + secret;
        public string Unprotect(string protectedSecret) => protectedSecret["protected:".Length..];
    }

    private sealed class PlainHasher : IPasswordHasher
    {
        public static string HashOf(string password) => "hash:" + password;
        public string Hash(string password) => HashOf(password);
        public bool Verify(string password, string passwordHash) => passwordHash == HashOf(password);
        public void VerifyDummy(string password) { }
    }

    // Trả claim amr trong chuỗi token để test đọc được phiên đã qua MFA hay chưa.
    private sealed class AmrJwtIssuer : IJwtTokenIssuer
    {
        public int AccessTokenExpirationSeconds => 900;
        public string IssueAccessToken(Account account, DateTimeOffset nowUtc) => "amr:pwd";
        public string IssueAccessToken(Account account, Guid sessionId, string authenticationMethods, DateTimeOffset nowUtc) => "amr:" + authenticationMethods;
    }

    private sealed class MutableClock(DateTimeOffset start) : IClock
    {
        public DateTimeOffset UtcNow { get; private set; } = start;
        public void Advance(TimeSpan by) => UtcNow += by;
    }
}
