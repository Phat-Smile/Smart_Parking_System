using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ParkingSystem.BuildingBlocks.Results;
using ParkingSystem.BuildingBlocks.Time;
using ParkingSystem.Identity.Application.Accounts;
using ParkingSystem.Identity.Application.Accounts.ForgotPassword;
using ParkingSystem.Identity.Application.Accounts.ResetPassword;
using ParkingSystem.Identity.Application.Accounts.VerifyEmailOtp;
using ParkingSystem.Identity.Application.Common.Messaging.Outbox;
using ParkingSystem.Identity.Application.Common.Security;
using ParkingSystem.Identity.Domain.Accounts;
using ParkingSystem.Identity.Domain.EmailVerifications;
using ParkingSystem.Identity.Persistence;
using ParkingSystem.Testing;
using Xunit;

namespace ParkingSystem.Tests.Identity.IntegrationTests;

// US-AUTH-006 against PostgreSQL: real handlers and repositories, one DbContext per request.
public sealed class PasswordResetIntegrationTests(PostgreSqlContainerFixture postgres) : IClassFixture<PostgreSqlContainerFixture>
{
    private static readonly DateTimeOffset Now = new(2026, 10, 9, 12, 0, 0, TimeSpan.Zero);
    private const string ResetCode = "246810";
    private const string NewPassword = "Xyz@67890";

    [Fact]
    public async Task Forgot_password_issues_a_reset_code_and_event_only_for_a_verified_account()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var provider = await CreateProviderAsync(ct);
        var verified = await SeedAccountAsync(provider, "reset-verified@example.com", verified: true, ct);
        var pending = await SeedAccountAsync(provider, "reset-pending@example.com", verified: false, ct);

        var forVerified = await ForgotAsync(provider, verified.Email, Now, ct);
        var forPending = await ForgotAsync(provider, pending.Email, Now, ct);
        var forUnknown = await ForgotAsync(provider, "reset-unknown@example.com", Now, ct);

        Assert.True(forVerified.IsSuccess && forPending.IsSuccess && forUnknown.IsSuccess);
        Assert.Equal(forVerified.Value!.Message, forPending.Value!.Message); // no hint about the account

        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var resetCodes = await db.EmailVerifications.AsNoTracking().Where(v => v.Purpose == OtpPurpose.PasswordReset).ToListAsync(ct);
        Assert.Single(resetCodes, v => v.AccountId == verified.Id);
        Assert.DoesNotContain(resetCodes, v => v.AccountId == pending.Id);
        var events = await db.OutboxMessages.AsNoTracking().Where(m => m.EventType == "identity.password-reset-otp-issued.v1").ToListAsync(ct);
        Assert.Single(events, m => m.Payload.Contains(verified.Email, StringComparison.Ordinal));
        Assert.DoesNotContain(events, m => m.Payload.Contains(pending.Email, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Second_request_within_60_seconds_gets_429_with_the_remaining_seconds()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var provider = await CreateProviderAsync(ct);
        var account = await SeedAccountAsync(provider, "reset-cooldown@example.com", verified: true, ct);

        await ForgotAsync(provider, account.Email, Now, ct);
        var second = await ForgotAsync(provider, account.Email, Now.AddSeconds(20), ct);

        Assert.Equal(ForgotPasswordErrors.CooldownActiveCode, second.Error!.Code);
        Assert.Equal(40, second.Error.RetryAfterSeconds);
    }

    [Fact]
    public async Task Reset_codes_and_email_verification_codes_do_not_share_the_cooldown()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var provider = await CreateProviderAsync(ct);
        var account = await SeedAccountAsync(provider, "reset-own-quota@example.com", verified: true, ct);
        await using (var scope = provider.CreateAsyncScope())
        {
            // A verification code issued a moment ago must not block a reset request.
            var codes = scope.ServiceProvider.GetRequiredService<IEmailVerificationRepository>();
            codes.Add(EmailVerification.Issue(Guid.NewGuid(), account.Id, account.Email, PlainOtpHasher.Hash("135790", account.Id), Now));
            await codes.SaveChangesAsync(ct);
        }

        var result = await ForgotAsync(provider, account.Email, Now.AddSeconds(5), ct);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Reset_changes_the_password_revokes_every_session_and_clears_the_lockout()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var provider = await CreateProviderAsync(ct);
        var account = await SeedAccountAsync(provider, "reset-success@example.com", verified: true, ct, lockedOut: true, sessions: 2);
        await ForgotAsync(provider, account.Email, Now, ct);

        var result = await ResetAsync(provider, account.Email, ResetCode, Now.AddMinutes(1), ct);

        Assert.True(result.IsSuccess);
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var stored = await db.Accounts.AsNoTracking().SingleAsync(a => a.Id == account.Id, ct);
        Assert.Equal(PlainPasswordHasher.Hash(NewPassword), stored.PasswordHash);
        Assert.Equal(0, stored.FailedLoginAttempts);
        Assert.Null(stored.LockoutEndUtc);
        Assert.All(await db.AuthSessions.AsNoTracking().Where(s => s.AccountId == account.Id).ToListAsync(ct), s => Assert.True(s.IsRevoked));
        Assert.All(await db.RefreshTokens.AsNoTracking().Where(t => t.AccountId == account.Id).ToListAsync(ct), t => Assert.True(t.IsRevoked));
        var code = await db.EmailVerifications.AsNoTracking().SingleAsync(v => v.AccountId == account.Id && v.Purpose == OtpPurpose.PasswordReset, ct);
        Assert.True(code.IsUsed);

        var replay = await ResetAsync(provider, account.Email, ResetCode, Now.AddMinutes(2), ct);
        Assert.Equal(VerifyEmailOtpErrors.OtpExpired.Code, replay.Error!.Code);
    }

