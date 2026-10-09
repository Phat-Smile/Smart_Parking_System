using ParkingSystem.Identity.Application.Vehicles.Lookup;
using Xunit;

namespace ParkingSystem.Tests.Identity.UnitTests.Vehicles;

public sealed class LicensePlateNormalizerTests
{
    private readonly LicensePlateNormalizer _normalizer = new();

    [Theory]
    [InlineData(null, "")]
    [InlineData("", "")]
    [InlineData("   ", "")]
    [InlineData("51a-123.45", "51A12345")]
    [InlineData("51-A 123.45", "51A12345")]
    [InlineData("59-X1 123.45", "59X112345")]
    [InlineData("29-a 999.99", "29A99999")]
    [InlineData(" 43B1-9999 ", "43B19999")]
    [InlineData("\t51A\u00A0123.45\n", "51A12345")]
    public void Normalize_removes_delimiters_and_uppercases(string? raw, string expected)
    {
        var result = _normalizer.Normalize(raw);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("51A12345", true)]
    [InlineData("59X112345", true)]
    [InlineData("29A1234", true)]
    [InlineData("30F12345", true)]
    [InlineData("43B11234", true)]
    [InlineData("72A99999", true)]
    [InlineData("51A123456", true)]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData("51-A12345", false)]
    [InlineData("ABC12345", false)]
    [InlineData("5112345", false)]
    [InlineData("51A123", false)]
    [InlineData("51A1234567", false)]
    [InlineData("51AAAA123", false)]
    // Heuristic only, not a legal plate model: two-letter series match, plates that start with letters do not.
    [InlineData("80NG12345", true)]
    [InlineData("80LD12345", true)]
    [InlineData("TM12345", false)]
    [InlineData("INVALID", false)]
    public void IsValidFormat_validates_standard_vietnamese_plate_syntax(string? plate, bool expectedValid)
    {
        var result = _normalizer.IsValidFormat(plate!);
        Assert.Equal(expectedValid, result);
    }
}
