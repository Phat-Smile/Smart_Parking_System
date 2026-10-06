using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using ParkingSystem.BuildingBlocks.Time;
using ParkingSystem.Identity.Application.Accounts;
using ParkingSystem.Identity.Application.Accounts.Login;
using ParkingSystem.Identity.Application.Common.Security;
using ParkingSystem.Identity.Domain.Accounts;
using ParkingSystem.Identity.Infrastructure.Security;
using Xunit;

namespace ParkingSystem.Tests.Identity.UnitTests;

public sealed class LoginTests
{
    private static readonly DateTimeOffset InitialTime = new(2026, 10, 5, 12, 0, 0, TimeSpan.Zero);
    private const string CorrectPassword = "SecurePassword@123";
    private const string WrongPassword = "WrongPassword@999";
    private const string CorrectPasswordHash = "hash:SecurePassword@123";

    [Theory]
    [InlineData("user@example.com", "any-password", true)]
    [InlineData("user@example.com", "123", true)] // Complexity not required on login
    [InlineData("invalid-email", "any-password", false)]
    [InlineData("", "any-password", false)]
    [InlineData("user@example.com", "", false)]
    public void Validator_checks_email_and_password(string email, string password, bool expectedValid)
    {
        var validator = new LoginCommandValidator();
        var result = validator.Validate(new LoginCommand(email, password));

        Assert.Equal(expectedValid, result.IsValid);
    }

