using ParkingSystem.Payment.Domain.Constants;
using Xunit;

namespace ParkingSystem.Tests.Payment.UnitTests.Domain;

public sealed class AuditDetailsSecurityTests
{
    [Theory]
    [InlineData("password")]
    [InlineData("NewPassword")]
    [InlineData("old_password")]
    [InlineData("accessToken")]
    [InlineData("refreshToken")]
    [InlineData("SECRET_KEY")]
    [InlineData("otpCode")]
    [InlineData("encryption_key")]
    [InlineData("passwordHash")]
    [InlineData("userCredentials")]
    [InlineData("authorizationHeader")]
    public void ContainsForbiddenKey_WithSensitiveKey_ReturnsTrue(string key)
    {
        Assert.True(AuditDetailsSecurity.ContainsForbiddenKey(key));
    }

    [Theory]
    [InlineData("amount")]
    [InlineData("plateNumberMasked")]
    [InlineData("fromVersion")]
    [InlineData("toVersion")]
    [InlineData("reversalOfEntryId")]
    [InlineData("slotCode")]
    [InlineData("sessionId")]
    public void ContainsForbiddenKey_WithSafeKey_ReturnsFalse(string key)
    {
        Assert.False(AuditDetailsSecurity.ContainsForbiddenKey(key));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void ContainsForbiddenKey_WithNullOrWhitespace_ReturnsTrue(string? key)
    {
        Assert.True(AuditDetailsSecurity.ContainsForbiddenKey(key!));
    }
}
