using ParkingSystem.BuildingBlocks.Time;
using ParkingSystem.Identity.Application.Accounts;
using ParkingSystem.Identity.Application.Accounts.RegisterAccount;
using ParkingSystem.Identity.Application.Common.Messaging.Outbox;
using ParkingSystem.Identity.Application.Common.Security;
using ParkingSystem.Identity.Domain.Accounts;
using ParkingSystem.Identity.Domain.EmailVerifications;
using Xunit;

namespace ParkingSystem.Tests.Identity.UnitTests;

public sealed class RegisterAccountTests
{
    private const string ValidPassword = "Secret#123";
    private readonly RegisterAccountCommandValidator validator = new();

    [Fact]
    public void Validator_accepts_email_and_password_without_phone_number()
    {
        Assert.True(validator.Validate(new RegisterAccountCommand("user@example.com", ValidPassword, null)).IsValid);
    }

    [Theory]
    [InlineData("Sh#1a")]          // shorter than 8
    [InlineData("secret#123")]     // no upper case
    [InlineData("SECRET#123")]     // no lower case
    [InlineData("Secret#abc")]     // no digit
    [InlineData("Secret1234")]     // no special character
    public void Validator_rejects_passwords_outside_the_policy(string password)
    {
        var result = validator.Validate(new RegisterAccountCommand("user@example.com", password, null));

        Assert.Contains(result.Errors, error => error.PropertyName == nameof(RegisterAccountCommand.Password));
    }

    [Theory]
    [InlineData("not-an-email")]
    [InlineData("")]
    public void Validator_rejects_invalid_email(string email)
    {
        var result = validator.Validate(new RegisterAccountCommand(email, ValidPassword, null));

        Assert.Contains(result.Errors, error => error.PropertyName == nameof(RegisterAccountCommand.Email));
    }

    [Theory]
    [InlineData("0901234567", true)]
    [InlineData("+84901234567", true)]
    [InlineData("090-123", false)]
    [InlineData("abc", false)]
    [InlineData("1234567890", false)]   // does not start with 0 or +84
    [InlineData("090123456", false)]     // 9 digits (too short)
    [InlineData("09012345678", false)]   // 11 digits (too long)
    [InlineData("+8490123456", false)]   // +84 followed by 8 digits (too short)
    [InlineData("+849012345678", false)] // +84 followed by 10 digits (too long)
    [InlineData("0312345678", true)]
    [InlineData(" 0901234567 ", true)]   // surrounding spaces are trimmed when stored
    [InlineData("+84012345678", false)]  // +84 must not be followed by 0
    [InlineData("0123456789", false)]    // 1 is not a mobile prefix
    [InlineData("0212345678", false)]    // landline prefix
    [InlineData("0000000000", false)]
    [InlineData("84901234567", false)]   // country code without '+'
    public void Validator_checks_phone_number_only_when_supplied(string phoneNumber, bool expectedValid)
    {
        var result = validator.Validate(new RegisterAccountCommand("user@example.com", ValidPassword, phoneNumber));

        Assert.Equal(expectedValid, result.IsValid);
    }

    [Theory]
    [InlineData("0901234567", "+84901234567")]
    [InlineData("+84901234567", "+84901234567")]
    [InlineData(" 0901234567 ", "+84901234567")]
    [InlineData(null, null)]
    [InlineData("  ", null)]
    public void Account_stores_phone_number_in_plus84_form(string? phoneNumber, string? expected)
    {
        Assert.Equal(expected, Account.NormalizePhoneNumber(phoneNumber));
    }

