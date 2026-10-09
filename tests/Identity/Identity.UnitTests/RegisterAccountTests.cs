using ParkingSystem.BuildingBlocks.Time;
using ParkingSystem.Contracts.Events.Identity;
using ParkingSystem.Identity.Application.Accounts;
using ParkingSystem.Identity.Application.Accounts.RegisterAccount;
using ParkingSystem.Identity.Application.Common.Messaging.Outbox;
using ParkingSystem.Identity.Application.Common.Security;
using ParkingSystem.Identity.Application.Profiles;
using ParkingSystem.Identity.Domain.Accounts;
using ParkingSystem.Identity.Domain.EmailVerifications;
using ParkingSystem.Identity.Domain.Profiles;
using ParkingSystem.Identity.Infrastructure.Security;
using Xunit;

namespace ParkingSystem.Tests.Identity.UnitTests;

public sealed class RegisterAccountTests
{
    private const string ValidFullName = "Nguyễn Văn An";
    private const string ValidPassword = "Secret#123";
    private readonly RegisterAccountCommandValidator validator = new();

    private static RegisterAccountCommand CreateValidCommand(
        string? fullName = null,
        string? email = null,
        string? password = null,
        string? phoneNumber = null) =>
        new(
            fullName ?? ValidFullName,
            email ?? "user@example.com",
            password ?? ValidPassword,
            phoneNumber);

    [Fact]
    public void Validator_accepts_valid_full_registration()
    {
        Assert.True(validator.Validate(CreateValidCommand()).IsValid);
    }

