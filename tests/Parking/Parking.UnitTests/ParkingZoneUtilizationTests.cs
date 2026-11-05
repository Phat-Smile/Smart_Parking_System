using ParkingSystem.Parking.Application.ParkingLayout.ManageLayout;
using Xunit;

namespace ParkingSystem.Tests.Parking.UnitTests;

public sealed class ParkingZoneUtilizationTests
{
    [Fact]
    public void Car_zone_counts_only_real_slots_and_excludes_blocked_slots_from_usable_capacity()
    {
        var layout = Layout(new LayoutZone(Guid.NewGuid(), "A", "Car", 20, 0, true, [
            Slot("Available"), Slot("Occupied"), Slot("Assigned"),
            Slot("NeedsVerification"), Slot("Maintenance"), Slot("Unavailable")
        ], null, null));

        var usage = ParkingZoneUtilizationCalculator.Calculate(layout).Zones.Single();

        Assert.Equal(20, usage.DeclaredCapacity);
        Assert.Equal(4, usage.UsableCapacity);
        Assert.Equal(3, usage.InUse);
        Assert.Equal(1, usage.Available);
        Assert.Equal(75, usage.UtilizationPercent);
        Assert.Equal(1, usage.Maintenance);
        Assert.Equal(1, usage.Unavailable);
    }

    [Fact]
    public void Motorbike_zone_uses_zone_count_instead_of_individual_slots()
    {
        var layout = Layout(new LayoutZone(Guid.NewGuid(), "M", "Motorbike", 8, 3, false,
            [], new GridBounds(0, 0, 4, 5), null));

        var usage = ParkingZoneUtilizationCalculator.Calculate(layout).Zones.Single();

        Assert.Equal(3, usage.InUse);
        Assert.Equal(5, usage.Available);
        Assert.Equal(37.5, usage.UtilizationPercent);
        Assert.Equal(8, usage.UsableCapacity);
    }

    [Fact]
    public void Empty_or_unusable_zones_have_zero_utilization_without_division_by_zero()
    {
        var layout = Layout(new LayoutZone(Guid.NewGuid(), "A", "Car", 1, 0, false,
            [Slot("Unavailable")], null, null));

        var usage = ParkingZoneUtilizationCalculator.Calculate(layout).Zones.Single();
        Assert.Equal(0, usage.UsableCapacity);
        Assert.Equal(0, usage.InUse);
        Assert.Equal(0, usage.UtilizationPercent);
    }

    private static LayoutSlot Slot(string status) =>
        new(Guid.NewGuid(), Guid.NewGuid().ToString("N"), null, status, new GridPoint(0, 0));

    private static ParkingLotLayout Layout(LayoutZone zone) =>
        new(Guid.NewGuid(), 4, [new LayoutFloor(Guid.NewGuid(), "Ground", [zone])]);
}