    [Fact]
    public void Validator_rejects_password_longer_than_72_characters()
    {
        var validator = new LoginCommandValidator();
        var result = validator.Validate(new LoginCommand("user@example.com", new string('a', 73)));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(LoginCommand.Password));
    }

    [Fact]
    public async Task Scenario1_Login_succeeds_with_correct_credentials_and_verified_email()
    {
        var clock = new MutableClock(InitialTime);
        var account = Account.Register(Guid.NewGuid(), "user@example.com", CorrectPasswordHash, "+84901234567", clock.UtcNow);
        account.VerifyEmail();

        var accountRepo = new FakeAccountRepository([account]);
        var refreshTokenRepo = new FakeRefreshTokenRepository();
        var passwordHasher = new FakePasswordHasher();
        var jwtIssuer = new FakeJwtTokenIssuer("signed-jwt-token", 900);
        var tokenGen = new FakeRefreshTokenGenerator("raw-refresh-token", "hash-refresh-token");

        var handler = new LoginCommandHandler(accountRepo, refreshTokenRepo, passwordHasher, jwtIssuer, tokenGen, clock);
        var result = await handler.Handle(new LoginCommand("  User@Example.COM ", CorrectPassword), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal("signed-jwt-token", result.Value!.AccessToken);
        Assert.Equal("Bearer", result.Value.TokenType);
        Assert.Equal(900, result.Value.ExpiresIn);
        Assert.Equal("raw-refresh-token", result.Value.RefreshToken);
        Assert.Equal(account.Id, result.Value.Account.Id);
        Assert.Equal("user@example.com", result.Value.Account.Email);
        Assert.Equal("EmailVerified", result.Value.Account.Status);
        Assert.Equal("Customer", result.Value.Account.Role);

        var savedRefreshToken = Assert.Single(refreshTokenRepo.Saved);
        Assert.Equal(account.Id, savedRefreshToken.AccountId);
        Assert.Equal("hash-refresh-token", savedRefreshToken.TokenHash);
        Assert.Equal(clock.UtcNow.AddDays(7), savedRefreshToken.ExpiresAtUtc);
    }

    [Fact]
    public async Task Scenario2_Login_fails_with_invalid_credentials_when_email_does_not_exist()
    {
        var clock = new MutableClock(InitialTime);
        var accountRepo = new FakeAccountRepository([]);
        var refreshTokenRepo = new FakeRefreshTokenRepository();
        var passwordHasher = new FakePasswordHasher();
        var jwtIssuer = new FakeJwtTokenIssuer("token", 900);
        var tokenGen = new FakeRefreshTokenGenerator("raw", "hash");

        var handler = new LoginCommandHandler(accountRepo, refreshTokenRepo, passwordHasher, jwtIssuer, tokenGen, clock);
        var result = await handler.Handle(new LoginCommand("unknown@example.com", "any-password"), TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal(LoginErrors.InvalidCredentials, result.Error);
        Assert.True(passwordHasher.DummyVerificationInvoked);
        Assert.Empty(refreshTokenRepo.Saved);
    }

    [Fact]
    public async Task Scenario3_Login_fails_attempt_1_increments_failed_attempts()
    {
        var clock = new MutableClock(InitialTime);
        var account = Account.Register(Guid.NewGuid(), "user@example.com", CorrectPasswordHash, null, clock.UtcNow);
        account.VerifyEmail();

        var accountRepo = new FakeAccountRepository([account]);
        var refreshTokenRepo = new FakeRefreshTokenRepository();
        var passwordHasher = new FakePasswordHasher();
        var jwtIssuer = new FakeJwtTokenIssuer("token", 900);
        var tokenGen = new FakeRefreshTokenGenerator("raw", "hash");

        var handler = new LoginCommandHandler(accountRepo, refreshTokenRepo, passwordHasher, jwtIssuer, tokenGen, clock);
        var result = await handler.Handle(new LoginCommand("user@example.com", WrongPassword), TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal(LoginErrors.InvalidCredentials, result.Error);
        Assert.Equal(1, account.FailedLoginAttempts);
        Assert.Null(account.LockoutEndUtc);
        Assert.False(account.IsLockedOut(clock.UtcNow));
    }

    [Fact]
    public async Task Scenario4_Login_fails_attempt_2_increments_failed_attempts()
    {
        var clock = new MutableClock(InitialTime);
        var account = Account.Register(Guid.NewGuid(), "user@example.com", CorrectPasswordHash, null, clock.UtcNow);
        account.VerifyEmail();
        account.RecordFailedLogin(clock.UtcNow); // attempt 1

        var accountRepo = new FakeAccountRepository([account]);
        var refreshTokenRepo = new FakeRefreshTokenRepository();
        var passwordHasher = new FakePasswordHasher();
        var jwtIssuer = new FakeJwtTokenIssuer("token", 900);
        var tokenGen = new FakeRefreshTokenGenerator("raw", "hash");

        var handler = new LoginCommandHandler(accountRepo, refreshTokenRepo, passwordHasher, jwtIssuer, tokenGen, clock);
        var result = await handler.Handle(new LoginCommand("user@example.com", WrongPassword), TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal(LoginErrors.InvalidCredentials, result.Error);
        Assert.Equal(2, account.FailedLoginAttempts);
        Assert.Null(account.LockoutEndUtc);
    }

    [Fact]
    public async Task Scenario5_Login_fails_attempt_3_locks_account_for_15_minutes()
    {
        var clock = new MutableClock(InitialTime);
        var account = Account.Register(Guid.NewGuid(), "user@example.com", CorrectPasswordHash, null, clock.UtcNow);
        account.VerifyEmail();
        account.RecordFailedLogin(clock.UtcNow); // attempt 1
        account.RecordFailedLogin(clock.UtcNow); // attempt 2

        var accountRepo = new FakeAccountRepository([account]);
        var refreshTokenRepo = new FakeRefreshTokenRepository();
        var passwordHasher = new FakePasswordHasher();
        var jwtIssuer = new FakeJwtTokenIssuer("token", 900);
        var tokenGen = new FakeRefreshTokenGenerator("raw", "hash");

        var handler = new LoginCommandHandler(accountRepo, refreshTokenRepo, passwordHasher, jwtIssuer, tokenGen, clock);
        var result = await handler.Handle(new LoginCommand("user@example.com", WrongPassword), TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal(LoginErrors.AccountTemporarilyLocked, result.Error);
        Assert.Equal(3, account.FailedLoginAttempts);
        Assert.NotNull(account.LockoutEndUtc);
        Assert.Equal(clock.UtcNow.AddMinutes(15), account.LockoutEndUtc);
        Assert.True(account.IsLockedOut(clock.UtcNow));
    }

    [Fact]
    public async Task Scenario6_Login_while_locked_out_returns_locked_error_without_checking_password()
    {
        var clock = new MutableClock(InitialTime);
        var account = Account.Register(Guid.NewGuid(), "user@example.com", CorrectPasswordHash, null, clock.UtcNow);
        account.VerifyEmail();
        for (var i = 0; i < 3; i++)
        {
            account.RecordFailedLogin(clock.UtcNow);
        }

        var accountRepo = new FakeAccountRepository([account]);
        var refreshTokenRepo = new FakeRefreshTokenRepository();
        var passwordHasher = new FakePasswordHasher();
        var jwtIssuer = new FakeJwtTokenIssuer("token", 900);
        var tokenGen = new FakeRefreshTokenGenerator("raw", "hash");

        var handler = new LoginCommandHandler(accountRepo, refreshTokenRepo, passwordHasher, jwtIssuer, tokenGen, clock);

        // Advance 5 minutes (still in lockout)
        clock.UtcNow = InitialTime.AddMinutes(5);

        var result = await handler.Handle(new LoginCommand("user@example.com", CorrectPassword), TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal(LoginErrors.AccountTemporarilyLocked, result.Error);
        Assert.False(passwordHasher.VerificationInvoked);
    }

    [Fact]
    public async Task Scenario7_After_lockout_expires_correct_password_succeeds_and_resets_counter()
    {
        var clock = new MutableClock(InitialTime);
        var account = Account.Register(Guid.NewGuid(), "user@example.com", CorrectPasswordHash, null, clock.UtcNow);
        account.VerifyEmail();
        for (var i = 0; i < 3; i++)
        {
            account.RecordFailedLogin(clock.UtcNow);
        }

        var accountRepo = new FakeAccountRepository([account]);
        var refreshTokenRepo = new FakeRefreshTokenRepository();
        var passwordHasher = new FakePasswordHasher();
        var jwtIssuer = new FakeJwtTokenIssuer("token", 900);
        var tokenGen = new FakeRefreshTokenGenerator("raw", "hash");

        var handler = new LoginCommandHandler(accountRepo, refreshTokenRepo, passwordHasher, jwtIssuer, tokenGen, clock);

        // Advance past 15 minutes
        clock.UtcNow = InitialTime.AddMinutes(15).AddSeconds(1);

        var result = await handler.Handle(new LoginCommand("user@example.com", CorrectPassword), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, account.FailedLoginAttempts);
        Assert.Null(account.LockoutEndUtc);
    }

    [Fact]
    public async Task Scenario8_After_lockout_expires_wrong_password_resets_old_lock_and_counts_as_attempt_1()
    {
        var clock = new MutableClock(InitialTime);
        var account = Account.Register(Guid.NewGuid(), "user@example.com", CorrectPasswordHash, null, clock.UtcNow);
        account.VerifyEmail();
        for (var i = 0; i < 3; i++)
        {
            account.RecordFailedLogin(clock.UtcNow);
        }

        var accountRepo = new FakeAccountRepository([account]);
        var refreshTokenRepo = new FakeRefreshTokenRepository();
        var passwordHasher = new FakePasswordHasher();
        var jwtIssuer = new FakeJwtTokenIssuer("token", 900);
        var tokenGen = new FakeRefreshTokenGenerator("raw", "hash");

        var handler = new LoginCommandHandler(accountRepo, refreshTokenRepo, passwordHasher, jwtIssuer, tokenGen, clock);

        // Advance past 15 minutes
        clock.UtcNow = InitialTime.AddMinutes(15).AddSeconds(1);

        var result = await handler.Handle(new LoginCommand("user@example.com", WrongPassword), TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal(LoginErrors.InvalidCredentials, result.Error);
        Assert.Equal(1, account.FailedLoginAttempts);
        Assert.Null(account.LockoutEndUtc);
    }

    [Fact]
    public async Task Scenario9_Correct_password_on_pending_account_breaks_streak_and_returns_403()
    {
        var clock = new MutableClock(InitialTime);
        var account = Account.Register(Guid.NewGuid(), "user@example.com", CorrectPasswordHash, null, clock.UtcNow);
        // Note: account is PendingEmailVerification
        account.RecordFailedLogin(clock.UtcNow);
        account.RecordFailedLogin(clock.UtcNow);
        Assert.Equal(2, account.FailedLoginAttempts);

        var accountRepo = new FakeAccountRepository([account]);
        var refreshTokenRepo = new FakeRefreshTokenRepository();
        var passwordHasher = new FakePasswordHasher();
        var jwtIssuer = new FakeJwtTokenIssuer("token", 900);
        var tokenGen = new FakeRefreshTokenGenerator("raw", "hash");

        var handler = new LoginCommandHandler(accountRepo, refreshTokenRepo, passwordHasher, jwtIssuer, tokenGen, clock);
        var result = await handler.Handle(new LoginCommand("user@example.com", CorrectPassword), TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal(LoginErrors.EmailNotVerified, result.Error);
        // Consecutive streak MUST be reset
        Assert.Equal(0, account.FailedLoginAttempts);
        Assert.Null(account.LockoutEndUtc);
        Assert.Empty(refreshTokenRepo.Saved);
    }

    [Fact]
    public void Scenario10_JwtTokenIssuer_generates_valid_rs256_token_with_required_claims()
    {
        using var rsa = RSA.Create(2048);
        var privateKeyPem = rsa.ExportPkcs8PrivateKeyPem();
        var tempKeyPath = Path.Combine(Path.GetTempPath(), $"jwt_test_key_{Guid.NewGuid():N}.pem");
        File.WriteAllText(tempKeyPath, privateKeyPem);

        try
        {
            var options = Options.Create(new JwtIssuerOptions
            {
                Issuer = "ParkingSystem.Identity",
                Audience = "ParkingSystem",
                PrivateKeyPath = tempKeyPath,
                ExpirationMinutes = 15
            });

            using var issuer = new JwtTokenIssuer(options);
            var account = Account.Register(Guid.NewGuid(), "test@smartparking.io", "hash", null, InitialTime);

            var token = issuer.IssueAccessToken(account, InitialTime);
            Assert.NotNull(token);

            var parts = token.Split('.');
            Assert.Equal(3, parts.Length);

            // Decode and verify header
            var headerBytes = Base64UrlDecode(parts[0]);
            using var headerDoc = JsonDocument.Parse(headerBytes);
            Assert.Equal("RS256", headerDoc.RootElement.GetProperty("alg").GetString());
            Assert.Equal("JWT", headerDoc.RootElement.GetProperty("typ").GetString());

            // Decode and verify payload
            var payloadBytes = Base64UrlDecode(parts[1]);
            using var payloadDoc = JsonDocument.Parse(payloadBytes);
            var root = payloadDoc.RootElement;
            Assert.Equal(account.Id.ToString(), root.GetProperty("sub").GetString());
            Assert.Equal("test@smartparking.io", root.GetProperty("email").GetString());
            Assert.Equal("Customer", root.GetProperty("role").GetString());
            Assert.Equal("ParkingSystem.Identity", root.GetProperty("iss").GetString());
            Assert.Equal("ParkingSystem", root.GetProperty("aud").GetString());
            Assert.Equal(InitialTime.ToUnixTimeSeconds(), root.GetProperty("iat").GetInt64());
            Assert.Equal(InitialTime.AddMinutes(15).ToUnixTimeSeconds(), root.GetProperty("exp").GetInt64());

            // Verify signature using the public RSA key
            var dataToVerify = Encoding.UTF8.GetBytes($"{parts[0]}.{parts[1]}");
            var signature = Base64UrlDecode(parts[2]);
            var validSignature = rsa.VerifyData(dataToVerify, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            Assert.True(validSignature);
        }
        finally
        {
            if (File.Exists(tempKeyPath))
                File.Delete(tempKeyPath);
        }
    }

    [Fact]
    public void Scenario11_RefreshTokenGenerator_generates_64_hex_chars_raw_and_sha256_hash()
    {
        var generator = new RefreshTokenGenerator();
        var (rawToken, tokenHash) = generator.GenerateToken();

        Assert.Equal(64, rawToken.Length);
        Assert.Equal(64, tokenHash.Length);

        // Verify SHA-256 hash match
        var computedHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken))).ToLowerInvariant();
        Assert.Equal(computedHash, tokenHash);
    }

    [Fact]
    public async Task Scenario12_Concurrent_failed_logins_serialize_and_lock_account_at_attempt_3()
    {
        var clock = new MutableClock(InitialTime);
        var account = Account.Register(Guid.NewGuid(), "user@example.com", CorrectPasswordHash, null, clock.UtcNow);
        account.VerifyEmail();

        var accountRepo = new FakeAccountRepository([account]);
        var refreshTokenRepo = new FakeRefreshTokenRepository();
        var passwordHasher = new FakePasswordHasher();
        var jwtIssuer = new FakeJwtTokenIssuer("token", 900);
        var tokenGen = new FakeRefreshTokenGenerator("raw", "hash");

        var handler = new LoginCommandHandler(accountRepo, refreshTokenRepo, passwordHasher, jwtIssuer, tokenGen, clock);

        // Run 5 concurrent failed login requests simultaneously
        var tasks = Enumerable.Range(0, 5)
            .Select(_ => handler.Handle(new LoginCommand("user@example.com", WrongPassword), TestContext.Current.CancellationToken))
            .ToList();

        var results = await Task.WhenAll(tasks);

        // All 5 requests must fail
        Assert.All(results, r => Assert.True(r.IsFailure));

        // Exactly 2 requests fail with InvalidCredentials (attempt 1 and 2),
        // and remaining 3 requests fail with AccountTemporarilyLocked (attempt 3 onwards)
        var invalidCredResults = results.Where(r => r.Error == LoginErrors.InvalidCredentials).ToList();
        var lockedResults = results.Where(r => r.Error == LoginErrors.AccountTemporarilyLocked).ToList();

        Assert.Equal(2, invalidCredResults.Count);
        Assert.Equal(3, lockedResults.Count);
        Assert.True(account.IsLockedOut(clock.UtcNow));
        Assert.Equal(3, account.FailedLoginAttempts);
    }

    private static byte[] Base64UrlDecode(string input)
    {
        var output = input.Replace('-', '+').Replace('_', '/');
        switch (output.Length % 4)
        {
            case 2: output += "=="; break;
            case 3: output += "="; break;
        }
        return Convert.FromBase64String(output);
    }

    private sealed class MutableClock(DateTimeOffset utcNow) : IClock
    {
        public DateTimeOffset UtcNow { get; set; } = utcNow;
    }

    private sealed class FakeAccountRepository(IEnumerable<Account> initial) : IAccountRepository
    {
        private readonly List<Account> accounts = [..initial];
        private readonly SemaphoreSlim transactionLock = new(1, 1);

        public Task<bool> EmailExistsAsync(string normalizedEmail, CancellationToken cancellationToken) =>
            Task.FromResult(accounts.Any(a => a.Email == normalizedEmail));

        public Task<Account?> FindByEmailAsync(string normalizedEmail, CancellationToken cancellationToken) =>
            Task.FromResult(accounts.FirstOrDefault(a => a.Email == normalizedEmail));

        public Task<Account?> FindByEmailForUpdateAsync(string normalizedEmail, CancellationToken cancellationToken) =>
            FindByEmailAsync(normalizedEmail, cancellationToken);

        public Task<Account?> FindByIdAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult(accounts.FirstOrDefault(a => a.Id == id));

        public void Add(Account account) => accounts.Add(account);

        public Task<bool> TrySaveChangesAsync(CancellationToken cancellationToken) => Task.FromResult(true);

        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public async Task<TResult> ExecuteInTransactionAsync<TResult>(Func<Task<TResult>> action, CancellationToken cancellationToken)
        {
            await transactionLock.WaitAsync(cancellationToken);
            try
            {
                return await action();
            }
            finally
            {
                transactionLock.Release();
            }
        }
    }

    private sealed class FakeRefreshTokenRepository : IRefreshTokenRepository
    {
        public List<RefreshToken> Saved { get; } = [];

        public void Add(RefreshToken refreshToken) => Saved.Add(refreshToken);

        public Task<RefreshToken?> FindByHashAsync(string tokenHash, CancellationToken cancellationToken) =>
            Task.FromResult(Saved.FirstOrDefault(rt => rt.TokenHash == tokenHash));
    }

    private sealed class FakePasswordHasher : IPasswordHasher
    {
        public bool VerificationInvoked { get; private set; }
        public bool DummyVerificationInvoked { get; private set; }

        public string Hash(string password) => "hash:" + password;

        public bool Verify(string password, string passwordHash)
        {
            VerificationInvoked = true;
            return passwordHash == "hash:" + password;
        }

        public void VerifyDummy(string password)
        {
            DummyVerificationInvoked = true;
        }
    }

    private sealed class FakeJwtTokenIssuer(string token, int expirationSeconds) : IJwtTokenIssuer
    {
        public int AccessTokenExpirationSeconds => expirationSeconds;

        public string IssueAccessToken(Account account, DateTimeOffset nowUtc) => token;
    }

    private sealed class FakeRefreshTokenGenerator(string rawToken, string tokenHash) : IRefreshTokenGenerator
    {
        public (string RawToken, string TokenHash) GenerateToken() => (rawToken, tokenHash);
    }
}