    [Fact]
    public async Task Third_wrong_code_is_committed_and_blocks_the_right_code_until_a_new_one()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var provider = await CreateProviderAsync(ct);
        var account = await SeedAccountAsync(provider, "reset-wrong@example.com", verified: true, ct);
        await ForgotAsync(provider, account.Email, Now, ct);

        var results = new List<Result<PasswordResetResult>>();
        foreach (var wrong in new[] { "900001", "900002", "900003" })
            results.Add(await ResetAsync(provider, account.Email, wrong, Now.AddMinutes(1), ct));
        var right = await ResetAsync(provider, account.Email, ResetCode, Now.AddMinutes(1), ct);

        Assert.Equal(VerifyEmailOtpErrors.InvalidOtpCode.Code, results[0].Error!.Code);
        Assert.Equal(VerifyEmailOtpErrors.InvalidOtpCode.Code, results[1].Error!.Code);
        Assert.Equal(VerifyEmailOtpErrors.OtpMaxAttemptsExceeded.Code, results[2].Error!.Code);
        Assert.Equal(VerifyEmailOtpErrors.OtpMaxAttemptsExceeded.Code, right.Error!.Code);

        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var code = await db.EmailVerifications.AsNoTracking().SingleAsync(v => v.AccountId == account.Id && v.Purpose == OtpPurpose.PasswordReset, ct);
        Assert.Equal(3, code.AttemptCount);
        Assert.NotNull(code.InvalidatedAtUtc);
        var stored = await db.Accounts.AsNoTracking().SingleAsync(a => a.Id == account.Id, ct);
        Assert.Equal(PlainPasswordHasher.Hash("Old@12345"), stored.PasswordHash);
    }

    [Fact]
    public async Task An_email_verification_code_cannot_reset_the_password()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var provider = await CreateProviderAsync(ct);
        var account = await SeedAccountAsync(provider, "reset-wrong-purpose@example.com", verified: true, ct);
        await using (var scope = provider.CreateAsyncScope())
        {
            var codes = scope.ServiceProvider.GetRequiredService<IEmailVerificationRepository>();
            codes.Add(EmailVerification.Issue(Guid.NewGuid(), account.Id, account.Email, PlainOtpHasher.Hash(ResetCode, account.Id), Now));
            await codes.SaveChangesAsync(ct);
        }

        var result = await ResetAsync(provider, account.Email, ResetCode, Now.AddMinutes(1), ct);

        Assert.Equal(VerifyEmailOtpErrors.OtpExpired.Code, result.Error!.Code);
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

    private static async Task<Account> SeedAccountAsync(
        ServiceProvider provider, string email, bool verified, CancellationToken cancellationToken, bool lockedOut = false, int sessions = 0)
    {
        await using var scope = provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var accounts = services.GetRequiredService<IAccountRepository>();
        var account = Account.Register(Guid.NewGuid(), email, PlainPasswordHasher.Hash("Old@12345"), null, Now.AddDays(-1));
        if (verified)
            account.VerifyEmail();
        if (lockedOut)
            for (var i = 0; i < Account.MaxFailedAttemptsBeforeLockout; i++)
                account.RecordFailedLogin(Now);
        accounts.Add(account);

        for (var i = 0; i < sessions; i++)
        {
            var session = AuthSession.Create(Guid.NewGuid(), account.Id, "device", "127.0.0.1", Now.AddHours(-1), Now.AddDays(30));
            services.GetRequiredService<IAuthSessionRepository>().Add(session);
            services.GetRequiredService<IRefreshTokenRepository>()
                .Add(RefreshToken.Issue(Guid.NewGuid(), session.Id, account.Id, $"hash-{Guid.NewGuid():N}", Now.AddHours(-1), Now.AddDays(7)));
        }

        await accounts.SaveChangesAsync(cancellationToken);
        return account;
    }

    private static async Task<Result<ForgotPasswordResult>> ForgotAsync(
        ServiceProvider provider, string email, DateTimeOffset at, CancellationToken cancellationToken)
    {
        await using var scope = provider.CreateAsyncScope();
        var s = scope.ServiceProvider;
        var handler = new ForgotPasswordCommandHandler(
            s.GetRequiredService<IAccountRepository>(),
            s.GetRequiredService<IEmailVerificationRepository>(),
            new FixedOtpGenerator(),
            new PlainOtpHasher(),
            s.GetRequiredService<IOutboxWriter>(),
            new FixedClock(at));
        return await handler.Handle(new ForgotPasswordCommand(email), cancellationToken);
    }

    private static async Task<Result<PasswordResetResult>> ResetAsync(
        ServiceProvider provider, string email, string code, DateTimeOffset at, CancellationToken cancellationToken)
    {
        await using var scope = provider.CreateAsyncScope();
        var s = scope.ServiceProvider;
        var handler = new ResetPasswordCommandHandler(
            s.GetRequiredService<IAccountRepository>(),
            s.GetRequiredService<IEmailVerificationRepository>(),
            s.GetRequiredService<IAuthSessionRepository>(),
            s.GetRequiredService<IRefreshTokenRepository>(),
            new PlainPasswordHasher(),
            new PlainOtpHasher(),
            new FixedClock(at));
        return await handler.Handle(new ResetPasswordCommand(email, code, NewPassword), cancellationToken);
    }

    private sealed class PlainOtpHasher : IOtpHasher
    {
        public static string Hash(string otpCode, Guid accountId) => $"{accountId:N}:{otpCode}";
        public string HashOtp(string otpCode, Guid accountId) => Hash(otpCode, accountId);
        public bool VerifyOtp(string candidateOtp, Guid accountId, string expectedHash) => Hash(candidateOtp, accountId) == expectedHash;
    }

    private sealed class PlainPasswordHasher : IPasswordHasher
    {
        public static string Hash(string password) => "plain:" + password;
        string IPasswordHasher.Hash(string password) => Hash(password);
        public bool Verify(string password, string passwordHash) => Hash(password) == passwordHash;
        public void VerifyDummy(string password) { }
    }

    private sealed class FixedOtpGenerator : IOtpGenerator
    {
        public string GenerateNumericOtp(int length = 6) => ResetCode;
    }

    private sealed class FixedClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset UtcNow => now;
    }
}
