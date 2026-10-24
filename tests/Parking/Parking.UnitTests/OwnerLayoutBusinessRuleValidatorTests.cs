using ParkingSystem.Parking.Application.ParkingLayout.ManageLayout;
using Xunit;

namespace ParkingSystem.Tests.Parking.UnitTests;

public sealed class OwnerLayoutBusinessRuleValidatorTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ZoneCapacityMustBePositive(int capacity)
    {
        var floors = new[] { Floor("Ground", CarZone("A", capacity)) };

        Assert.NotNull(ParkingLayoutInputValidator.Validate(floors));
    }

    [Fact]
    public void ZoneCodeMustBeUniqueAcrossDifferentFloorsIgnoringCaseAndWhitespace()
    {
        var floors = new[]
        {
            Floor("Ground", CarZone("Zone-A")),
            Floor("First", CarZone(" zone-a "))
        };

        Assert.NotNull(ParkingLayoutInputValidator.Validate(floors));
    }

    [Fact]
    public void MotorbikeZoneCannotHaveSlotSensor()
    {
        var floors = new[] { Floor("Ground", MotorbikeZone("M1", hasSensor: true)) };

        Assert.NotNull(ParkingLayoutInputValidator.Validate(floors));
    }

    [Fact]
    public void PositiveCapacityUniqueCodesAndSensorlessMotorbikeZoneAreValid()
    {
        var floors = new[]
        {
            Floor("Ground", CarZone("C1")),
            Floor("First", MotorbikeZone("M1", hasSensor: false))
        };

        Assert.Null(ParkingLayoutInputValidator.Validate(floors));
    }

    private static LayoutFloorInput Floor(string name, params LayoutZoneInput[] zones) =>
        new(Guid.NewGuid(), name, zones);

    private static LayoutZoneInput CarZone(string code, int capacity = 1) =>
        new(Guid.NewGuid(), code, "Car", capacity, false, []);

    private static LayoutZoneInput MotorbikeZone(string code, bool hasSensor) =>
        new(Guid.NewGuid(), code, "Motorbike", 1, hasSensor, [],
            Bounds: new GridBounds(0, 0, 10, 10));
}
