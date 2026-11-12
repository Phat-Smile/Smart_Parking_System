using ParkingSystem.Identity.Application.Vehicles;
using ParkingSystem.Identity.Domain.Vehicles;
using Xunit;

namespace ParkingSystem.Tests.Identity.UnitTests.Vehicles;

public sealed class VietnamesePlateFormatTests
{
    [Theory]
    [InlineData("30F25658", VehicleDomainType.Car, true)]
    [InlineData("51LD12345", VehicleDomainType.Car, true)]
    [InlineData("30F2568", VehicleDomainType.Car, true)]
    [InlineData("59A112345", VehicleDomainType.Motorbike, true)]
    [InlineData("59A11234", VehicleDomainType.Motorbike, true)]
    [InlineData("59A112345", VehicleDomainType.Car, false)]
    [InlineData("51LD12345", VehicleDomainType.Motorbike, false)]
    [InlineData("00A12345", VehicleDomainType.Car, false)]
    [InlineData("INVALID", VehicleDomainType.Car, false)]
    [InlineData("", VehicleDomainType.Bicycle, true)]
    [InlineData("59A112345", VehicleDomainType.Bicycle, false)]
    public void IsValid_checks_supported_civilian_syntax(string plate, VehicleDomainType type, bool expected)
    {
        Assert.Equal(expected, VietnamesePlateFormat.IsValid(plate, type));
    }

    [Fact]
    public void IsValid_rejects_missing_required_plate()
    {
        Assert.False(VietnamesePlateFormat.IsValid(null, VehicleDomainType.Car));
        Assert.False(VietnamesePlateFormat.IsValid("", VehicleDomainType.Motorbike));
    }
}
