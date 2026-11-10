using ParkingSystem.Parking.Application.Common.Plate;
using Xunit;

namespace ParkingSystem.Parking.UnitTests;

public sealed class VietnamesePlateNormalizerTests
{
    [Theory]
    [InlineData("51F - 782.91", "51F78291")]
    [InlineData("59-X1 234.56", "59X123456")]
    [InlineData("29a-1234", "29A1234")]
    [InlineData("  43-d1-999.99  ", "43D199999")]
    [InlineData("", "")]
    [InlineData(null, "")]
    public void Normalize_CleansSpecialChars_AndConvertsToUpper(string? input, string expected)
    {
        var result = VietnamesePlateNormalizer.Normalize(input);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("51F78291", true)]
    [InlineData("59X123456", true)]
    [InlineData("29A1234", true)]
    [InlineData("51LD12345", true)]
    [InlineData("ABC", false)]
    [InlineData("12345", false)]
    [InlineData("", false)]
    public void IsValidFormat_ValidatesVietnamesePlateSyntax(string input, bool expected)
    {
        var result = VietnamesePlateNormalizer.IsValidFormat(input);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("51F78291", "Car")]
    [InlineData("30H12345", "Car")]
    [InlineData("59X123456", "Motorbike")]
    [InlineData("29B299999", "Motorbike")]
    [InlineData("43D199999", "Motorbike")]
    public void DetectVehicleType_DistinguishesCarAndMotorbike(string normalizedPlate, string expectedType)
    {
        var result = VietnamesePlateNormalizer.DetectVehicleType(normalizedPlate);
        Assert.Equal(expectedType, result);
    }

    [Theory]
    [InlineData("51F78291", "Car", "51F-782.91")]
    [InlineData("59X123456", "Motorbike", "59X1-234.56")]
    [InlineData("29A1234", "Car", "29A-1234")]
    public void FormatDisplay_FormatsCorrectlyForDisplay(string input, string vehicleType, string expected)
    {
        var result = VietnamesePlateNormalizer.FormatDisplay(input, vehicleType);
        Assert.Equal(expected, result);
    }
}
