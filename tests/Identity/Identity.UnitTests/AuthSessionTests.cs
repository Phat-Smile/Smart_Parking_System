using ParkingSystem.Identity.Domain.Accounts;
using Xunit;

namespace ParkingSystem.Tests.Identity.UnitTests;

public sealed class AuthSessionTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 7, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_initializes_active_session_with_expected_lifetimes()
    {
        var sessionId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var absoluteExpiresAt = Now.AddDays(AuthSession.DefaultAbsoluteLifetimeDays);

        var session = AuthSession.Create(
            sessionId,
            accountId,
            "Chrome on macOS",
            "192.168.1.100",
            Now,
            absoluteExpiresAt);

        Assert.Equal(sessionId, session.Id);
        Assert.Equal(accountId, session.AccountId);
        Assert.Equal("Chrome on macOS", session.DeviceInfo);
        Assert.Equal("192.168.1.100", session.IpAddress);
        Assert.Equal(Now, session.CreatedAtUtc);
        Assert.Equal(Now, session.LastActivityAtUtc);
        Assert.Equal(absoluteExpiresAt, session.AbsoluteExpiresAtUtc);
        Assert.False(session.IsRevoked);
        Assert.False(session.IsExpired(Now));
        Assert.True(session.IsActive(Now));
    }

    [Fact]
    public void Create_truncates_oversized_device_info_and_ip_to_column_length()
    {
        var session = AuthSession.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new string('u', 1000),
            new string('9', 200),
            Now,
            Now.AddDays(AuthSession.DefaultAbsoluteLifetimeDays));

        Assert.Equal(AuthSession.MaxDeviceInfoLength, session.DeviceInfo!.Length);
        Assert.Equal(AuthSession.MaxIpAddressLength, session.IpAddress!.Length);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_stores_null_for_missing_device_info_and_ip(string? value)
    {
        var session = AuthSession.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            value,
            value,
            Now,
            Now.AddDays(AuthSession.DefaultAbsoluteLifetimeDays));

        Assert.Null(session.DeviceInfo);
        Assert.Null(session.IpAddress);
    }

    [Fact]
    public void RecordActivity_updates_last_activity_time()
    {
        var session = AuthSession.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            null,
            null,
            Now,
            Now.AddDays(30));

        var later = Now.AddHours(2);
        session.RecordActivity(later);

        Assert.Equal(later, session.LastActivityAtUtc);
    }

    [Fact]
    public void Revoke_sets_revoked_at_and_reason()
    {
        var session = AuthSession.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            null,
            null,
            Now,
            Now.AddDays(30));

        session.Revoke(Now.AddMinutes(5), "Logout");

        Assert.True(session.IsRevoked);
        Assert.False(session.IsActive(Now));
        Assert.Equal("Logout", session.RevokedReason);
        Assert.Equal(Now.AddMinutes(5), session.RevokedAtUtc);
    }

    [Fact]
    public void CalculateNextTokenExpiration_caps_token_expiration_at_session_absolute_expiration()
    {
        var absoluteExpires = Now.AddDays(3); // only 3 days left
        var session = AuthSession.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            null,
            null,
            Now.AddDays(-27),
            absoluteExpires);

        // Standard idle is 7 days, but absolute is only 3 days away
        var tokenExpiresAt = session.CalculateNextTokenExpiration(Now, idleDays: 7);

        Assert.Equal(absoluteExpires, tokenExpiresAt);
    }

    [Fact]
    public void CalculateNextTokenExpiration_uses_idle_when_well_within_absolute_expiration()
    {
        var absoluteExpires = Now.AddDays(30);
        var session = AuthSession.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            null,
            null,
            Now,
            absoluteExpires);

        var tokenExpiresAt = session.CalculateNextTokenExpiration(Now, idleDays: 7);

        Assert.Equal(Now.AddDays(7), tokenExpiresAt);
    }

    [Fact]
    public void RefreshToken_Issue_links_to_session_and_validates_state()
    {
        var sessionId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var tokenId = Guid.NewGuid();
        var tokenHash = "dummyhash123";
        var expiresAt = Now.AddDays(7);

        var token = RefreshToken.Issue(tokenId, sessionId, accountId, tokenHash, Now, expiresAt);

        Assert.Equal(tokenId, token.Id);
        Assert.Equal(sessionId, token.SessionId);
        Assert.Equal(accountId, token.AccountId);
        Assert.Equal(tokenHash, token.TokenHash);
        Assert.Equal(Now, token.CreatedAtUtc);
        Assert.Equal(expiresAt, token.ExpiresAtUtc);
        Assert.True(token.IsActive(Now));
        Assert.False(token.IsRevoked);
        Assert.False(token.IsExpired(Now));

        var nextTokenId = Guid.NewGuid();
        token.Revoke(Now.AddMinutes(1), "Rotated", nextTokenId, "newhash456");

        Assert.True(token.IsRevoked);
        Assert.False(token.IsActive(Now));
        Assert.Equal("Rotated", token.RevokedReason);
        Assert.Equal(nextTokenId, token.ReplacedByTokenId);
        Assert.Equal("newhash456", token.ReplacedByTokenHash);
    }
}
