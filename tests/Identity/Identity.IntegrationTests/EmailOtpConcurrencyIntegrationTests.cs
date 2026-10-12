using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ParkingSystem.BuildingBlocks.Results;
using ParkingSystem.BuildingBlocks.Time;
using ParkingSystem.Identity.Application.Accounts;
using ParkingSystem.Identity.Application.Accounts.ResendOtp;
using ParkingSystem.Identity.Application.Accounts.VerifyEmailOtp;
using ParkingSystem.Identity.Application.Common.Messaging.Outbox;
using ParkingSystem.Identity.Application.Common.Security;
using ParkingSystem.Identity.Domain.Accounts;
using ParkingSystem.Identity.Domain.EmailVerifications;
using ParkingSystem.Identity.Infrastructure.Security;
using ParkingSystem.Identity.Persistence;
using ParkingSystem.Testing;
using Xunit;

namespace ParkingSystem.Tests.Identity.IntegrationTests;

// Real Verify / Resend handlers against PostgreSQL, each call on its own DbContext/connection, so the
// Account FOR UPDATE lock is what serialises them (GAP-OTP-004 from the US-AUTH-003 test report).
public sealed class EmailOtpConcurrencyIntegrationTests(PostgreSqlContainerFixture postgres) : IClassFixture<PostgreSqlContainerFixture>
{
    private static readonly DateTimeOffset IssuedAt = new(2026, 10, 9, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Now = IssuedAt.AddSeconds(90); // past the 60 s resend cooldown, inside the 5 min TTL
    private const string FirstCode = "111111";
    private const string ResentCode = "222222";

    [Fact]
    public async Task Third_wrong_attempt_is_committed_with_the_code_invalidated_and_later_tries_stay_blocked()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var provider = await CreateProviderAsync(cancellationToken);
        var (accountId, email) = await SeedPendingAccountAsync(provider, "otp-third-wrong@example.com", cancellationToken);

        var wrong = new List<Result<VerifiedEmailAccount>>();
        foreach (var code in new[] { "900001", "900002", "900003" })
            wrong.Add(await VerifyAsync(provider, email, code, cancellationToken));
        var correctAfterwards = await VerifyAsync(provider, email, FirstCode, cancellationToken);

        Assert.Equal(VerifyEmailOtpErrors.InvalidOtpCode.Code, wrong[0].Error!.Code);
        Assert.Equal(VerifyEmailOtpErrors.InvalidOtpCode.Code, wrong[1].Error!.Code);
        Assert.Equal(VerifyEmailOtpErrors.OtpMaxAttemptsExceeded.Code, wrong[2].Error!.Code);
        Assert.Equal(VerifyEmailOtpErrors.OtpMaxAttemptsExceeded.Code, correctAfterwards.Error!.Code);

        // Fresh DbContext: the 400 responses did not roll the attempt count or the invalidation back.
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var stored = await db.EmailVerifications.AsNoTracking().SingleAsync(v => v.AccountId == accountId, cancellationToken);
        Assert.Equal(3, stored.AttemptCount);
        Assert.NotNull(stored.InvalidatedAtUtc);
        Assert.False(stored.IsUsed);
        var account = await db.Accounts.AsNoTracking().SingleAsync(a => a.Id == accountId, cancellationToken);
        Assert.Equal(AccountStatus.PendingEmailVerification, account.Status);
    }

    [Fact]
    public async Task Resend_processed_first_makes_the_old_code_unusable()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var provider = await CreateProviderAsync(cancellationToken);
        var (accountId, email) = await SeedPendingAccountAsync(provider, "otp-resend-first@example.com", cancellationToken);

        var resend = await ResendAsync(provider, email, cancellationToken);
        var verifyOld = await VerifyAsync(provider, email, FirstCode, cancellationToken);

