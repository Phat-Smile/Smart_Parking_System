using ParkingSystem.Identity.Application.Profiles;
using Xunit;

namespace ParkingSystem.Tests.Identity.UnitTests.Profiles;

// US-PRIV-002: Unit tests for sensitive identity masking (CCCD and Driver License).
public sealed class IdentityMaskingHelperTests
{
    [Theory]
    [InlineData("079123456789", "********6789")]
    [InlineData("001099012345", "********2345")]
    [InlineData("123456789", "*****6789")] // 9-digit old CMND
    [InlineData("  079123456789  ", "********6789")] // whitespace trimmed
    public void MaskCitizenId_masks_all_digits_except_last_four(string input, string expected)
    {
        var result = IdentityMaskingHelper.MaskCitizenId(input);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void MaskCitizenId_returns_null_when_empty_or_whitespace(string? input)
    {
        var result = IdentityMaskingHelper.MaskCitizenId(input);
        Assert.Null(result);
    }

    [Theory]
    [InlineData("1", "****")]
    [InlineData("12", "****")]
    [InlineData("123", "****")]
    [InlineData("1234", "****")]
    public void MaskCitizenId_masks_completely_when_length_is_four_or_fewer(string input, string expected)
    {
        var result = IdentityMaskingHelper.MaskCitizenId(input);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("B2-123456", "*****3456")]
    [InlineData("012345678901", "********8901")]
    [InlineData("A1-9999", "***9999")]
    public void MaskDriverLicense_masks_all_characters_except_last_four(string input, string expected)
    {
        var result = IdentityMaskingHelper.MaskDriverLicense(input);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void MaskDriverLicense_returns_null_when_empty_or_whitespace(string? input)
    {
        var result = IdentityMaskingHelper.MaskDriverLicense(input);
        Assert.Null(result);
    }
}
