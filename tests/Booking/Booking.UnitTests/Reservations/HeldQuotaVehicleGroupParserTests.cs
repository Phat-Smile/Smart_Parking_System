using ParkingSystem.Booking.Domain.Reservations;
using Xunit;

namespace ParkingSystem.Tests.Booking.UnitTests.Reservations;

public sealed class HeldQuotaVehicleGroupParserTests
{
    [Theory]
    [InlineData(null, "Car")]
    [InlineData("", "Car")]
    [InlineData("   ", "Car")]
    [InlineData("Car", "Car")]
    [InlineData("car", "Car")]
    [InlineData("CAR", "Car")]
    [InlineData("cAr", "Car")]
    [InlineData("Motorbike", "Motorbike")]
    [InlineData("motorbike", "Motorbike")]
    [InlineData("MOTORBIKE", "Motorbike")]
    [InlineData("mOtOrBiKe", "Motorbike")]
    public void TryParse_ValidInputs_ResolvesSuccessfully(string? input, string expectedGroup)
    {
        var success = HeldQuotaVehicleGroupParser.TryParse(input, out var resolvedGroup);

        Assert.True(success);
        Assert.Equal(expectedGroup, resolvedGroup);
    }

    [Theory]
    [InlineData("Truck")]
    [InlineData("truck")]
    [InlineData("Van")]
    [InlineData("Bicycle")]
    [InlineData("Bus")]
    [InlineData("123")]
    [InlineData("Car ")]
    [InlineData(" Motorbike ")]
    public void TryParse_InvalidInputs_ReturnsFalseAndEmpty(string? input)
    {
        var success = HeldQuotaVehicleGroupParser.TryParse(input, out var resolvedGroup);

        Assert.False(success);
        Assert.Equal(string.Empty, resolvedGroup);
    }
}

