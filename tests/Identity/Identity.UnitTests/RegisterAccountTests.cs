using ParkingSystem.BuildingBlocks.Time;
using ParkingSystem.Identity.Application.Accounts;
using ParkingSystem.Identity.Application.Accounts.RegisterAccount;
using ParkingSystem.Identity.Application.Common.Security;
using ParkingSystem.Identity.Domain.Accounts;
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
    public void Validator_checks_phone_number_only_when_supplied(string phoneNumber, bool expectedValid)
    {
        var result = validator.Validate(new RegisterAccountCommand("user@example.com", ValidPassword, phoneNumber));

        Assert.Equal(expectedValid, result.IsValid);
    }

    [Fact]
    public async Task Handler_registers_pending_account_with_normalized_email_and_hashed_password()
    {
        var repository = new FakeAccountRepository();
        var handler = new RegisterAccountCommandHandler(repository, new FakePasswordHasher(), new FixedClock());

        var result = await handler.Handle(
            new RegisterAccountCommand("  User@Example.COM ", ValidPassword, "0901234567"),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        var account = Assert.Single(repository.Saved);
        Assert.Equal("user@example.com", account.Email);
        Assert.Equal("hashed:" + ValidPassword, account.PasswordHash);
        Assert.Equal(AccountStatus.PendingEmailVerification, account.Status);
        Assert.Equal(FixedClock.Now, account.CreatedAtUtc);
        Assert.Equal(new RegisteredAccount(account.Id, "user@example.com", "PendingEmailVerification"), result.Value);
    }

    [Fact]
    public async Task Handler_rejects_an_email_that_is_already_registered()
    {
        var repository = new FakeAccountRepository { ExistingEmail = "user@example.com" };
        var handler = new RegisterAccountCommandHandler(repository, new FakePasswordHasher(), new FixedClock());

        var result = await handler.Handle(
            new RegisterAccountCommand("USER@example.com", ValidPassword, null),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal(RegisterAccountErrors.EmailAlreadyRegistered, result.Error);
        Assert.Empty(repository.Saved);
    }

    [Fact]
    public async Task Handler_reports_conflict_when_a_concurrent_registration_wins_the_unique_index()
    {
        var repository = new FakeAccountRepository { FailSaveWithDuplicate = true };
        var handler = new RegisterAccountCommandHandler(repository, new FakePasswordHasher(), new FixedClock());

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

        public void Add(Account account) => pending.Add(account);

        public Task<bool> TrySaveChangesAsync(CancellationToken cancellationToken)
        {
            if (FailSaveWithDuplicate)
                return Task.FromResult(false);
            Saved.AddRange(pending);
            pending.Clear();
            return Task.FromResult(true);
        }
    }

    private sealed class FakePasswordHasher : IPasswordHasher
    {
        public string Hash(string password) => "hashed:" + password;

        public bool Verify(string password, string passwordHash) => passwordHash == "hashed:" + password;
    }

    private sealed class FixedClock : IClock
    {
        public static readonly DateTimeOffset Now = new(2026, 10, 3, 12, 0, 0, TimeSpan.Zero);

        public DateTimeOffset UtcNow => Now;
    }
}
