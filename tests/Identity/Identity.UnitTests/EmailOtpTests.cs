using System.Net.Mail;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ParkingSystem.BuildingBlocks.Time;
using ParkingSystem.Identity.Application.Accounts;
using ParkingSystem.Contracts.Events.Identity;
using ParkingSystem.Identity.Application.Accounts.ResendOtp;
using ParkingSystem.Identity.Application.Accounts.VerifyEmailOtp;
using ParkingSystem.Identity.Application.Common.Messaging.Outbox;
using ParkingSystem.Identity.Application.Common.Security;
using ParkingSystem.Identity.Domain.Accounts;
using ParkingSystem.Identity.Domain.EmailVerifications;
using ParkingSystem.Identity.Infrastructure.Security;
using Xunit;

namespace ParkingSystem.Tests.Identity.UnitTests;

public sealed class EmailOtpTests
{
    private static readonly DateTimeOffset InitialTime = new(2026, 10, 5, 10, 0, 0, TimeSpan.Zero);
    private readonly FakeOtpHasher _otpHasher = new();

    [Theory]
    [InlineData("user@example.com", "123456", true)]
    [InlineData("user@example.com", "000000", true)]
    [InlineData("invalid-email", "123456", false)]
    [InlineData("", "123456", false)]
    [InlineData("user@example.com", "12345", false)]
    [InlineData("user@example.com", "1234567", false)]
    [InlineData("user@example.com", "abcdef", false)]
    [InlineData("user@example.com", "", false)]
    [InlineData("user@example.com", "١٢٣٤٥٦", false)] // Arabic-Indic digits match \d but are not an OTP
    public void VerifyEmailOtpCommandValidator_validates_email_and_otp_format(string email, string otpCode, bool expectedValid)
    {
        var validator = new VerifyEmailOtpCommandValidator();
        var result = validator.Validate(new VerifyEmailOtpCommand(email, otpCode));

        Assert.Equal(expectedValid, result.IsValid);
    }

    [Theory]
    [InlineData("user@example.com", true)]
    [InlineData("invalid-email", false)]
    [InlineData("", false)]
    public void ResendOtpCommandValidator_validates_email_format(string email, bool expectedValid)
    {
        var validator = new ResendOtpCommandValidator();
        var result = validator.Validate(new ResendOtpCommand(email));

        Assert.Equal(expectedValid, result.IsValid);
    }