    [Fact]
    public async Task Handler_registers_pending_account_with_normalized_email_and_hashed_password()
    {
        var repository = new FakeAccountRepository();
        var verificationRepository = new FakeEmailVerificationRepository();
        var outboxWriter = new FakeOutboxWriter();
        var otpGenerator = new FakeOtpGenerator("123456");
        var otpHasher = new FakeOtpHasher();
        var handler = new RegisterAccountCommandHandler(
            repository,
            verificationRepository,
            new FakePasswordHasher(),
            otpGenerator,
            otpHasher,
            outboxWriter,
            new FixedClock());

        var result = await handler.Handle(
            new RegisterAccountCommand("  User@Example.COM ", ValidPassword, "0901234567"),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        var account = Assert.Single(repository.Saved);
        Assert.Equal("user@example.com", account.Email);
        Assert.Equal("hashed:" + ValidPassword, account.PasswordHash);
        Assert.Equal("+84901234567", account.PhoneNumber);
        Assert.Equal(AccountStatus.PendingEmailVerification, account.Status);
        Assert.Equal(FixedClock.Now, account.CreatedAtUtc);
        Assert.Equal(new RegisteredAccount(account.Id, "user@example.com", "PendingEmailVerification"), result.Value);

        // US-AUTH-001 & US-AUTH-003: verify initial OTP issuance and outbox enqueue
        var verification = Assert.Single(verificationRepository.Saved);
        Assert.Equal(account.Id, verification.AccountId);
        Assert.Equal("user@example.com", verification.Email);
        Assert.Equal(otpHasher.HashOtp("123456", account.Id), verification.OtpHash);

        var enqueued = Assert.Single(outboxWriter.Enqueued);
        Assert.Equal("identity.user-verification-otp-issued.v1", enqueued.EventType);
        var payload = Assert.IsType<ParkingSystem.BuildingBlocks.Events.UserVerificationOtpIssuedIntegrationEvent>(enqueued.Payload);
        Assert.Equal(account.Id, payload.AccountId);
        Assert.Equal("123456", payload.OtpCode);
    }

    [Fact]
    public async Task Handler_rejects_an_email_that_is_already_registered()
    {
        var repository = new FakeAccountRepository { ExistingEmail = "user@example.com" };
        var verificationRepository = new FakeEmailVerificationRepository();
        var outboxWriter = new FakeOutboxWriter();
        var otpHasher = new FakeOtpHasher();
        var handler = new RegisterAccountCommandHandler(
            repository,
            verificationRepository,
            new FakePasswordHasher(),
            new FakeOtpGenerator("123456"),
            otpHasher,
            outboxWriter,
            new FixedClock());

        var result = await handler.Handle(
            new RegisterAccountCommand("USER@example.com", ValidPassword, null),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal(RegisterAccountErrors.EmailAlreadyRegistered, result.Error);
        Assert.Empty(repository.Saved);
        Assert.Empty(verificationRepository.Saved);
        Assert.Empty(outboxWriter.Enqueued);
    }

    [Fact]
    public async Task Handler_reports_conflict_when_a_concurrent_registration_wins_the_unique_index()
    {
        var repository = new FakeAccountRepository { FailSaveWithDuplicate = true };
        var verificationRepository = new FakeEmailVerificationRepository();
        var outboxWriter = new FakeOutboxWriter();
        var otpHasher = new FakeOtpHasher();
        var handler = new RegisterAccountCommandHandler(
            repository,
            verificationRepository,
            new FakePasswordHasher(),
            new FakeOtpGenerator("123456"),
            otpHasher,
            outboxWriter,
            new FixedClock());

        var result = await handler.Handle(
            new RegisterAccountCommand("user@example.com", ValidPassword, null),
            TestContext.Current.CancellationToken);

        Assert.Equal(RegisterAccountErrors.EmailAlreadyRegistered, result.Error);
    }

    [Fact]
    public void Bcrypt_hasher_produces_a_verifiable_salted_hash()
    {
        var hasher = new ParkingSystem.Identity.Infrastructure.Security.BCryptPasswordHasher();

        var first = hasher.Hash(ValidPassword);
        var second = hasher.Hash(ValidPassword);

        Assert.NotEqual(ValidPassword, first);
        Assert.NotEqual(first, second);
        Assert.True(hasher.Verify(ValidPassword, first));
        Assert.False(hasher.Verify("Wrong#123", first));
    }

    private sealed class FakeAccountRepository : IAccountRepository
    {
        private readonly List<Account> pending = [];

        public string? ExistingEmail { get; init; }
        public bool FailSaveWithDuplicate { get; init; }
        public List<Account> Saved { get; } = [];

        public Task<bool> EmailExistsAsync(string normalizedEmail, CancellationToken cancellationToken) =>
            Task.FromResult(normalizedEmail == ExistingEmail);

        public Task<Account?> FindByEmailAsync(string normalizedEmail, CancellationToken cancellationToken) =>
            Task.FromResult(Saved.Concat(pending).FirstOrDefault(account => account.Email == normalizedEmail));

        public Task<Account?> FindByEmailForUpdateAsync(string normalizedEmail, CancellationToken cancellationToken) =>
            FindByEmailAsync(normalizedEmail, cancellationToken);

        public Task<Account?> FindByIdAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult(Saved.Concat(pending).FirstOrDefault(account => account.Id == id));

        public void Add(Account account) => pending.Add(account);

        public Task<bool> TrySaveChangesAsync(CancellationToken cancellationToken)
        {
            if (FailSaveWithDuplicate)
                return Task.FromResult(false);
            Saved.AddRange(pending);
            pending.Clear();
            return Task.FromResult(true);
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken)
        {
            Saved.AddRange(pending);
            pending.Clear();
            return Task.CompletedTask;
        }

        public Task<TResult> ExecuteInTransactionAsync<TResult>(Func<Task<TResult>> action, CancellationToken cancellationToken) =>
            action();
    }

    private sealed class FakePasswordHasher : IPasswordHasher
    {
        public string Hash(string password) => "hashed:" + password;

        public bool Verify(string password, string passwordHash) => passwordHash == "hashed:" + password;
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

    private sealed class FakeOtpGenerator(string fixedOtp) : IOtpGenerator
    {
        public string GenerateNumericOtp(int length = 6) => fixedOtp;
    }

    private sealed class FakeEmailVerificationRepository : IEmailVerificationRepository
    {
        public List<EmailVerification> Saved { get; } = [];

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

    private sealed class FixedClock : IClock
    {
        public static readonly DateTimeOffset Now = new(2026, 10, 3, 12, 0, 0, TimeSpan.Zero);

        public DateTimeOffset UtcNow => Now;
    }
}