    [Theory]
    [InlineData("A")]
    [InlineData("John123")]
    [InlineData("John @Doe")]
    [InlineData("John  Doe")]
    public void Validator_rejects_invalid_full_name(string invalidName)
    {
        var result = validator.Validate(CreateValidCommand(fullName: invalidName));
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(RegisterAccountCommand.FullName));
    }

    [Theory]
    [InlineData("Trần Thị Bích Ngọc")]
    [InlineData("Nguyễn Văn An")]
    [InlineData("Le Van Ba")]
    [InlineData("John Doe")]
    [InlineData("Anne-Marie")]
    [InlineData("Jean-Luc")]
    [InlineData("O'Connor")]
    [InlineData("Nguyen-An")]
    public void Validator_accepts_valid_unicode_vietnamese_names(string validName)
    {
        var result = validator.Validate(CreateValidCommand(fullName: validName));
        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("Anne--Marie")]
    [InlineData("O''Connor")]
    [InlineData("-Anne")]
    [InlineData("Marie-")]
    [InlineData("Anne'")]
    [InlineData("'Anne")]
    public void Validator_rejects_malformed_punctuated_names(string invalidName)
    {
        var result = validator.Validate(CreateValidCommand(fullName: invalidName));
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(RegisterAccountCommand.FullName));
    }

    [Theory]
    [InlineData("Sh#1a")]          // shorter than 8
    [InlineData("secret#123")]     // no upper case
    [InlineData("SECRET#123")]     // no lower case
    [InlineData("Secret#abc")]     // no digit
    [InlineData("Secret1234")]     // no special character
    [InlineData("Aa1@ầầầầầầầầầầầầầầầầầầầầầầầầ")] // 29 characters but 79 bytes in UTF-8
    public void Validator_rejects_passwords_outside_the_policy(string password)
    {
        var result = validator.Validate(CreateValidCommand(password: password));
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(RegisterAccountCommand.Password));
    }

    [Theory]
    [InlineData("not-an-email")]
    [InlineData("")]
    public void Validator_rejects_invalid_email(string email)
    {
        var result = validator.Validate(CreateValidCommand(email: email));
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(RegisterAccountCommand.Email));
    }

    [Theory]
    [InlineData("0901234567", true)]
    [InlineData("+84901234567", true)]
    [InlineData("090-123", false)]
    [InlineData("abc", false)]
    [InlineData("1234567890", false)]
    [InlineData("090123456", false)]
    [InlineData("09012345678", false)]
    [InlineData("+8490123456", false)]
    [InlineData("+849012345678", false)]
    [InlineData("0312345678", true)]
    [InlineData(" 0901234567 ", true)]
    [InlineData("+84012345678", false)]
    [InlineData("0123456789", false)]
    [InlineData("0212345678", false)]
    [InlineData("0000000000", false)]
    [InlineData("84901234567", false)]
    public void Validator_checks_phone_number_only_when_supplied(string phoneNumber, bool expectedValid)
    {
        var result = validator.Validate(CreateValidCommand(phoneNumber: phoneNumber));
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

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validator_accepts_registration_without_a_full_name(string? fullName)
    {
        var result = validator.Validate(new RegisterAccountCommand(fullName, "user@example.com", ValidPassword, null));
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validator_accepts_a_password_of_exactly_72_utf8_bytes()
    {
        var password = "Aa1@" + new string('ầ', 22) + "xx"; // 4 + 22 * 3 + 2 = 72 bytes
        Assert.Equal(72, System.Text.Encoding.UTF8.GetByteCount(password));

        var result = validator.Validate(CreateValidCommand(password: password));

        Assert.DoesNotContain(result.Errors, error => error.PropertyName == nameof(RegisterAccountCommand.Password));
    }

    [Fact]
    public async Task Handler_registers_without_a_profile_when_no_name_is_given()
    {
        var accountRepository = new FakeAccountRepository();
        var profileRepository = new FakeProfileRepository();
        var verificationRepository = new FakeEmailVerificationRepository();
        var outboxWriter = new FakeOutboxWriter();
        var handler = new RegisterAccountCommandHandler(
            accountRepository,
            profileRepository,
            verificationRepository,
            new FakePasswordHasher(),
            new FakeOtpGenerator("123456"),
            new FakeOtpHasher(),
            outboxWriter,
            new FixedClock());

        var result = await handler.Handle(
            new RegisterAccountCommand(null, "noname@example.com", ValidPassword, null),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value!.FullName);
        Assert.Single(accountRepository.Saved);
        Assert.Empty(profileRepository.Saved);
        Assert.Single(verificationRepository.Saved);
        Assert.Single(outboxWriter.Enqueued);
    }

    [Fact]
    public async Task Handler_registers_pending_account_and_profile_and_issues_otp()
    {
        var accountRepository = new FakeAccountRepository();
        var profileRepository = new FakeProfileRepository();
        var verificationRepository = new FakeEmailVerificationRepository();
        var outboxWriter = new FakeOutboxWriter();
        var otpGenerator = new FakeOtpGenerator("123456");
        var otpHasher = new FakeOtpHasher();
        var handler = new RegisterAccountCommandHandler(
            accountRepository,
            profileRepository,
            verificationRepository,
            new FakePasswordHasher(),
            otpGenerator,
            otpHasher,
            outboxWriter,
            new FixedClock());

        var command = CreateValidCommand(
            fullName: "Nguyễn Văn An",
            email: "  User@Example.COM ",
            phoneNumber: "0901234567");

        var result = await handler.Handle(command, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        var account = Assert.Single(accountRepository.Saved);
        Assert.Equal("user@example.com", account.Email);
        Assert.Equal("hashed:" + ValidPassword, account.PasswordHash);
        Assert.Equal("+84901234567", account.PhoneNumber);
        Assert.Equal(AccountStatus.PendingEmailVerification, account.Status);
        Assert.Equal(FixedClock.Now, account.CreatedAtUtc);

        var profile = Assert.Single(profileRepository.Saved);
        Assert.Equal(account.Id, profile.AccountId);
        Assert.Equal("Nguyễn Văn An", profile.FullName);
        Assert.Null(profile.DateOfBirth);
        Assert.Equal(FixedClock.Now, profile.CreatedAtUtc);

        Assert.Equal(new RegisteredAccount(account.Id, "user@example.com", "Nguyễn Văn An", "PendingEmailVerification"), result.Value);

        // US-AUTH-001 & US-AUTH-003: verify initial OTP issuance and outbox enqueue
        var verification = Assert.Single(verificationRepository.Saved);
        Assert.Equal(account.Id, verification.AccountId);
        Assert.Equal("user@example.com", verification.Email);
        Assert.Equal(otpHasher.HashOtp("123456", account.Id), verification.OtpHash);

        var enqueued = Assert.Single(outboxWriter.Enqueued);
        Assert.Equal("identity.user-verification-otp-issued.v1", enqueued.EventType);
        var payload = Assert.IsType<UserVerificationOtpIssuedIntegrationEvent>(enqueued.Payload);
        Assert.Equal(account.Id, payload.AccountId);
        Assert.Equal("123456", payload.OtpCode);
    }

    [Fact]
    public async Task Handler_rejects_an_email_that_is_already_registered()
    {
        var accountRepository = new FakeAccountRepository { ExistingEmail = "user@example.com" };
        var profileRepository = new FakeProfileRepository();
        var verificationRepository = new FakeEmailVerificationRepository();
        var outboxWriter = new FakeOutboxWriter();
        var handler = new RegisterAccountCommandHandler(
            accountRepository,
            profileRepository,
            verificationRepository,
            new FakePasswordHasher(),
            new FakeOtpGenerator("123456"),
            new FakeOtpHasher(),
            outboxWriter,
            new FixedClock());

        var result = await handler.Handle(CreateValidCommand(email: "user@example.com"), TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(RegisterAccountErrors.EmailAlreadyRegistered, result.Error);
        Assert.Empty(accountRepository.Saved);
        Assert.Empty(profileRepository.Saved);
        Assert.Empty(verificationRepository.Saved);
        Assert.Empty(outboxWriter.Enqueued);
    }

    [Fact]
    public async Task Handler_reports_phone_conflict_when_the_phone_belongs_to_another_account()
    {
        var accountRepository = new FakeAccountRepository { FailSaveWithDuplicatePhone = true };
        var handler = new RegisterAccountCommandHandler(
            accountRepository,
            new FakeProfileRepository(),
            new FakeEmailVerificationRepository(),
            new FakePasswordHasher(),
            new FakeOtpGenerator("123456"),
            new FakeOtpHasher(),
            new FakeOutboxWriter(),
            new FixedClock());

        var result = await handler.Handle(CreateValidCommand(email: "new@example.com"), TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(RegisterAccountErrors.PhoneAlreadyRegistered, result.Error);
        Assert.Empty(accountRepository.Saved);
    }

    [Fact]
    public async Task Handler_reports_a_duplicate_when_concurrent_save_fails_on_unique_index_and_account_already_verified()
    {
        var existingVerified = Account.Register(Guid.NewGuid(), "user@example.com", "hash", null, FixedClock.Now);
        existingVerified.VerifyEmail();
        var accountRepository = new FakeAccountRepository { FailSaveWithDuplicate = true };
        accountRepository.Saved.Add(existingVerified);
        var profileRepository = new FakeProfileRepository();
        var verificationRepository = new FakeEmailVerificationRepository();
        var outboxWriter = new FakeOutboxWriter();
        var handler = new RegisterAccountCommandHandler(
            accountRepository,
            profileRepository,
            verificationRepository,
            new FakePasswordHasher(),
            new FakeOtpGenerator("123456"),
            new FakeOtpHasher(),
            outboxWriter,
            new FixedClock());

        var result = await handler.Handle(CreateValidCommand(email: "user@example.com"), TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(RegisterAccountErrors.EmailAlreadyRegistered, result.Error);
    }

    [Fact]
    public async Task Handler_rejects_when_concurrent_save_fails_on_unique_index_and_account_is_pending()
    {
        var existingPending = Account.Register(Guid.NewGuid(), "user@example.com", "hash", null, FixedClock.Now);
        var accountRepository = new FakeAccountRepository { FailSaveWithDuplicate = true };
        accountRepository.Saved.Add(existingPending);
        var handler = new RegisterAccountCommandHandler(
            accountRepository,
            new FakeProfileRepository(),
            new FakeEmailVerificationRepository(),
            new FakePasswordHasher(),
            new FakeOtpGenerator("123456"),
            new FakeOtpHasher(),
            new FakeOutboxWriter(),
            new FixedClock());

        var result = await handler.Handle(CreateValidCommand(email: "user@example.com"), TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(RegisterAccountErrors.EmailAlreadyRegistered, result.Error);
    }

    [Fact]
    public async Task Handler_rejects_an_email_that_is_waiting_for_verification()
    {
        var existing = Account.Register(Guid.NewGuid(), "user@example.com", "hash", null, FixedClock.Now);
        var accountRepository = new FakeAccountRepository();
        accountRepository.Saved.Add(existing);
        var verificationRepository = new FakeEmailVerificationRepository();
        var outboxWriter = new FakeOutboxWriter();
        var handler = new RegisterAccountCommandHandler(
            accountRepository,
            new FakeProfileRepository(),
            verificationRepository,
            new FakePasswordHasher(),
            new FakeOtpGenerator("123456"),
            new FakeOtpHasher(),
            outboxWriter,
            new FixedClock());

        var result = await handler.Handle(CreateValidCommand(email: "user@example.com", fullName: "New Name"), TestContext.Current.CancellationToken);

        // US-AUTH-001 (decided 2026-10-08): pending emails are rejected too; no new OTP, nothing leaks.
        Assert.False(result.IsSuccess);
        Assert.Equal(RegisterAccountErrors.EmailAlreadyRegistered, result.Error);
        Assert.Empty(verificationRepository.Saved);
        Assert.Empty(outboxWriter.Enqueued);
    }

    [Fact]
    public async Task TC01_ConcurrentRegistration_GivenUnregisteredEmail_WhenTwoConcurrentRegistrations_ThenOnlyOneAccountCreatedAndBothGetConsistentBusinessState()
    {
        var accountRepository = new ConcurrentRaceFakeAccountRepository();
        var profileRepository = new FakeProfileRepository();
        var verificationRepository = new FakeEmailVerificationRepository();
        var outboxWriter = new FakeOutboxWriter();
        var handler = new RegisterAccountCommandHandler(
            accountRepository,
            profileRepository,
            verificationRepository,
            new FakePasswordHasher(),
            new FakeOtpGenerator("123456"),
            new FakeOtpHasher(),
            outboxWriter,
            new FixedClock());

        var command1 = CreateValidCommand(email: "concurrent@example.com", fullName: "User One");
        var command2 = CreateValidCommand(email: "concurrent@example.com", fullName: "User Two");

        // When: Two concurrent registrations pass initial email lookup simultaneously
        var task1 = handler.Handle(command1, TestContext.Current.CancellationToken);
        var task2 = handler.Handle(command2, TestContext.Current.CancellationToken);
        var results = await Task.WhenAll(task1, task2);

        // The winner is created; the loser hits the unique index and gets email_already_registered.
        Assert.Equal(1, results.Count(r => r.IsSuccess));
        Assert.Single(results, r => r.Error == RegisterAccountErrors.EmailAlreadyRegistered);
        Assert.Single(accountRepository.Saved);
        Assert.Equal("concurrent@example.com", accountRepository.Saved[0].Email);
    }

    [Fact]
    public async Task TC06_PendingRegistrationRetry_GivenPendingAccountAndExpiredOtp_WhenRegisterCalledAgain_ThenRejectsAndDoesNotReissueOtp()
    {
        var existingAccount = Account.Register(Guid.NewGuid(), "pending@example.com", "hash", null, FixedClock.Now.AddHours(-1));
        var expiredVerification = EmailVerification.Issue(
            Guid.NewGuid(),
            existingAccount.Id,
            existingAccount.Email,
            "expired-hash",
            FixedClock.Now.AddHours(-1));

        var accountRepository = new FakeAccountRepository();
        accountRepository.Saved.Add(existingAccount);
        var verificationRepository = new FakeEmailVerificationRepository();
        verificationRepository.Saved.Add(expiredVerification);
        var outboxWriter = new FakeOutboxWriter();
        var handler = new RegisterAccountCommandHandler(
            accountRepository,
            new FakeProfileRepository(),
            verificationRepository,
            new FakePasswordHasher(),
            new FakeOtpGenerator("654321"),
            new FakeOtpHasher(),
            outboxWriter,
            new FixedClock());

        var result = await handler.Handle(CreateValidCommand(email: "pending@example.com"), TestContext.Current.CancellationToken);

        // 409: the client sends the user to POST /resend-otp, which is the only way to get a new code.
        Assert.False(result.IsSuccess);
        Assert.Equal(RegisterAccountErrors.EmailAlreadyRegistered, result.Error);
        Assert.Single(verificationRepository.Saved);
        Assert.Empty(outboxWriter.Enqueued);
    }

    [Fact]
    public void BCrypt_hashes_and_verifies_passwords_correctly()
    {
        var hasher = new BCryptPasswordHasher();
        var first = hasher.Hash(ValidPassword);
        var second = hasher.Hash(ValidPassword);

        Assert.NotEqual(ValidPassword, first);
        Assert.NotEqual(first, second);
        Assert.True(hasher.Verify(ValidPassword, first));
        Assert.False(hasher.Verify("Wrong#123", first));

        // Feedback Item 8: Verify that VerifyDummy executes without exception and performs timing mitigation
        var dummyException = Record.Exception(() => hasher.VerifyDummy("AnyAttemptPassword#123"));
        Assert.Null(dummyException);
    }

    private sealed class FakeAccountRepository : IAccountRepository
    {
        private readonly List<Account> pending = [];

        public string? ExistingEmail { get; init; }
        public bool FailSaveWithDuplicate { get; init; }
        public bool FailSaveWithDuplicatePhone { get; init; }
        public List<Account> Saved { get; } = [];

        public Task<bool> EmailExistsAsync(string normalizedEmail, CancellationToken cancellationToken) =>
            Task.FromResult(normalizedEmail == ExistingEmail);

        public Task<Account?> FindByEmailAsync(string normalizedEmail, CancellationToken cancellationToken)
        {
            if (ExistingEmail is not null && normalizedEmail == ExistingEmail)
            {
                var existing = Account.Register(Guid.NewGuid(), ExistingEmail, "hash", null, FixedClock.Now);
                existing.VerifyEmail();
                return Task.FromResult<Account?>(existing);
            }

            return Task.FromResult(Saved.FirstOrDefault(account => account.Email == normalizedEmail));
        }

        public Task<Account?> FindByEmailForUpdateAsync(string normalizedEmail, CancellationToken cancellationToken) =>
            FindByEmailAsync(normalizedEmail, cancellationToken);

        public Task<Account?> FindByIdAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult(Saved.Concat(pending).FirstOrDefault(account => account.Id == id));

        public Task<Account?> FindByIdForUpdateAsync(Guid id, CancellationToken cancellationToken) =>
            FindByIdAsync(id, cancellationToken);

        public void Add(Account account) => pending.Add(account);

        public Task<SaveAccountResult> SaveNewAccountAsync(CancellationToken cancellationToken)
        {
            if (FailSaveWithDuplicatePhone)
            {
                pending.Clear();
                return Task.FromResult(SaveAccountResult.DuplicatePhoneConflict);
            }

            if (FailSaveWithDuplicate || pending.Any(p => Saved.Any(s => s.Email == p.Email)))
            {
                pending.Clear();
                return Task.FromResult(SaveAccountResult.DuplicateEmailConflict);
            }
            Saved.AddRange(pending);
            pending.Clear();
            return Task.FromResult(SaveAccountResult.Success);
        }

        public async Task<bool> TrySaveChangesAsync(CancellationToken cancellationToken)
        {
            var res = await SaveNewAccountAsync(cancellationToken);
            return res == SaveAccountResult.Success;
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

    private sealed class FakeProfileRepository : IProfileRepository
    {
        public List<Profile> Saved { get; } = [];

        public void Add(Profile profile) => Saved.Add(profile);

        public Task<Profile?> FindByAccountIdAsync(Guid accountId, CancellationToken cancellationToken) =>
            Task.FromResult(Saved.FirstOrDefault(p => p.AccountId == accountId));
    }

    private sealed class FakePasswordHasher : IPasswordHasher
    {
        public string Hash(string password) => "hashed:" + password;

        public bool Verify(string password, string passwordHash) => passwordHash == "hashed:" + password;

        public void VerifyDummy(string password) { }
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

    private sealed class FixedClock : IClock
    {
        public static readonly DateTimeOffset Now = new(2026, 10, 6, 12, 0, 0, TimeSpan.Zero);

        public DateTimeOffset UtcNow => Now;
    }

    private sealed class ConcurrentRaceFakeAccountRepository : IAccountRepository
    {
        private readonly object _lock = new();
        public List<Account> Saved { get; } = [];

        public Task<bool> EmailExistsAsync(string normalizedEmail, CancellationToken cancellationToken) =>
            Task.FromResult(false);

        public Task<Account?> FindByEmailAsync(string normalizedEmail, CancellationToken cancellationToken)
        {
            lock (_lock)
            {
                return Task.FromResult(Saved.FirstOrDefault(a => a.Email == normalizedEmail));
            }
        }

        public Task<Account?> FindByEmailForUpdateAsync(string normalizedEmail, CancellationToken cancellationToken) =>
            FindByEmailAsync(normalizedEmail, cancellationToken);

        public Task<Account?> FindByIdAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult<Account?>(null);

        public Task<Account?> FindByIdForUpdateAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult<Account?>(null);

        public void Add(Account account)
        {
            // Staged in memory
        }

        public Task<SaveAccountResult> SaveNewAccountAsync(CancellationToken cancellationToken)
        {
            lock (_lock)
            {
                if (Saved.Count > 0)
                    return Task.FromResult(SaveAccountResult.DuplicateEmailConflict); // Unique constraint violation

                Saved.Add(Account.Register(Guid.NewGuid(), "concurrent@example.com", "hash", null, FixedClock.Now));
                return Task.FromResult(SaveAccountResult.Success);
            }
        }

        public async Task<bool> TrySaveChangesAsync(CancellationToken cancellationToken)
        {
            var res = await SaveNewAccountAsync(cancellationToken);
            return res == SaveAccountResult.Success;
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<TResult> ExecuteInTransactionAsync<TResult>(Func<Task<TResult>> action, CancellationToken cancellationToken) =>
            action();
    }
}