    [Fact]
    public async Task VerifyEmailOtp_succeeds_when_otp_matches_and_marks_account_verified()
    {
        var clock = new MutableClock(InitialTime);
        var account = Account.Register(Guid.NewGuid(), "user@example.com", "hash", null, clock.UtcNow);
        var verification = EmailVerification.Issue(Guid.NewGuid(), account.Id, account.Email, _otpHasher.HashOtp("654321", account.Id), clock.UtcNow);

        var accountRepo = new FakeAccountRepository([account]);
        var verificationRepo = new FakeEmailVerificationRepository([verification]);
        var outboxWriter = new FakeOutboxWriter();

        var handler = new VerifyEmailOtpCommandHandler(accountRepo, verificationRepo, _otpHasher, outboxWriter, clock);
        var result = await handler.Handle(
            new VerifyEmailOtpCommand("  User@Example.COM ", "654321"),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(account.Id, result.Value!.AccountId);
        Assert.Equal("user@example.com", result.Value.Email);
        Assert.Equal("EmailVerified", result.Value.Status);
        Assert.True(result.Value.IsVerified);

        Assert.Equal(AccountStatus.EmailVerified, account.Status);
        Assert.True(verification.IsUsed);

        var enqueued = Assert.Single(outboxWriter.Enqueued);
        Assert.Equal("identity.account-email-verified.v1", enqueued.EventType);
        var eventPayload = Assert.IsType<AccountEmailVerifiedIntegrationEvent>(enqueued.Payload);
        Assert.Equal(account.Id, eventPayload.AccountId);
        Assert.Equal("user@example.com", eventPayload.Email);
    }

    [Fact]
    public async Task VerifyEmailOtp_fails_with_generic_expired_error_when_account_does_not_exist()
    {
        var clock = new MutableClock(InitialTime);
        var accountRepo = new FakeAccountRepository([]);
        var verificationRepo = new FakeEmailVerificationRepository([]);
        var outboxWriter = new FakeOutboxWriter();

        var handler = new VerifyEmailOtpCommandHandler(accountRepo, verificationRepo, _otpHasher, outboxWriter, clock);
        var result = await handler.Handle(
            new VerifyEmailOtpCommand("nonexistent@example.com", "123456"),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal(VerifyEmailOtpErrors.OtpExpired, result.Error);
    }

    [Fact]
    public async Task VerifyEmailOtp_fails_with_generic_expired_error_when_account_already_verified()
    {
        var clock = new MutableClock(InitialTime);
        var account = Account.Register(Guid.NewGuid(), "user@example.com", "hash", null, clock.UtcNow);
        account.VerifyEmail();

        var accountRepo = new FakeAccountRepository([account]);
        var verificationRepo = new FakeEmailVerificationRepository([]);
        var outboxWriter = new FakeOutboxWriter();

        var handler = new VerifyEmailOtpCommandHandler(accountRepo, verificationRepo, _otpHasher, outboxWriter, clock);
        var result = await handler.Handle(
            new VerifyEmailOtpCommand("user@example.com", "123456"),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal(VerifyEmailOtpErrors.OtpExpired, result.Error);
    }

    [Fact]
    public async Task VerifyEmailOtp_fails_when_otp_is_expired()
    {
        var clock = new MutableClock(InitialTime);
        var account = Account.Register(Guid.NewGuid(), "user@example.com", "hash", null, clock.UtcNow);
        var verification = EmailVerification.Issue(Guid.NewGuid(), account.Id, account.Email, _otpHasher.HashOtp("123456", account.Id), clock.UtcNow);

        // Advance past 5 minutes
        clock.UtcNow = InitialTime.AddMinutes(5).AddSeconds(1);

        var accountRepo = new FakeAccountRepository([account]);
        var verificationRepo = new FakeEmailVerificationRepository([verification]);
        var outboxWriter = new FakeOutboxWriter();

        var handler = new VerifyEmailOtpCommandHandler(accountRepo, verificationRepo, _otpHasher, outboxWriter, clock);
        var result = await handler.Handle(
            new VerifyEmailOtpCommand("user@example.com", "123456"),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal(VerifyEmailOtpErrors.OtpExpired, result.Error);
        Assert.Equal(AccountStatus.PendingEmailVerification, account.Status);
    }

    [Fact]
    public async Task VerifyEmailOtp_increments_attempt_count_and_reports_invalid_code_on_mismatch()
    {
        var clock = new MutableClock(InitialTime);
        var account = Account.Register(Guid.NewGuid(), "user@example.com", "hash", null, clock.UtcNow);
        var verification = EmailVerification.Issue(Guid.NewGuid(), account.Id, account.Email, _otpHasher.HashOtp("123456", account.Id), clock.UtcNow);

        var accountRepo = new FakeAccountRepository([account]);
        var verificationRepo = new FakeEmailVerificationRepository([verification]);
        var outboxWriter = new FakeOutboxWriter();

        var handler = new VerifyEmailOtpCommandHandler(accountRepo, verificationRepo, _otpHasher, outboxWriter, clock);

        // Attempt 1: wrong code
        var firstResult = await handler.Handle(
            new VerifyEmailOtpCommand("user@example.com", "999999"),
            TestContext.Current.CancellationToken);

        Assert.True(firstResult.IsFailure);
        Assert.Equal(VerifyEmailOtpErrors.InvalidOtpCode, firstResult.Error);
        Assert.Equal(1, verification.AttemptCount);
        Assert.False(verification.IsUsed);

        // Attempt 2: wrong code
        var secondResult = await handler.Handle(
            new VerifyEmailOtpCommand("user@example.com", "888888"),
            TestContext.Current.CancellationToken);

        Assert.True(secondResult.IsFailure);
        Assert.Equal(VerifyEmailOtpErrors.InvalidOtpCode, secondResult.Error);
        Assert.Equal(2, verification.AttemptCount);
        Assert.False(verification.IsUsed);

        // Attempt 3: wrong code -> locks code
        var thirdResult = await handler.Handle(
            new VerifyEmailOtpCommand("user@example.com", "777777"),
            TestContext.Current.CancellationToken);

        Assert.True(thirdResult.IsFailure);
        Assert.Equal(VerifyEmailOtpErrors.OtpMaxAttemptsExceeded, thirdResult.Error);
        Assert.Equal(3, verification.AttemptCount);
        Assert.True(verification.IsInvalidated);
        Assert.Equal(clock.UtcNow, verification.InvalidatedAtUtc);

        // Attempt 4: even with correct code, now locked
        var fourthResult = await handler.Handle(
            new VerifyEmailOtpCommand("user@example.com", "123456"),
            TestContext.Current.CancellationToken);

        Assert.True(fourthResult.IsFailure);
        Assert.Equal(VerifyEmailOtpErrors.OtpMaxAttemptsExceeded, fourthResult.Error);
        Assert.Equal(AccountStatus.PendingEmailVerification, account.Status);
    }

    [Fact]
    public async Task VerifyEmailOtp_accepts_the_new_code_after_resend_following_three_wrong_attempts()
    {
        var clock = new MutableClock(InitialTime);
        var account = Account.Register(Guid.NewGuid(), "user@example.com", "hash", null, clock.UtcNow);
        var first = EmailVerification.Issue(Guid.NewGuid(), account.Id, account.Email, _otpHasher.HashOtp("123456", account.Id), clock.UtcNow);
        var accountRepo = new FakeAccountRepository([account]);
        var verificationRepo = new FakeEmailVerificationRepository([first]);
        var outboxWriter = new FakeOutboxWriter();
        var verify = new VerifyEmailOtpCommandHandler(accountRepo, verificationRepo, _otpHasher, outboxWriter, clock);

        foreach (var wrong in new[] { "111111", "222222", "333333" })
            await verify.Handle(new VerifyEmailOtpCommand("user@example.com", wrong), TestContext.Current.CancellationToken);
        Assert.True(first.IsInvalidated);

        clock.UtcNow = InitialTime.AddSeconds(61);
        var resend = new ResendOtpCommandHandler(accountRepo, verificationRepo, new FakeOtpGenerator("654321"), _otpHasher, outboxWriter, clock);
        var resent = await resend.Handle(new ResendOtpCommand("user@example.com"), TestContext.Current.CancellationToken);
        Assert.True(resent.IsSuccess);

        var result = await verify.Handle(new VerifyEmailOtpCommand("user@example.com", "654321"), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(AccountStatus.EmailVerified, account.Status);
    }

    [Fact]
    public void ResendOtpErrors_round_the_remaining_time_up_and_never_below_one_second()
    {
        var now = InitialTime;

        Assert.Equal(1, ResendOtpErrors.SecondsUntil(now.AddMilliseconds(200), now));
        Assert.Equal(2, ResendOtpErrors.SecondsUntil(now.AddSeconds(1.2), now));
        Assert.Equal(1, ResendOtpErrors.SecondsUntil(now.AddSeconds(-5), now));
    }

    [Fact]
    public async Task ResendOtp_issues_new_otp_and_invalidates_previous_active_otp()
    {
        var clock = new MutableClock(InitialTime);
        var account = Account.Register(Guid.NewGuid(), "user@example.com", "hash", null, clock.UtcNow);
        var oldVerification = EmailVerification.Issue(Guid.NewGuid(), account.Id, account.Email, _otpHasher.HashOtp("111111", account.Id), clock.UtcNow);

        // Advance 61 seconds so cooldown has elapsed
        clock.UtcNow = InitialTime.AddSeconds(61);

        var accountRepo = new FakeAccountRepository([account]);
        var verificationRepo = new FakeEmailVerificationRepository([oldVerification]);
        var outboxWriter = new FakeOutboxWriter();
        var otpGenerator = new FakeOtpGenerator("987654");

        var handler = new ResendOtpCommandHandler(accountRepo, verificationRepo, otpGenerator, _otpHasher, outboxWriter, clock);
        var result = await handler.Handle(
            new ResendOtpCommand("  user@example.com "),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal("user@example.com", result.Value!.Email);
        Assert.Equal(300, result.Value.ExpiresInSeconds);
        Assert.Equal(60, result.Value.CooldownSeconds);

        Assert.True(oldVerification.IsInvalidated);
        Assert.Equal(clock.UtcNow, oldVerification.InvalidatedAtUtc);
        var newVerification = Assert.Single(verificationRepo.Saved, v => v.Id != oldVerification.Id);
        Assert.Equal(_otpHasher.HashOtp("987654", account.Id), newVerification.OtpHash);

        var enqueued = Assert.Single(outboxWriter.Enqueued);
        Assert.Equal("identity.user-verification-otp-issued.v1", enqueued.EventType);
        var payload = Assert.IsType<UserVerificationOtpIssuedIntegrationEvent>(enqueued.Payload);
        Assert.Equal(account.Id, payload.AccountId);
        Assert.Equal("987654", payload.OtpCode);
    }

    [Fact]
    public async Task ResendOtp_fails_with_too_many_requests_when_cooldown_is_active()
    {
        var clock = new MutableClock(InitialTime);
        var account = Account.Register(Guid.NewGuid(), "user@example.com", "hash", null, clock.UtcNow);
        var recentVerification = EmailVerification.Issue(Guid.NewGuid(), account.Id, account.Email, _otpHasher.HashOtp("111111", account.Id), clock.UtcNow);

        // Only 30 seconds elapsed (< 60s cooldown)
        clock.UtcNow = InitialTime.AddSeconds(30);

        var accountRepo = new FakeAccountRepository([account]);
        var verificationRepo = new FakeEmailVerificationRepository([recentVerification]);
        var outboxWriter = new FakeOutboxWriter();
        var otpGenerator = new FakeOtpGenerator("987654");

        var handler = new ResendOtpCommandHandler(accountRepo, verificationRepo, otpGenerator, _otpHasher, outboxWriter, clock);
        var result = await handler.Handle(
            new ResendOtpCommand("user@example.com"),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal(ResendOtpErrors.CooldownActiveCode, result.Error!.Code);
        Assert.Equal(30, result.Error.RetryAfterSeconds); // 60 s cooldown, 30 s already elapsed
        Assert.Contains("30 seconds", result.Error.Message);
        Assert.Empty(outboxWriter.Enqueued);
    }

    [Fact]
    public async Task ResendOtp_fails_with_too_many_requests_when_hourly_limit_is_exceeded()
    {
        var clock = new MutableClock(InitialTime);
        var account = Account.Register(Guid.NewGuid(), "user@example.com", "hash", null, clock.UtcNow);

        // 5 verifications in past hour, with last one 70s ago
        var verifications = Enumerable.Range(1, 5)
            .Select(i => EmailVerification.Issue(Guid.NewGuid(), account.Id, account.Email, _otpHasher.HashOtp($"00000{i}", account.Id), InitialTime.AddMinutes(i * 10)))
            .ToList();

        clock.UtcNow = InitialTime.AddMinutes(55);

        var accountRepo = new FakeAccountRepository([account]);
        var verificationRepo = new FakeEmailVerificationRepository(verifications);
        var outboxWriter = new FakeOutboxWriter();
        var otpGenerator = new FakeOtpGenerator("987654");

        var handler = new ResendOtpCommandHandler(accountRepo, verificationRepo, otpGenerator, _otpHasher, outboxWriter, clock);
        var result = await handler.Handle(
            new ResendOtpCommand("user@example.com"),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal(ResendOtpErrors.RateLimitExceededCode, result.Error!.Code);
        Assert.Equal(15 * 60, result.Error.RetryAfterSeconds); // oldest code at +10 min leaves the window at +70 min
        Assert.Empty(outboxWriter.Enqueued);
    }

    [Fact]
    public async Task ResendOtp_returns_silent_success_when_account_does_not_exist_or_is_active()
    {
        var clock = new MutableClock(InitialTime);
        var accountRepo = new FakeAccountRepository([]);
        var verificationRepo = new FakeEmailVerificationRepository([]);
        var outboxWriter = new FakeOutboxWriter();
        var otpGenerator = new FakeOtpGenerator("987654");

        var handler = new ResendOtpCommandHandler(accountRepo, verificationRepo, otpGenerator, _otpHasher, outboxWriter, clock);
        var result = await handler.Handle(
            new ResendOtpCommand("unknown@example.com"),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Empty(outboxWriter.Enqueued);
        Assert.Empty(verificationRepo.Saved);
    }

    [Fact]
    public async Task TC02_OtpConcurrentVerify_GivenValidOtp_WhenTwoConcurrentVerifyRequests_ThenAccountTransitionsEmailVerifiedOnlyOnce()
    {
        var clock = new MutableClock(InitialTime);
        var account = Account.Register(Guid.NewGuid(), "concurrent@example.com", "hash", null, clock.UtcNow);
        var verification = EmailVerification.Issue(Guid.NewGuid(), account.Id, account.Email, _otpHasher.HashOtp("123456", account.Id), clock.UtcNow);

        var accountRepo = new FakeAccountRepository([account]);
        var verificationRepo = new FakeEmailVerificationRepository([verification]);
        var outboxWriter = new FakeOutboxWriter();

        var handler = new VerifyEmailOtpCommandHandler(accountRepo, verificationRepo, _otpHasher, outboxWriter, clock);

        // When 2 verify requests are sent concurrently (serialized in transaction via row-lock)
        var verifyTask1 = handler.Handle(new VerifyEmailOtpCommand("concurrent@example.com", "123456"), TestContext.Current.CancellationToken);
        var verifyTask2 = handler.Handle(new VerifyEmailOtpCommand("concurrent@example.com", "123456"), TestContext.Current.CancellationToken);

        var results = await Task.WhenAll(verifyTask1, verifyTask2);

        // Exactly one transitions to EmailVerified and succeeds; the second receives generic 400 OtpExpired
        var successCount = results.Count(r => r.IsSuccess);
        var expiredCount = results.Count(r => !r.IsSuccess && r.Error == VerifyEmailOtpErrors.OtpExpired);

        Assert.Equal(1, successCount);
        Assert.Equal(1, expiredCount);
        Assert.Equal(AccountStatus.EmailVerified, account.Status);
        Assert.True(verification.IsUsed);

        // Crucial: exactly 1 domain/integration event is published, avoiding duplicate activation events
        var enqueued = Assert.Single(outboxWriter.Enqueued);
        Assert.Equal("identity.account-email-verified.v1", enqueued.EventType);
    }

    [Fact]
    public async Task TC03_OtpThirdFailure_GivenTwoPreviousFailures_WhenThirdIncorrectOtpAttempt_ThenOtpInvalidated()
    {
        var clock = new MutableClock(InitialTime);
        var account = Account.Register(Guid.NewGuid(), "user@example.com", "hash", null, clock.UtcNow);
        var verification = EmailVerification.Issue(Guid.NewGuid(), account.Id, account.Email, _otpHasher.HashOtp("123456", account.Id), clock.UtcNow);

        // Given: 2 previous failed attempts already recorded
        verification.RecordAttempt("111111", _otpHasher, clock.UtcNow);
        verification.RecordAttempt("222222", _otpHasher, clock.UtcNow);
        Assert.Equal(2, verification.AttemptCount);
        Assert.True(verification.CanAttempt());

        var accountRepo = new FakeAccountRepository([account]);
        var verificationRepo = new FakeEmailVerificationRepository([verification]);
        var outboxWriter = new FakeOutboxWriter();
        var handler = new VerifyEmailOtpCommandHandler(accountRepo, verificationRepo, _otpHasher, outboxWriter, clock);

        // When: 3rd incorrect attempt is submitted
        var result = await handler.Handle(
            new VerifyEmailOtpCommand("user@example.com", "333333"),
            TestContext.Current.CancellationToken);

        // Then: OTP is permanently locked / invalidated for further attempts
        Assert.True(result.IsFailure);
        Assert.Equal(VerifyEmailOtpErrors.OtpMaxAttemptsExceeded, result.Error);
        Assert.Equal(3, verification.AttemptCount);
        Assert.False(verification.CanAttempt());
        Assert.False(verification.IsUsed);
        Assert.True(verification.IsInvalidated);
        Assert.Equal(AccountStatus.PendingEmailVerification, account.Status);

        // Subsequent attempt with the CORRECT code is also rejected
        var subsequentResult = await handler.Handle(
            new VerifyEmailOtpCommand("user@example.com", "123456"),
            TestContext.Current.CancellationToken);

        Assert.True(subsequentResult.IsFailure);
        Assert.Equal(VerifyEmailOtpErrors.OtpMaxAttemptsExceeded, subsequentResult.Error);
    }

    [Fact]
    public async Task TC07_ResendEnumeration_GivenUnknownOrVerifiedOrPendingEmail_WhenResendCalled_ThenPublicResponsesAreIdentical()
    {
        var clock = new MutableClock(InitialTime);
        var pendingAccount = Account.Register(Guid.NewGuid(), "pending@example.com", "hash", null, clock.UtcNow);
        var verifiedAccount = Account.Register(Guid.NewGuid(), "verified@example.com", "hash", null, clock.UtcNow);
        verifiedAccount.VerifyEmail();

        var accountRepo = new FakeAccountRepository([pendingAccount, verifiedAccount]);
        var verificationRepo = new FakeEmailVerificationRepository([]);
        var outboxWriter = new FakeOutboxWriter();
        var otpGenerator = new FakeOtpGenerator("654321");
        var handler = new ResendOtpCommandHandler(accountRepo, verificationRepo, otpGenerator, _otpHasher, outboxWriter, clock);

        // 1. Unknown email
        var unknownResult = await handler.Handle(new ResendOtpCommand("unknown@example.com"), TestContext.Current.CancellationToken);

        // 2. Already verified email
        var verifiedResult = await handler.Handle(new ResendOtpCommand("verified@example.com"), TestContext.Current.CancellationToken);

        // 3. Pending email
        var pendingResult = await handler.Handle(new ResendOtpCommand("pending@example.com"), TestContext.Current.CancellationToken);

        // Public responses MUST be completely identical to prevent account enumeration
        Assert.True(unknownResult.IsSuccess);
        Assert.True(verifiedResult.IsSuccess);
        Assert.True(pendingResult.IsSuccess);

        Assert.Equal(unknownResult.Value!.ExpiresInSeconds, verifiedResult.Value!.ExpiresInSeconds);
        Assert.Equal(verifiedResult.Value!.ExpiresInSeconds, pendingResult.Value!.ExpiresInSeconds);

        Assert.Equal(unknownResult.Value!.CooldownSeconds, verifiedResult.Value!.CooldownSeconds);
        Assert.Equal(verifiedResult.Value!.CooldownSeconds, pendingResult.Value!.CooldownSeconds);

        Assert.Equal(unknownResult.Value!.Message, verifiedResult.Value!.Message);
        Assert.Equal(verifiedResult.Value!.Message, pendingResult.Value!.Message);

        // Internal behavior distinction: only pending actually issues an OTP and writes to outbox
        var singleOutboxEvent = Assert.Single(outboxWriter.Enqueued);
        Assert.Equal("identity.user-verification-otp-issued.v1", singleOutboxEvent.EventType);
        Assert.Single(verificationRepo.Saved); // Only 1 OTP saved (for pending account)
    }

    [Fact]
    public void HmacSha256OtpHasher_generates_different_hashes_for_different_peppers()
    {
        var options1 = Options.Create(new OtpSecurityOptions { Pepper = "pepper-one-123456" });
        var options2 = Options.Create(new OtpSecurityOptions { Pepper = "pepper-two-654321" });

        var hasher1 = new HmacSha256OtpHasher(options1);
        var hasher2 = new HmacSha256OtpHasher(options2);

        var accountId = Guid.NewGuid();
        var hash1 = hasher1.HashOtp("123456", accountId);
        var hash2 = hasher2.HashOtp("123456", accountId);

        Assert.NotEqual(hash1, hash2);
        Assert.True(hasher1.VerifyOtp("123456", accountId, hash1));
        Assert.False(hasher2.VerifyOtp("123456", accountId, hash1));
    }

    [Fact]
    public void HmacSha256OtpHasher_throws_when_pepper_is_empty()
    {
        var emptyOptions = Options.Create(new OtpSecurityOptions { Pepper = "   " });
        Assert.Throws<InvalidOperationException>(() => new HmacSha256OtpHasher(emptyOptions));
    }

    [Fact]
    public void Account_VerifyEmail_rejects_transition_from_non_pending_status()
    {
        var account = Account.Register(Guid.NewGuid(), "user@example.com", "hash", null, InitialTime);
        Assert.Equal(AccountStatus.PendingEmailVerification, account.Status);

        // First transition succeeds
        Assert.True(account.VerifyEmail());
        Assert.Equal(AccountStatus.EmailVerified, account.Status);

        // Second transition is rejected (idempotent guard)
        Assert.False(account.VerifyEmail());
    }

    [Fact]
    public void SecureOtpGenerator_generates_cryptographic_6_digit_otp_and_rejects_other_lengths()
    {
        var generator = new SecureOtpGenerator();

        var first = generator.GenerateNumericOtp(6);
        var second = generator.GenerateNumericOtp();

        Assert.Equal(6, first.Length);
        Assert.Equal(6, second.Length);
        Assert.True(int.TryParse(first, out _));
        Assert.True(int.TryParse(second, out _));

        Assert.Throws<ArgumentOutOfRangeException>(() => generator.GenerateNumericOtp(4));
        Assert.Throws<ArgumentOutOfRangeException>(() => generator.GenerateNumericOtp(10));
    }

    private sealed class MutableClock(DateTimeOffset utcNow) : IClock
    {
        public DateTimeOffset UtcNow { get; set; } = utcNow;
    }

    private sealed class FakeOtpGenerator(string fixedOtp) : IOtpGenerator
    {
        public string GenerateNumericOtp(int length = 6) => fixedOtp;
    }

    private sealed class FakeOtpHasher(string pepper = "test-otp-pepper-secret") : IOtpHasher
    {
        public string HashOtp(string otpCode, Guid accountId)
        {
            var rawBytes = System.Text.Encoding.UTF8.GetBytes($"{accountId:D}:{otpCode.Trim()}");
            var hashBytes = System.Security.Cryptography.HMACSHA256.HashData(System.Text.Encoding.UTF8.GetBytes(pepper), rawBytes);
            return Convert.ToHexString(hashBytes);
        }

        public bool VerifyOtp(string candidateOtp, Guid accountId, string expectedHash) =>
            HashOtp(candidateOtp, accountId) == expectedHash;
    }

    private sealed class FakeAccountRepository(IEnumerable<Account> initial) : IAccountRepository
    {
        private readonly List<Account> accounts = [..initial];

        public Task<bool> EmailExistsAsync(string normalizedEmail, CancellationToken cancellationToken) =>
            Task.FromResult(accounts.Any(a => a.Email == normalizedEmail));

        public Task<Account?> FindByEmailAsync(string normalizedEmail, CancellationToken cancellationToken) =>
            Task.FromResult(accounts.FirstOrDefault(a => a.Email == normalizedEmail));

        public Task<Account?> FindByEmailForUpdateAsync(string normalizedEmail, CancellationToken cancellationToken) =>
            FindByEmailAsync(normalizedEmail, cancellationToken);

        public Task<Account?> FindByIdAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult(accounts.FirstOrDefault(a => a.Id == id));

        public Task<Account?> FindByIdForUpdateAsync(Guid id, CancellationToken cancellationToken) =>
            FindByIdAsync(id, cancellationToken);

        public void Add(Account account) => accounts.Add(account);

        public Task<SaveAccountResult> SaveNewAccountAsync(CancellationToken cancellationToken) =>
            Task.FromResult(SaveAccountResult.Success);

        public Task<bool> TrySaveChangesAsync(CancellationToken cancellationToken) => Task.FromResult(true);

        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<TResult> ExecuteInTransactionAsync<TResult>(Func<Task<TResult>> action, CancellationToken cancellationToken) =>
            action();
    }

    private sealed class FakeEmailVerificationRepository(IEnumerable<EmailVerification> initial) : IEmailVerificationRepository
    {
        public List<EmailVerification> Saved { get; } = [..initial];

        public Task<EmailVerification?> GetLatestActiveForAccountAsync(Guid accountId, CancellationToken cancellationToken) =>
            Task.FromResult(Saved
                .Where(v => v.AccountId == accountId && !v.IsUsed && v.InvalidatedAtUtc == null)
                .OrderByDescending(v => v.CreatedAtUtc)
                .FirstOrDefault());

        public Task<int> CountRecentResendsInPastHourAsync(string normalizedEmail, DateTimeOffset sinceUtc, CancellationToken cancellationToken) =>
            Task.FromResult(Saved.Count(v => v.Email == normalizedEmail && v.CreatedAtUtc >= sinceUtc));

        public Task<EmailVerification?> GetLatestResendInCooldownAsync(string normalizedEmail, DateTimeOffset sinceUtc, CancellationToken cancellationToken) =>
            Task.FromResult(Saved
                .Where(v => v.Email == normalizedEmail && v.CreatedAtUtc >= sinceUtc)
                .OrderByDescending(v => v.CreatedAtUtc)
                .FirstOrDefault());

        public Task<EmailVerification?> GetLatestUnusedForAccountAsync(Guid accountId, CancellationToken cancellationToken) =>
            Task.FromResult(Saved
                .Where(v => v.AccountId == accountId && !v.IsUsed)
                .OrderByDescending(v => v.CreatedAtUtc)
                .FirstOrDefault());

        public Task<DateTimeOffset?> GetOldestIssuedSinceAsync(string normalizedEmail, DateTimeOffset sinceUtc, CancellationToken cancellationToken) =>
            Task.FromResult(Saved
                .Where(v => v.Email == normalizedEmail && v.CreatedAtUtc >= sinceUtc)
                .OrderBy(v => v.CreatedAtUtc)
                .Select(v => (DateTimeOffset?)v.CreatedAtUtc)
                .FirstOrDefault());

        public void Add(EmailVerification emailVerification) => Saved.Add(emailVerification);

        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class FakeOutboxWriter : IOutboxWriter
    {
        public List<(object Payload, string EventType, int Version)> Enqueued { get; } = [];

        public void Enqueue<TEvent>(TEvent integrationEvent, string eventType, int eventVersion, Guid? correlationId = null, Guid? causationId = null) where TEvent : class
        {
            Enqueued.Add((integrationEvent, eventType, eventVersion));
        }
    }
}
