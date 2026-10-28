using ParkingSystem.Payment.Domain.Constants;
using Xunit;

namespace ParkingSystem.Tests.Payment.UnitTests.Domain;

public sealed class AuditActionCatalogTests
{
    [Theory]
    [InlineData("CashPaymentConfirmed")]
    [InlineData("WalletEntryReversed")]
    [InlineData("RefundRequestCreated")]
    [InlineData("RefundRequestApproved")]
    [InlineData("RefundRequestRejected")]
    [InlineData("PricingPolicyChanged")]
    [InlineData("SlotStatusOverridden")]
    [InlineData("ManualParkingConfirmed")]
    [InlineData("ManualExitVerified")]
    [InlineData("SessionQrReissued")]
    [InlineData("LoginFailed")]
    [InlineData("AccountLocked")]
    [InlineData("PasswordChanged")]
    [InlineData("PasswordReset")]
    [InlineData("RefreshTokenReuseDetected")]
    public void IsValid_WithKnownAction_ReturnsTrue(string actionType)
    {
        Assert.True(AuditActionCatalog.IsValid(actionType));
    }

    [Theory]
    [InlineData("UnknownAction")]
    [InlineData("cashpaymentconfirmed")] // Case sensitive check
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void IsValid_WithInvalidAction_ReturnsFalse(string? actionType)
    {
        Assert.False(AuditActionCatalog.IsValid(actionType));
    }
}
