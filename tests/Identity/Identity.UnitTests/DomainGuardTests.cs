using ParkingSystem.Identity.Domain.Accounts;
using ParkingSystem.Identity.Domain.EmailVerifications;
using ParkingSystem.Identity.Domain.Profiles;
using Xunit;

namespace ParkingSystem.Tests.Identity.UnitTests;

public sealed class AccountDomainGuardTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 15, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Register_throws_when_id_is_empty()
    {
        Assert.Throws<ArgumentException>(() =>
            Account.Register(Guid.Empty, "user@test.com", "hash", null, Now));
    }

    [Fact]
    public void Register_throws_when_email_is_whitespace()
    {
        Assert.Throws<ArgumentException>(() =>
            Account.Register(Guid.NewGuid(), "  ", "hash", null, Now));
    }

    [Fact]
    public void Register_throws_when_passwordHash_is_whitespace()
    {
        Assert.Throws<ArgumentException>(() =>
            Account.Register(Guid.NewGuid(), "user@test.com", "  ", null, Now));
    }

    [Fact]
    public void RegisterWithGoogle_throws_when_id_is_empty()
    {
        Assert.Throws<ArgumentException>(() =>
            Account.RegisterWithGoogle(Guid.Empty, "user@test.com", "google-sub", Now));
    }

    [Fact]
    public void RegisterWithGoogle_throws_when_googleSubject_is_whitespace()
    {
        Assert.Throws<ArgumentException>(() =>
            Account.RegisterWithGoogle(Guid.NewGuid(), "user@test.com", "  ", Now));
    }

    [Fact]
    public void LinkGoogle_throws_when_linked_to_different_subject()
    {
        var account = Account.RegisterWithGoogle(Guid.NewGuid(), "user@test.com", "sub-original", Now);
        Assert.Throws<InvalidOperationException>(() => account.LinkGoogle("sub-different"));
    }

    [Fact]
    public void LinkGoogle_is_idempotent_for_same_subject()
    {
        var account = Account.RegisterWithGoogle(Guid.NewGuid(), "user@test.com", "sub-same", Now);
        account.LinkGoogle("sub-same");
        Assert.Equal("sub-same", account.GoogleSubject);
    }

    [Fact]
    public void Disable_sets_status_to_disabled()
    {
        var account = Account.Register(Guid.NewGuid(), "user@test.com", "hash", null, Now);
        account.VerifyEmail();
        account.Disable();
        Assert.Equal(AccountStatus.Disabled, account.Status);
    }

    [Fact]
    public void RecordCorrectCredential_is_noop_when_no_failed_attempts()
    {
        var account = Account.Register(Guid.NewGuid(), "user@test.com", "hash", null, Now);
        account.VerifyEmail();
        var versionBefore = account.RowVersion;

        account.RecordCorrectCredential();

        Assert.Equal(versionBefore, account.RowVersion);
        Assert.Equal(0, account.FailedLoginAttempts);
    }

    [Fact]
    public void RecordCorrectCredential_clears_failed_attempts_and_lockout()
    {
        var account = Account.Register(Guid.NewGuid(), "user@test.com", "hash", null, Now);
        account.VerifyEmail();
        account.RecordFailedLogin(Now);
        account.RecordFailedLogin(Now);
        var versionBefore = account.RowVersion;

        account.RecordCorrectCredential();

        Assert.Equal(0, account.FailedLoginAttempts);
        Assert.True(account.RowVersion > versionBefore);
    }
}

public sealed class AuthSessionDomainGuardTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 15, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_throws_when_id_is_empty()
    {
        Assert.Throws<ArgumentException>(() =>
            AuthSession.Create(Guid.Empty, Guid.NewGuid(), "Chrome", "1.2.3.4", Now, Now.AddDays(30)));
    }

    [Fact]
    public void Create_throws_when_accountId_is_empty()
    {
        Assert.Throws<ArgumentException>(() =>
            AuthSession.Create(Guid.NewGuid(), Guid.Empty, "Chrome", "1.2.3.4", Now, Now.AddDays(30)));
    }

    [Fact]
    public void Create_throws_when_expiresAt_is_not_greater_than_createdAt()
    {
        Assert.Throws<ArgumentException>(() =>
            AuthSession.Create(Guid.NewGuid(), Guid.NewGuid(), "Chrome", "1.2.3.4", Now, Now));
    }

    [Fact]
    public void Revoke_is_idempotent()
    {
        var session = AuthSession.Create(Guid.NewGuid(), Guid.NewGuid(), null, null, Now, Now.AddDays(30));
        session.Revoke(Now, "First");
        var revokedAt = session.RevokedAtUtc;

        session.Revoke(Now.AddMinutes(1), "Second");

        Assert.Equal(revokedAt, session.RevokedAtUtc);
        Assert.Equal("First", session.RevokedReason);
    }
}

public sealed class RefreshTokenDomainGuardTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 15, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Issue_throws_when_id_is_empty()
    {
        Assert.Throws<ArgumentException>(() =>
            RefreshToken.Issue(Guid.Empty, Guid.NewGuid(), Guid.NewGuid(), "hash", Now, Now.AddDays(7)));
    }

    [Fact]
    public void Issue_throws_when_sessionId_is_empty()
    {
        Assert.Throws<ArgumentException>(() =>
            RefreshToken.Issue(Guid.NewGuid(), Guid.Empty, Guid.NewGuid(), "hash", Now, Now.AddDays(7)));
    }

    [Fact]
    public void Issue_throws_when_accountId_is_empty()
    {
        Assert.Throws<ArgumentException>(() =>
            RefreshToken.Issue(Guid.NewGuid(), Guid.NewGuid(), Guid.Empty, "hash", Now, Now.AddDays(7)));
    }

    [Fact]
    public void Issue_throws_when_tokenHash_is_whitespace()
    {
        Assert.Throws<ArgumentException>(() =>
            RefreshToken.Issue(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "  ", Now, Now.AddDays(7)));
    }

    [Fact]
    public void Issue_throws_when_expiresAt_is_not_greater_than_createdAt()
    {
        Assert.Throws<ArgumentException>(() =>
            RefreshToken.Issue(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "hash", Now, Now));
    }

    [Fact]
    public void Revoke_is_idempotent()
    {
        var token = RefreshToken.Issue(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "hash", Now, Now.AddDays(7));
        token.Revoke(Now, "First");
        var revokedAt = token.RevokedAtUtc;

        token.Revoke(Now.AddMinutes(1), "Second");

        Assert.Equal(revokedAt, token.RevokedAtUtc);
        Assert.Equal("First", token.RevokedReason);
    }

    [Fact]
    public void Revoke_defaults_to_Revoked_when_reason_is_whitespace()
    {
        var token = RefreshToken.Issue(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "hash", Now, Now.AddDays(7));
        token.Revoke(Now, "   ");
        Assert.Equal("Revoked", token.RevokedReason);
    }
}

public sealed class EmailVerificationDomainGuardTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 15, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Issue_throws_when_id_is_empty()
    {
        Assert.Throws<ArgumentException>(() =>
            EmailVerification.Issue(Guid.Empty, Guid.NewGuid(), "user@test.com", "hash", Now));
    }

    [Fact]
    public void Issue_throws_when_accountId_is_empty()
    {
        Assert.Throws<ArgumentException>(() =>
            EmailVerification.Issue(Guid.NewGuid(), Guid.Empty, "user@test.com", "hash", Now));
    }

    [Fact]
    public void Issue_throws_when_email_is_whitespace()
    {
        Assert.Throws<ArgumentException>(() =>
            EmailVerification.Issue(Guid.NewGuid(), Guid.NewGuid(), "  ", "hash", Now));
    }

    [Fact]
    public void Issue_throws_when_otpHash_is_whitespace()
    {
        Assert.Throws<ArgumentException>(() =>
            EmailVerification.Issue(Guid.NewGuid(), Guid.NewGuid(), "user@test.com", "  ", Now));
    }

    [Fact]
    public void RecordAttempt_returns_false_when_otp_is_whitespace()
    {
        var accountId = Guid.NewGuid();
        var ev = EmailVerification.Issue(Guid.NewGuid(), accountId, "user@test.com", "correct-hash", Now);
        var hasher = new FakeOtpHasher("correct");

        var result = ev.RecordAttempt("   ", hasher, Now);

        Assert.False(result);
    }

    [Fact]
    public void Invalidate_is_idempotent()
    {
        var ev = EmailVerification.Issue(Guid.NewGuid(), Guid.NewGuid(), "user@test.com", "hash", Now);
        ev.Invalidate(Now);
        var invalidatedAt = ev.InvalidatedAtUtc;

        ev.Invalidate(Now.AddMinutes(1));

        Assert.Equal(invalidatedAt, ev.InvalidatedAtUtc);
    }

    private sealed class FakeOtpHasher(string correctOtp) : IOtpHasher
    {
        public string HashOtp(string otp, Guid accountId) => otp == correctOtp ? "correct-hash" : "wrong-hash";
        public bool VerifyOtp(string otp, Guid accountId, string hash) => otp == correctOtp && hash == "correct-hash";
    }
}

public sealed class ProfileDomainGuardTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 15, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_throws_when_accountId_is_empty()
    {
        Assert.Throws<ArgumentException>(() =>
            Profile.Create(Guid.Empty, "Nguyễn Văn A", Now));
    }

    [Fact]
    public void Create_throws_when_fullName_is_whitespace()
    {
        Assert.Throws<ArgumentException>(() =>
            Profile.Create(Guid.NewGuid(), "  ", Now));
    }

    [Fact]
    public void Create_throws_when_fullName_exceeds_100_characters()
    {
        var longName = new string('A', 101);
        Assert.Throws<ArgumentException>(() =>
            Profile.Create(Guid.NewGuid(), longName, Now));
    }

    [Fact]
    public void Create_with_exactly_100_characters_succeeds()
    {
        var name = new string('A', 100);
        var profile = Profile.Create(Guid.NewGuid(), name, Now);
        Assert.Equal(name, profile.FullName);
    }

    [Fact]
    public void Create_stores_optional_fields()
    {
        var accountId = Guid.NewGuid();
        var dob = new DateOnly(1990, 5, 15);
        var profile = Profile.Create(accountId, "Lộc", Now,
            dateOfBirth: dob,
            citizenIdNumber: "  012345678  ",
            driverLicenseNumber: "  DL123  ",
            avatarUrl: "  https://avatar.test/img  ");

        Assert.Equal(dob, profile.DateOfBirth);
        Assert.Equal("012345678", profile.CitizenIdNumber);
        Assert.Equal("DL123", profile.DriverLicenseNumber);
        Assert.Equal("https://avatar.test/img", profile.AvatarUrl);
    }
}
