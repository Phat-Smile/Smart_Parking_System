using ParkingSystem.Booking.Domain.Reservations;
using Xunit;

namespace ParkingSystem.Tests.Booking.UnitTests.Reservations;

public sealed class VietnameseLicensePlateValidatorTests
{
    [Theory]
    [InlineData("51A12345", "51A12345")]
    [InlineData("51A-123.45", "51A12345")]
    [InlineData(" 51A-123.45 ", "51A12345")]
    [InlineData("51-A 123.45", "51A12345")]
    [InlineData("51a12345", "51A12345")]
    [InlineData("51a-123.45", "51A12345")]
    [InlineData("29A-999.99", "29A99999")]
    [InlineData("43C-1234", "43C1234")]
    [InlineData("51LD-1234", "51LD1234")]
    [InlineData("29NN-123.45", "29NN12345")]
    [InlineData("29A1-123.45", "29A112345")]
    public void Valid_license_plates_normalize_and_validate_successfully(string rawPlate, string expectedCanonical)
    {
        var isValid = VietnameseLicensePlateValidator.IsValid(rawPlate);
        var tryResult = VietnameseLicensePlateValidator.TryNormalizeAndValidate(rawPlate, out var canonical);

        Assert.True(isValid);
        Assert.True(tryResult);
        Assert.Equal(expectedCanonical, canonical);
        Assert.True(VietnameseLicensePlateValidator.IsNormalizedValid(canonical));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\r\n")]
    public void Null_or_whitespace_plates_are_rejected(string? plate)
    {
        Assert.False(VietnameseLicensePlateValidator.IsValid(plate));
        Assert.False(VietnameseLicensePlateValidator.IsNormalizedValid(plate));
        Assert.False(VietnameseLicensePlateValidator.TryNormalizeAndValidate(plate, out var canonical));
        Assert.Equal(string.Empty, canonical);
    }

    [Theory]
    [InlineData("ABCDEFGH")]          // Only letters
    [InlineData("12345678")]          // Only numbers
    [InlineData("A51-123.45")]        // Province code starts with letter
    [InlineData("1A-123.45")]         // 1-digit province code
    [InlineData("511-123.45")]        // 3-digit province code without series letter
    [InlineData("51A-1")]             // Too short
    [InlineData("51A-12")]            // Too short
    [InlineData("51A-123")]           // Too short (needs at least 4 digits)
    [InlineData("51A-1234567")]       // Too long (7 digits)
    [InlineData("51A@12345")]         // Special character '@'
    [InlineData("51A#12345")]         // Special character '#'
    [InlineData("51A$12345")]         // Special character '$'
    [InlineData("51-XYZ-12345")]      // 3 series letters
    [InlineData("UNKNOWN_PLATE")]     // Arbitrary string
    public void Malformed_or_unsupported_plates_are_rejected(string rawPlate)
    {
        Assert.False(VietnameseLicensePlateValidator.IsValid(rawPlate));
        Assert.False(VietnameseLicensePlateValidator.TryNormalizeAndValidate(rawPlate, out var canonical));
        Assert.Equal(string.Empty, canonical);
    }

    [Theory]
    [InlineData("51A12345")]
    [InlineData("51G12345")]
    [InlineData("51J12345")]
    [InlineData("51K12345")]
    [InlineData("51B98765")]
    [InlineData("51C45678")]
    [InlineData("51F88888")]
    public void Regression_All_plates_from_US_RES_010_pass_syntax_validation(string canonicalPlate)
    {
        Assert.True(VietnameseLicensePlateValidator.IsNormalizedValid(canonicalPlate));
        Assert.True(VietnameseLicensePlateValidator.IsValid(canonicalPlate));
        Assert.True(VietnameseLicensePlateValidator.TryNormalizeAndValidate(canonicalPlate, out var normalized));
        Assert.Equal(canonicalPlate, normalized);
    }

    [Fact]
    public void Equivalence_with_LicensePlateNormalizer_is_preserved()
    {
        const string raw1 = " 51A-123.45 ";
        const string raw2 = "51a12345";

        Assert.True(LicensePlateNormalizer.AreEquivalent(raw1, raw2));
        Assert.True(VietnameseLicensePlateValidator.TryNormalizeAndValidate(raw1, out var can1));
        Assert.True(VietnameseLicensePlateValidator.TryNormalizeAndValidate(raw2, out var can2));
        Assert.Equal(can1, can2);
    }

    [Fact]
    public void Syntax_validator_does_not_infer_vehicle_type()
    {
        // Both car and motorbike series share structural syntax (e.g., 29A1-123.45 vs 51A-123.45).
        // The validator confirms structural validity only; vehicle type classification is strictly Identity's responsibility.
        Assert.True(VietnameseLicensePlateValidator.IsValid("29A1-123.45"));
        Assert.True(VietnameseLicensePlateValidator.IsValid("51A-123.45"));
    }
}
