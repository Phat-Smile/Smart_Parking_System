using ParkingSystem.BuildingBlocks.Time;
using ParkingSystem.Identity.Application.Accounts;
using ParkingSystem.Identity.Application.Accounts.Events;
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

    [Theory]
    [InlineData("user@example.com", "123456", true)]
    [InlineData("user@example.com", "000000", true)]
    [InlineData("invalid-email", "123456", false)]
    [InlineData("", "123456", false)]
    [InlineData("user@example.com", "12345", false)]
    [InlineData("user@example.com", "1234567", false)]
    [InlineData("user@example.com", "abcdef", false)]
    [InlineData("user@example.com", "", false)]
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
        var verification = EmailVerification.Issue(Guid.NewGuid(), account.Id, account.Email, "654321", clock.UtcNow);

        var accountRepo = new FakeAccountRepository([account]);
        var verificationRepo = new FakeEmailVerificationRepository([verification]);
        var outboxWriter = new FakeOutboxWriter();

        var handler = new VerifyEmailOtpCommandHandler(accountRepo, verificationRepo, outboxWriter, clock);
        var result = await handler.Handle(
            new VerifyEmailOtpCommand("  User@Example.COM ", "654321"),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(account.Id, result.Value!.AccountId);
        Assert.Equal("user@example.com", result.Value.Email);
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

        var handler = new VerifyEmailOtpCommandHandler(accountRepo, verificationRepo, outboxWriter, clock);
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

        var handler = new VerifyEmailOtpCommandHandler(accountRepo, verificationRepo, outboxWriter, clock);
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
        var verification = EmailVerification.Issue(Guid.NewGuid(), account.Id, account.Email, "123456", clock.UtcNow);

        // Advance past 5 minutes
        clock.UtcNow = InitialTime.AddMinutes(5).AddSeconds(1);

        var accountRepo = new FakeAccountRepository([account]);
        var verificationRepo = new FakeEmailVerificationRepository([verification]);
        var outboxWriter = new FakeOutboxWriter();

        var handler = new VerifyEmailOtpCommandHandler(accountRepo, verificationRepo, outboxWriter, clock);
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
        var verification = EmailVerification.Issue(Guid.NewGuid(), account.Id, account.Email, "123456", clock.UtcNow);

        var accountRepo = new FakeAccountRepository([account]);
        var verificationRepo = new FakeEmailVerificationRepository([verification]);
        var outboxWriter = new FakeOutboxWriter();

        var handler = new VerifyEmailOtpCommandHandler(accountRepo, verificationRepo, outboxWriter, clock);

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

        // Attempt 4: even with correct code, now locked
        var fourthResult = await handler.Handle(
            new VerifyEmailOtpCommand("user@example.com", "123456"),
            TestContext.Current.CancellationToken);

        Assert.True(fourthResult.IsFailure);
        Assert.Equal(VerifyEmailOtpErrors.OtpMaxAttemptsExceeded, fourthResult.Error);
        Assert.Equal(AccountStatus.PendingEmailVerification, account.Status);
    }

    [Fact]
    public async Task ResendOtp_issues_new_otp_and_invalidates_previous_active_otp()
    {
        var clock = new MutableClock(InitialTime);
        var account = Account.Register(Guid.NewGuid(), "user@example.com", "hash", null, clock.UtcNow);
        var oldVerification = EmailVerification.Issue(Guid.NewGuid(), account.Id, account.Email, "111111", clock.UtcNow);

        // Advance 61 seconds so cooldown has elapsed
        clock.UtcNow = InitialTime.AddSeconds(61);

        var accountRepo = new FakeAccountRepository([account]);
        var verificationRepo = new FakeEmailVerificationRepository([oldVerification]);
        var outboxWriter = new FakeOutboxWriter();
        var otpGenerator = new FakeOtpGenerator("987654");

        var handler = new ResendOtpCommandHandler(accountRepo, verificationRepo, otpGenerator, outboxWriter, clock);
        var result = await handler.Handle(
            new ResendOtpCommand("  user@example.com "),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal("user@example.com", result.Value!.Email);
        Assert.Equal(300, result.Value.ExpiresInSeconds);
        Assert.Equal(60, result.Value.CooldownSeconds);

        Assert.True(oldVerification.IsUsed); // Old one invalidated
        var newVerification = Assert.Single(verificationRepo.Saved, v => v.Id != oldVerification.Id);
        Assert.Equal("987654", newVerification.OtpCode);

        var enqueued = Assert.Single(outboxWriter.Enqueued);
        Assert.Equal("identity.send-email-otp.v1", enqueued.EventType);
        var payload = Assert.IsType<SendEmailOtpIntegrationEvent>(enqueued.Payload);
        Assert.Equal(account.Id, payload.AccountId);
        Assert.Equal("987654", payload.OtpCode);
    }

    [Fact]
    public async Task ResendOtp_fails_with_too_many_requests_when_cooldown_is_active()
    {
        var clock = new MutableClock(InitialTime);
        var account = Account.Register(Guid.NewGuid(), "user@example.com", "hash", null, clock.UtcNow);
        var recentVerification = EmailVerification.Issue(Guid.NewGuid(), account.Id, account.Email, "111111", clock.UtcNow);

        // Only 30 seconds elapsed (< 60s cooldown)
        clock.UtcNow = InitialTime.AddSeconds(30);

        var accountRepo = new FakeAccountRepository([account]);
        var verificationRepo = new FakeEmailVerificationRepository([recentVerification]);
        var outboxWriter = new FakeOutboxWriter();
        var otpGenerator = new FakeOtpGenerator("987654");

        var handler = new ResendOtpCommandHandler(accountRepo, verificationRepo, otpGenerator, outboxWriter, clock);
        var result = await handler.Handle(
            new ResendOtpCommand("user@example.com"),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal(ResendOtpErrors.CooldownActive, result.Error);
        Assert.Empty(outboxWriter.Enqueued);
    }

    [Fact]
    public async Task ResendOtp_fails_with_too_many_requests_when_hourly_limit_is_exceeded()
    {
        var clock = new MutableClock(InitialTime);
        var account = Account.Register(Guid.NewGuid(), "user@example.com", "hash", null, clock.UtcNow);

        // 5 verifications in past hour, with last one 70s ago
        var verifications = Enumerable.Range(1, 5)
            .Select(i => EmailVerification.Issue(Guid.NewGuid(), account.Id, account.Email, $"00000{i}", InitialTime.AddMinutes(i * 10)))
            .ToList();

        clock.UtcNow = InitialTime.AddMinutes(55); // 55 mins into hour

        var accountRepo = new FakeAccountRepository([account]);
        var verificationRepo = new FakeEmailVerificationRepository(verifications);
        var outboxWriter = new FakeOutboxWriter();
        var otpGenerator = new FakeOtpGenerator("987654");

        var handler = new ResendOtpCommandHandler(accountRepo, verificationRepo, otpGenerator, outboxWriter, clock);
        var result = await handler.Handle(
            new ResendOtpCommand("user@example.com"),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal(ResendOtpErrors.RateLimitExceeded, result.Error);
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

        var handler = new ResendOtpCommandHandler(accountRepo, verificationRepo, otpGenerator, outboxWriter, clock);
        var result = await handler.Handle(
            new ResendOtpCommand("unknown@example.com"),
            TestContext.Current.CancellationToken);

        // Silent success to prevent account enumeration
        Assert.True(result.IsSuccess);
        Assert.Empty(outboxWriter.Enqueued);
        Assert.Empty(verificationRepo.Saved);
    }

    [Fact]
    public void SecureOtpGenerator_generates_cryptographic_6_digit_otp()
    {
        var generator = new SecureOtpGenerator();

        var first = generator.GenerateNumericOtp(6);
        var second = generator.GenerateNumericOtp(6);

        Assert.Equal(6, first.Length);
        Assert.Equal(6, second.Length);
        Assert.True(int.TryParse(first, out _));
        Assert.True(int.TryParse(second, out _));
    }

    private sealed class MutableClock(DateTimeOffset utcNow) : IClock
    {
        public DateTimeOffset UtcNow { get; set; } = utcNow;
    }

    private sealed class FakeOtpGenerator(string fixedOtp) : IOtpGenerator
    {
        public string GenerateNumericOtp(int length = 6) => fixedOtp;
    }

    private sealed class FakeAccountRepository(IEnumerable<Account> initial) : IAccountRepository
    {
        private readonly List<Account> accounts = [..initial];

        public Task<bool> EmailExistsAsync(string normalizedEmail, CancellationToken cancellationToken) =>
            Task.FromResult(accounts.Any(a => a.Email == normalizedEmail));

        public Task<Account?> FindByEmailAsync(string normalizedEmail, CancellationToken cancellationToken) =>
            Task.FromResult(accounts.FirstOrDefault(a => a.Email == normalizedEmail));

        public Task<Account?> FindByIdAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult(accounts.FirstOrDefault(a => a.Id == id));

        public void Add(Account account) => accounts.Add(account);

        public Task<bool> TrySaveChangesAsync(CancellationToken cancellationToken) => Task.FromResult(true);
    }

    private sealed class FakeEmailVerificationRepository(IEnumerable<EmailVerification> initial) : IEmailVerificationRepository
    {
        public List<EmailVerification> Saved { get; } = [..initial];

        public Task<EmailVerification?> GetLatestActiveForAccountAsync(Guid accountId, CancellationToken cancellationToken) =>
            Task.FromResult(Saved
                .Where(v => v.AccountId == accountId && !v.IsUsed)
                .OrderByDescending(v => v.CreatedAtUtc)
                .FirstOrDefault());

        public Task<int> CountRecentResendsInPastHourAsync(string normalizedEmail, DateTimeOffset sinceUtc, CancellationToken cancellationToken) =>
            Task.FromResult(Saved.Count(v => v.Email == normalizedEmail && v.CreatedAtUtc >= sinceUtc));

        public Task<EmailVerification?> GetLatestResendInCooldownAsync(string normalizedEmail, DateTimeOffset sinceUtc, CancellationToken cancellationToken) =>
            Task.FromResult(Saved
                .Where(v => v.Email == normalizedEmail && v.CreatedAtUtc >= sinceUtc)
                .OrderByDescending(v => v.CreatedAtUtc)
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
