using System.Text;
using ParkingSystem.Identity.Application.Accounts.Mfa;
using ParkingSystem.Identity.Infrastructure.Security;
using Xunit;

namespace ParkingSystem.Tests.Identity.UnitTests;

public sealed class MfaTests
{
    // RFC 6238 phụ lục B (SHA-1, secret ASCII "12345678901234567890"); RFC in mã 8 số, mã 6 số là 6 chữ số cuối.
    private static readonly byte[] RfcKey = Encoding.ASCII.GetBytes("12345678901234567890");
    private readonly TotpService _totp = new();

    [Theory]
    [InlineData(59L, "287082")]
    [InlineData(1111111109L, "081804")]
    [InlineData(1111111111L, "050471")]
    [InlineData(1234567890L, "005924")]
    [InlineData(2000000000L, "279037")]
    [InlineData(20000000000L, "353130")]
    public void Codes_match_the_rfc_6238_test_vectors(long unixSeconds, string expected) =>
        Assert.Equal(expected, TotpService.ComputeCode(RfcKey, unixSeconds / TotpService.StepSeconds));

    [Fact]
    public void Base32_round_trips_and_matches_the_known_encoding()
    {
        Assert.Equal("GEZDGNBVGY3TQOJQGEZDGNBVGY3TQOJQ", TotpService.Base32Encode(RfcKey));
        Assert.Equal(RfcKey, TotpService.Base32Decode("gezd gnbv gy3t qojq gezd gnbv gy3t qojq"));
        var secret = _totp.GenerateSecret();
        Assert.Equal(32, secret.Length);
        Assert.Equal(20, TotpService.Base32Decode(secret).Length);
    }

    [Fact]
    public void A_code_is_accepted_one_step_either_side_but_not_further()
    {
        var secret = TotpService.Base32Encode(RfcKey);
        var now = DateTimeOffset.FromUnixTimeSeconds(1234567890);
        var step = now.ToUnixTimeSeconds() / TotpService.StepSeconds;

        Assert.Equal(step, _totp.FindMatchingStep(secret, TotpService.ComputeCode(RfcKey, step), now));
        Assert.Equal(step - 1, _totp.FindMatchingStep(secret, TotpService.ComputeCode(RfcKey, step - 1), now));
        Assert.Equal(step + 1, _totp.FindMatchingStep(secret, TotpService.ComputeCode(RfcKey, step + 1), now));
        Assert.Null(_totp.FindMatchingStep(secret, TotpService.ComputeCode(RfcKey, step + 2), now));
        Assert.Null(_totp.FindMatchingStep(secret, "12345", now));
        Assert.Null(_totp.FindMatchingStep(secret, "١٢٣٤٥٦", now)); // chữ số Unicode không phải ASCII
    }

    [Fact]
    public void Provisioning_uri_carries_the_issuer_account_and_secret()
    {
        var uri = _totp.BuildProvisioningUri("an@example.com", "JBSWY3DPEHPK3PXP");

        Assert.StartsWith("otpauth://totp/SmartParking%3Aan%40example.com?", uri);
        Assert.Contains("secret=JBSWY3DPEHPK3PXP", uri);
        Assert.Contains("issuer=SmartParking", uri);
        Assert.Contains("digits=6", uri);
        Assert.Contains("period=30", uri);
    }

    [Fact]
    public void Recovery_codes_avoid_confusable_letters_and_hash_the_same_however_they_are_typed()
    {
        var code = MfaSecrets.NewRecoveryCode();

        Assert.Matches("^[A-HJKMNP-Z2-9]{4}-[A-HJKMNP-Z2-9]{4}$", code);
        Assert.Equal(MfaSecrets.HashRecoveryCode(code), MfaSecrets.HashRecoveryCode(" " + code.Replace("-", "").ToLowerInvariant() + " "));
    }

    [Theory]
    [InlineData("token", "123456", null, true)]
    [InlineData("token", null, "ABCD-EF23", true)]
    [InlineData("token", null, "abcdef23", true)]
    [InlineData("token", "123456", "ABCD-EF23", false)] // chỉ một trong hai
    [InlineData("token", null, null, false)]
    [InlineData("", "123456", null, false)]
    [InlineData("token", "12345a", null, false)]
    public void Second_step_validator_needs_a_token_and_exactly_one_kind_of_code(string token, string? code, string? recovery, bool expectedValid) =>
        Assert.Equal(expectedValid, new CompleteMfaLoginCommandValidator().Validate(new CompleteMfaLoginCommand(token, code, recovery)).IsValid);
}