        Assert.True(resend.IsSuccess);
        Assert.Equal(VerifyEmailOtpErrors.InvalidOtpCode.Code, verifyOld.Error!.Code);
        var state = await ReadStateAsync(provider, accountId, cancellationToken);
        Assert.Equal(AccountStatus.PendingEmailVerification, state.Status);
        Assert.Equal(2, state.Codes);
        Assert.Equal(1, state.ActiveCodes);
    }

    [Fact]
    public async Task Verify_processed_first_leaves_resend_silent_and_issues_no_new_code()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var provider = await CreateProviderAsync(cancellationToken);
        var (accountId, email) = await SeedPendingAccountAsync(provider, "otp-verify-first@example.com", cancellationToken);

        var verify = await VerifyAsync(provider, email, FirstCode, cancellationToken);
        var resend = await ResendAsync(provider, email, cancellationToken);

        Assert.True(verify.IsSuccess);
        Assert.True(resend.IsSuccess); // generic answer for an already verified account
        var state = await ReadStateAsync(provider, accountId, cancellationToken);
        Assert.Equal(AccountStatus.EmailVerified, state.Status);
        Assert.Equal(1, state.Codes);
    }

    [Fact]
    public async Task Concurrent_verify_and_resend_end_in_one_of_the_two_serial_outcomes()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var provider = await CreateProviderAsync(cancellationToken);

        for (var run = 0; run < 5; run++)
        {
            var (accountId, email) = await SeedPendingAccountAsync(provider, $"otp-race-{run}@example.com", cancellationToken);

            var verifyTask = VerifyAsync(provider, email, FirstCode, cancellationToken);
            var resendTask = ResendAsync(provider, email, cancellationToken);
            await Task.WhenAll(verifyTask, resendTask);

            var state = await ReadStateAsync(provider, accountId, cancellationToken);
            if (verifyTask.Result.IsSuccess)
            {
                // Verify won the lock: the account is verified and resend issued nothing.
                Assert.Equal(AccountStatus.EmailVerified, state.Status);
                Assert.Equal(1, state.Codes);
            }
            else
            {
                // Resend won: the old code was replaced, the account is still pending with exactly one live code.
                Assert.Equal(VerifyEmailOtpErrors.InvalidOtpCode.Code, verifyTask.Result.Error!.Code);
                Assert.Equal(AccountStatus.PendingEmailVerification, state.Status);
                Assert.Equal(2, state.Codes);
                Assert.Equal(1, state.ActiveCodes);
            }

            Assert.True(resendTask.Result.IsSuccess);
        }
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

    private static async Task<(Guid AccountId, string Email)> SeedPendingAccountAsync(
        ServiceProvider provider, string email, CancellationToken cancellationToken)
    {
        await using var scope = provider.CreateAsyncScope();
        var accounts = scope.ServiceProvider.GetRequiredService<IAccountRepository>();
        var account = Account.Register(Guid.NewGuid(), email, "hash", null, IssuedAt);
        accounts.Add(account);
        scope.ServiceProvider.GetRequiredService<IEmailVerificationRepository>()
            .Add(EmailVerification.Issue(Guid.NewGuid(), account.Id, account.Email, PlainOtpHasher.Hash(FirstCode, account.Id), IssuedAt));
        await accounts.SaveChangesAsync(cancellationToken);
        return (account.Id, account.Email);
    }

    private static async Task<Result<VerifiedEmailAccount>> VerifyAsync(
        ServiceProvider provider, string email, string code, CancellationToken cancellationToken)
    {
        await using var scope = provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var handler = new VerifyEmailOtpCommandHandler(
            services.GetRequiredService<IAccountRepository>(),
            services.GetRequiredService<IEmailVerificationRepository>(),
            new PlainOtpHasher(),
            services.GetRequiredService<IOutboxWriter>(),
            services.GetRequiredService<IAuthSessionRepository>(),
            services.GetRequiredService<IRefreshTokenRepository>(),
            new RefreshTokenGenerator(),
            new StubJwtTokenIssuer(),
            new FixedClock());
        return await handler.Handle(new VerifyEmailOtpCommand(email, code), cancellationToken);
    }

    private static async Task<Result<ResentOtpResult>> ResendAsync(
        ServiceProvider provider, string email, CancellationToken cancellationToken)
    {
        await using var scope = provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var handler = new ResendOtpCommandHandler(
            services.GetRequiredService<IAccountRepository>(),
            services.GetRequiredService<IEmailVerificationRepository>(),
            new FixedOtpGenerator(),
            new PlainOtpHasher(),
            services.GetRequiredService<IOutboxWriter>(),
            new FixedClock());
        return await handler.Handle(new ResendOtpCommand(email), cancellationToken);
    }

    private static async Task<(AccountStatus Status, int Codes, int ActiveCodes)> ReadStateAsync(
        ServiceProvider provider, Guid accountId, CancellationToken cancellationToken)
    {
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var account = await db.Accounts.AsNoTracking().SingleAsync(a => a.Id == accountId, cancellationToken);
        var codes = await db.EmailVerifications.AsNoTracking().Where(v => v.AccountId == accountId).ToListAsync(cancellationToken);
        return (account.Status, codes.Count, codes.Count(v => !v.IsUsed && v.InvalidatedAtUtc == null));
    }

    private sealed class PlainOtpHasher : IOtpHasher
    {
        public static string Hash(string otpCode, Guid accountId) => $"{accountId:N}:{otpCode}";

        public string HashOtp(string otpCode, Guid accountId) => Hash(otpCode, accountId);

        public bool VerifyOtp(string candidateOtp, Guid accountId, string expectedHash) =>
            Hash(candidateOtp, accountId) == expectedHash;
    }

    private sealed class StubJwtTokenIssuer : IJwtTokenIssuer
    {
        public int AccessTokenExpirationSeconds => 900;

        public string IssueAccessToken(Account account, DateTimeOffset nowUtc) => "access-token";
    }

    private sealed class FixedOtpGenerator : IOtpGenerator
    {
        public string GenerateNumericOtp(int length = 6) => ResentCode;
    }

    private sealed class FixedClock : IClock
    {
        public DateTimeOffset UtcNow => Now;
    }
}
