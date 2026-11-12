using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using ParkingSystem.BuildingBlocks.Time;
using ParkingSystem.Identity.Application.Accounts;
using ParkingSystem.Identity.Application.Accounts.Login;
using ParkingSystem.Identity.Application.Accounts.Mfa;
using ParkingSystem.Identity.Application.Common.Security;
using ParkingSystem.Identity.Domain.Accounts;
using ParkingSystem.Identity.Domain.Mfa;
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

        var authSessionRepo = new FakeAuthSessionRepository();
        var handler = CreateHandler(accountRepo, refreshTokenRepo, passwordHasher, jwtIssuer, tokenGen, clock, authSessionRepo);
        var result = await handler.Handle(new LoginCommand("  User@Example.COM ", CorrectPassword), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal("signed-jwt-token", result.Value!.Session!.AccessToken);
        Assert.Equal("Bearer", result.Value.Session!.TokenType);
        Assert.Equal(900, result.Value.Session!.ExpiresIn);
        Assert.Equal("raw-refresh-token", result.Value.Session!.RefreshToken);
        Assert.Equal(account.Id, result.Value.Session!.Account.Id);
        Assert.Equal("user@example.com", result.Value.Session!.Account.Email);
        Assert.Equal("EmailVerified", result.Value.Session!.Account.Status);
        Assert.Equal("Customer", result.Value.Session!.Account.Role);

        var savedRefreshToken = Assert.Single(refreshTokenRepo.Saved);
        Assert.Equal(account.Id, savedRefreshToken.AccountId);
        Assert.Equal("hash-refresh-token", savedRefreshToken.TokenHash);
        Assert.Equal(clock.UtcNow.AddDays(7), savedRefreshToken.ExpiresAtUtc);

        var savedSession = Assert.Single(authSessionRepo.Saved);
        Assert.Equal(account.Id, savedSession.AccountId);
        Assert.Equal(savedSession.Id, savedRefreshToken.SessionId);
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

        var handler = CreateHandler(accountRepo, refreshTokenRepo, passwordHasher, jwtIssuer, tokenGen, clock);
        var result = await handler.Handle(new LoginCommand("unknown@example.com", "any-password"), TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal(LoginErrors.InvalidCredentials.Code, result.Error!.Code);
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

        var handler = CreateHandler(accountRepo, refreshTokenRepo, passwordHasher, jwtIssuer, tokenGen, clock);
        var result = await handler.Handle(new LoginCommand("user@example.com", WrongPassword), TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal(LoginErrors.InvalidCredentials.Code, result.Error!.Code);
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

        var handler = CreateHandler(accountRepo, refreshTokenRepo, passwordHasher, jwtIssuer, tokenGen, clock);
        var result = await handler.Handle(new LoginCommand("user@example.com", WrongPassword), TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal(LoginErrors.InvalidCredentials.Code, result.Error!.Code);
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

        var handler = CreateHandler(accountRepo, refreshTokenRepo, passwordHasher, jwtIssuer, tokenGen, clock);
        var result = await handler.Handle(new LoginCommand("user@example.com", WrongPassword), TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal(LoginErrors.AccountTemporarilyLocked.Code, result.Error!.Code);
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

        var handler = CreateHandler(accountRepo, refreshTokenRepo, passwordHasher, jwtIssuer, tokenGen, clock);

        // Advance 5 minutes (still in lockout)
        clock.UtcNow = InitialTime.AddMinutes(5);

        var result = await handler.Handle(new LoginCommand("user@example.com", CorrectPassword), TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal(LoginErrors.AccountTemporarilyLocked.Code, result.Error!.Code);
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

        var handler = CreateHandler(accountRepo, refreshTokenRepo, passwordHasher, jwtIssuer, tokenGen, clock);

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

        var handler = CreateHandler(accountRepo, refreshTokenRepo, passwordHasher, jwtIssuer, tokenGen, clock);

        // Advance past 15 minutes
        clock.UtcNow = InitialTime.AddMinutes(15).AddSeconds(1);

        var result = await handler.Handle(new LoginCommand("user@example.com", WrongPassword), TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal(LoginErrors.InvalidCredentials.Code, result.Error!.Code);
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

        var handler = CreateHandler(accountRepo, refreshTokenRepo, passwordHasher, jwtIssuer, tokenGen, clock);
        var result = await handler.Handle(new LoginCommand("user@example.com", CorrectPassword), TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal(LoginErrors.EmailNotVerified.Code, result.Error!.Code);
        // Consecutive streak MUST be reset
        Assert.Equal(0, account.FailedLoginAttempts);
        Assert.Null(account.LockoutEndUtc);
        Assert.Empty(refreshTokenRepo.Saved);
    }

    [Fact]
    public async Task Scenario9b_Correct_password_on_suspended_account_returns_suspended_error()
    {
        var clock = new MutableClock(InitialTime);
        var account = Account.Register(Guid.NewGuid(), "user@example.com", CorrectPasswordHash, null, clock.UtcNow);
        account.VerifyEmail();
        account.Suspend();

        var accountRepo = new FakeAccountRepository([account]);
        var refreshTokenRepo = new FakeRefreshTokenRepository();
        var passwordHasher = new FakePasswordHasher();
        var jwtIssuer = new FakeJwtTokenIssuer("token", 900);
        var tokenGen = new FakeRefreshTokenGenerator("raw", "hash");

        var handler = CreateHandler(accountRepo, refreshTokenRepo, passwordHasher, jwtIssuer, tokenGen, clock);
        var result = await handler.Handle(new LoginCommand("user@example.com", CorrectPassword), TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal(LoginErrors.AccountSuspended.Code, result.Error!.Code);
        Assert.Empty(refreshTokenRepo.Saved);
    }

    [Fact]
    public async Task Scenario9c_Correct_password_on_disabled_account_returns_disabled_error()
    {
        var clock = new MutableClock(InitialTime);
        var account = Account.Register(Guid.NewGuid(), "user@example.com", CorrectPasswordHash, null, clock.UtcNow);
        account.VerifyEmail();
        account.Disable();

        var accountRepo = new FakeAccountRepository([account]);
        var refreshTokenRepo = new FakeRefreshTokenRepository();
        var passwordHasher = new FakePasswordHasher();
        var jwtIssuer = new FakeJwtTokenIssuer("token", 900);
        var tokenGen = new FakeRefreshTokenGenerator("raw", "hash");

        var handler = CreateHandler(accountRepo, refreshTokenRepo, passwordHasher, jwtIssuer, tokenGen, clock);
        var result = await handler.Handle(new LoginCommand("user@example.com", CorrectPassword), TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal(LoginErrors.AccountDisabled.Code, result.Error!.Code);
        Assert.Empty(refreshTokenRepo.Saved);
    }

    [Fact]
    public async Task TC04_LoginConcurrentFailure_GivenFailedLoginAttemptsIsOne_WhenTwoConcurrentFailedLogins_ThenAccountLockedAfterExactlyThreeFailures()
    {
        var clock = new MutableClock(InitialTime);
        var account = Account.Register(Guid.NewGuid(), "user@example.com", CorrectPasswordHash, null, clock.UtcNow);
        account.VerifyEmail();
        account.RecordFailedLogin(clock.UtcNow); // Given: FailedLoginAttempts = 1
        Assert.Equal(1, account.FailedLoginAttempts);

        var accountRepo = new FakeAccountRepository([account]);
        var refreshTokenRepo = new FakeRefreshTokenRepository();
        var passwordHasher = new FakePasswordHasher();
        var jwtIssuer = new FakeJwtTokenIssuer("token", 900);
        var tokenGen = new FakeRefreshTokenGenerator("raw", "hash");

        var handler = CreateHandler(accountRepo, refreshTokenRepo, passwordHasher, jwtIssuer, tokenGen, clock);

        // When: 2 failed logins run concurrently (serialized via row-lock in transaction)
        var loginTask1 = handler.Handle(new LoginCommand("user@example.com", WrongPassword), TestContext.Current.CancellationToken);
        var loginTask2 = handler.Handle(new LoginCommand("user@example.com", WrongPassword), TestContext.Current.CancellationToken);

        var results = await Task.WhenAll(loginTask1, loginTask2);

        // Then: Account must be locked exactly after reaching 3 consecutive failures
        Assert.Equal(3, account.FailedLoginAttempts);
        Assert.NotNull(account.LockoutEndUtc);
        Assert.Equal(clock.UtcNow.AddMinutes(15), account.LockoutEndUtc);
        Assert.True(account.IsLockedOut(clock.UtcNow));

        // One request receives 401 (attempt 2), while the third attempt triggers lockout and receives 423
        var unauthCount = results.Count(r => !r.IsSuccess && r.Error?.Code == LoginErrors.InvalidCredentials.Code);
        var lockedCount = results.Count(r => !r.IsSuccess && r.Error?.Code == LoginErrors.AccountTemporarilyLocked.Code);

        Assert.Equal(1, unauthCount);
        Assert.Equal(1, lockedCount);
    }

    [Fact]
    public async Task TC05_ThirdWrongPassword_GivenFailedAttemptsIsTwo_WhenThirdIncorrectAttempt_ThenLockoutEndUtcIs15MinutesAndReturnsTemporarilyLocked()
    {
        var clock = new MutableClock(InitialTime);
        var account = Account.Register(Guid.NewGuid(), "user@example.com", CorrectPasswordHash, null, clock.UtcNow);
        account.VerifyEmail();
        account.RecordFailedLogin(clock.UtcNow); // Attempt 1
        account.RecordFailedLogin(clock.UtcNow); // Attempt 2
        Assert.Equal(2, account.FailedLoginAttempts);
        Assert.Null(account.LockoutEndUtc);

        var accountRepo = new FakeAccountRepository([account]);
        var refreshTokenRepo = new FakeRefreshTokenRepository();
        var passwordHasher = new FakePasswordHasher();
        var jwtIssuer = new FakeJwtTokenIssuer("token", 900);
        var tokenGen = new FakeRefreshTokenGenerator("raw", "hash");

        var handler = CreateHandler(accountRepo, refreshTokenRepo, passwordHasher, jwtIssuer, tokenGen, clock);

        // When: 3rd incorrect password is entered
        var result = await handler.Handle(new LoginCommand("user@example.com", WrongPassword), TestContext.Current.CancellationToken);

        // Then: Return Error AccountTemporarilyLocked (mapped to HTTP 423 Locked)
        Assert.True(result.IsFailure);
        Assert.Equal(LoginErrors.AccountTemporarilyLocked.Code, result.Error!.Code);

        // And: LockoutEndUtc = now + 15m
        Assert.Equal(3, account.FailedLoginAttempts);
        Assert.NotNull(account.LockoutEndUtc);
        Assert.Equal(clock.UtcNow.AddMinutes(15), account.LockoutEndUtc);
        Assert.True(account.IsLockedOut(clock.UtcNow));
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

            var sessionId = Guid.NewGuid();
            var token = issuer.IssueAccessToken(account, sessionId, "pwd", InitialTime);
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
            Assert.Equal(sessionId.ToString(), root.GetProperty("sid").GetString());
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

        var handler = CreateHandler(accountRepo, refreshTokenRepo, passwordHasher, jwtIssuer, tokenGen, clock);

        // Run 5 concurrent failed login requests simultaneously
        var tasks = Enumerable.Range(0, 5)
            .Select(_ => handler.Handle(new LoginCommand("user@example.com", WrongPassword), TestContext.Current.CancellationToken))
            .ToList();

        var results = await Task.WhenAll(tasks);

        // All 5 requests must fail
        Assert.All(results, r => Assert.True(r.IsFailure));

        // Exactly 2 requests fail with InvalidCredentials (attempt 1 and 2),
        // and remaining 3 requests fail with AccountTemporarilyLocked (attempt 3 onwards)
        var invalidCredResults = results.Where(r => r.Error?.Code == LoginErrors.InvalidCredentials.Code).ToList();
        var lockedResults = results.Where(r => r.Error?.Code == LoginErrors.AccountTemporarilyLocked.Code).ToList();

        Assert.Equal(2, invalidCredResults.Count);
        Assert.Equal(3, lockedResults.Count);
        Assert.True(account.IsLockedOut(clock.UtcNow));
        Assert.Equal(3, account.FailedLoginAttempts);
    }

    [Fact]
    public async Task Wrong_passwords_tell_how_many_tries_are_left_then_how_long_the_lock_lasts()
    {
        var clock = new MutableClock(InitialTime);
        var account = Account.Register(Guid.NewGuid(), "user@example.com", CorrectPasswordHash, null, clock.UtcNow);
        account.VerifyEmail();
        var handler = CreateHandler(new FakeAccountRepository([account]), new FakeRefreshTokenRepository(), new FakePasswordHasher(),
            new FakeJwtTokenIssuer("token", 900), new FakeRefreshTokenGenerator("raw", "hash"), clock);

        var first = await handler.Handle(new LoginCommand("user@example.com", WrongPassword), TestContext.Current.CancellationToken);
        var second = await handler.Handle(new LoginCommand("user@example.com", WrongPassword), TestContext.Current.CancellationToken);
        var third = await handler.Handle(new LoginCommand("user@example.com", WrongPassword), TestContext.Current.CancellationToken);
        clock.UtcNow = InitialTime.AddMinutes(10).AddSeconds(30);
        var whileLocked = await handler.Handle(new LoginCommand("user@example.com", CorrectPassword), TestContext.Current.CancellationToken);

        Assert.Equal("Email hoặc mật khẩu không chính xác. Bạn còn 2 lần thử trước khi tài khoản bị tạm khóa 15 phút.", first.Error!.Message);
        Assert.Equal("Email hoặc mật khẩu không chính xác. Bạn còn 1 lần thử trước khi tài khoản bị tạm khóa 15 phút.", second.Error!.Message);
        Assert.Equal(LoginErrors.AccountTemporarilyLocked.Code, third.Error!.Code);
        Assert.Equal("Tài khoản đang tạm khóa do nhập sai mật khẩu 3 lần liên tiếp. Vui lòng thử lại sau 15 phút, hoặc dùng \"Quên mật khẩu\" để mở khóa ngay.", third.Error.Message);
        Assert.Equal(15 * 60, third.Error.RetryAfterSeconds);
        Assert.Equal("Tài khoản đang tạm khóa do nhập sai mật khẩu 3 lần liên tiếp. Vui lòng thử lại sau 5 phút, hoặc dùng \"Quên mật khẩu\" để mở khóa ngay.", whileLocked.Error!.Message);
        Assert.Equal(4 * 60 + 30, whileLocked.Error.RetryAfterSeconds);
    }

    [Fact]
    public async Task Unknown_email_gets_the_plain_invalid_credentials_message_without_a_try_count()
    {
        var handler = CreateHandler(new FakeAccountRepository([]), new FakeRefreshTokenRepository(), new FakePasswordHasher(),
            new FakeJwtTokenIssuer("token", 900), new FakeRefreshTokenGenerator("raw", "hash"), new MutableClock(InitialTime));

        var result = await handler.Handle(new LoginCommand("nobody@example.com", WrongPassword), TestContext.Current.CancellationToken);

        Assert.Equal(LoginErrors.InvalidCredentials, result.Error);
    }

    [Fact]
    public async Task Password_login_on_a_google_account_suggests_google_sign_in()
    {
        var clock = new MutableClock(InitialTime);
        var account = Account.RegisterWithGoogle(Guid.NewGuid(), "google@example.com", "google-sub", clock.UtcNow);
        var handler = CreateHandler(new FakeAccountRepository([account]), new FakeRefreshTokenRepository(), new FakePasswordHasher(),
            new FakeJwtTokenIssuer("token", 900), new FakeRefreshTokenGenerator("raw", "hash"), clock);

        var result = await handler.Handle(new LoginCommand("google@example.com", WrongPassword), TestContext.Current.CancellationToken);

        Assert.Equal(LoginErrors.PasswordNotSet, result.Error);
        Assert.Contains("Tiếp tục với Google", result.Error!.Message);
        Assert.Equal(1, account.FailedLoginAttempts); // still counts towards the lockout, like any wrong password
    }

    [Fact]
    public async Task Unverified_email_message_tells_how_to_get_the_code()
    {
        var clock = new MutableClock(InitialTime);
        var account = Account.Register(Guid.NewGuid(), "pending@example.com", CorrectPasswordHash, null, clock.UtcNow);
        var handler = CreateHandler(new FakeAccountRepository([account]), new FakeRefreshTokenRepository(), new FakePasswordHasher(),
            new FakeJwtTokenIssuer("token", 900), new FakeRefreshTokenGenerator("raw", "hash"), clock);

        var result = await handler.Handle(new LoginCommand("pending@example.com", CorrectPassword), TestContext.Current.CancellationToken);

        Assert.Equal(LoginErrors.EmailNotVerified, result.Error);
        Assert.Contains("Gửi lại mã", result.Error!.Message);
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

        public Task<Account?> FindByIdForUpdateAsync(Guid id, CancellationToken cancellationToken) =>
            FindByIdAsync(id, cancellationToken);

        public void Add(Account account) => accounts.Add(account);

        public Task<SaveAccountResult> SaveNewAccountAsync(CancellationToken cancellationToken) =>
            Task.FromResult(SaveAccountResult.Success);

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

    private static LoginCommandHandler CreateHandler(
        IAccountRepository accounts,
        IRefreshTokenRepository refreshTokens,
        IPasswordHasher passwordHasher,
        IJwtTokenIssuer jwtTokenIssuer,
        IRefreshTokenGenerator refreshTokenGenerator,
        IClock clock,
        IAuthSessionRepository? authSessions = null) =>
        new(accounts, authSessions ?? new FakeAuthSessionRepository(), refreshTokens, passwordHasher, jwtTokenIssuer, refreshTokenGenerator,
            clock, new MfaLoginGate(new NoMfaRepository()));

    // Các test đăng nhập ở đây đều cho tài khoản chưa bật MFA; luồng MFA được test trên PostgreSQL thật.
    private sealed class NoMfaRepository : IMfaRepository
    {
        public Task<AccountMfa?> FindAsync(Guid accountId, CancellationToken cancellationToken) => Task.FromResult<AccountMfa?>(null);
        public Task<bool> IsEnabledAsync(Guid accountId, CancellationToken cancellationToken) => Task.FromResult(false);
        public void Add(AccountMfa mfa) => throw new NotSupportedException();
        public void Remove(AccountMfa mfa) => throw new NotSupportedException();
        public Task<IReadOnlyList<MfaRecoveryCode>> ListUnusedRecoveryCodesAsync(Guid accountId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public void AddRecoveryCodes(IEnumerable<MfaRecoveryCode> codes) => throw new NotSupportedException();
        public Task DeleteRecoveryCodesAsync(Guid accountId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public void AddChallenge(MfaChallenge challenge) => throw new NotSupportedException();
        public Task<MfaChallenge?> FindChallengeByTokenHashAsync(string tokenHash, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task ReloadAsync(MfaChallenge challenge, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class FakeAuthSessionRepository : IAuthSessionRepository
    {
        public List<AuthSession> Saved { get; } = [];

        public void Add(AuthSession session) => Saved.Add(session);

        public Task<AuthSession?> FindByIdAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult(Saved.FirstOrDefault(s => s.Id == id));

        public Task<AuthSession?> FindByIdForUpdateAsync(Guid id, CancellationToken cancellationToken) =>
            FindByIdAsync(id, cancellationToken);

        public Task RevokeAllForAccountAsync(Guid accountId, DateTimeOffset nowUtc, string reason, CancellationToken cancellationToken)
        {
            foreach (var s in Saved.Where(s => s.AccountId == accountId && !s.IsRevoked))
            {
                s.Revoke(nowUtc, reason);
            }
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<AuthSession>> ListActiveForAccountAsync(Guid accountId, DateTimeOffset nowUtc, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<AuthSession>>(
                Saved.Where(s => s.AccountId == accountId && s.IsActive(nowUtc)).ToList());
    }

    private sealed class FakeRefreshTokenRepository : IRefreshTokenRepository
    {
        public List<RefreshToken> Saved { get; } = [];

        public void Add(RefreshToken refreshToken) => Saved.Add(refreshToken);

        public Task<RefreshToken?> FindByHashAsync(string tokenHash, CancellationToken cancellationToken) =>
            Task.FromResult(Saved.FirstOrDefault(rt => rt.TokenHash == tokenHash));

        public Task<RefreshToken?> FindLatestBySessionIdAsync(Guid sessionId, CancellationToken cancellationToken) =>
            Task.FromResult(Saved.Where(rt => rt.SessionId == sessionId).OrderByDescending(rt => rt.CreatedAtUtc).FirstOrDefault());

        public Task RevokeActiveTokensBySessionIdAsync(Guid sessionId, DateTimeOffset nowUtc, string reason, CancellationToken cancellationToken)
        {
            foreach (var rt in Saved.Where(rt => rt.SessionId == sessionId && !rt.IsRevoked))
            {
                rt.Revoke(nowUtc, reason);
            }
            return Task.CompletedTask;
        }

        public Task RevokeAllForAccountAsync(Guid accountId, DateTimeOffset nowUtc, string reason, CancellationToken cancellationToken)
        {
            foreach (var rt in Saved.Where(rt => rt.AccountId == accountId && !rt.IsRevoked))
            {
                rt.Revoke(nowUtc, reason);
            }
            return Task.CompletedTask;
        }

        public Task ReloadAsync(RefreshToken refreshToken, CancellationToken cancellationToken) =>
            Task.CompletedTask;
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

        public string IssueAccessToken(Account account, Guid sessionId, string authenticationMethods, DateTimeOffset nowUtc) => token;
    }

    private sealed class FakeRefreshTokenGenerator(string rawToken, string tokenHash) : IRefreshTokenGenerator
    {
        public (string RawToken, string TokenHash) GenerateToken() => (rawToken, tokenHash);

        public string HashToken(string rawToken) => tokenHash;
    }
}
