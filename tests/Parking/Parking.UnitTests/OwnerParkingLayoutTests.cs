using ParkingSystem.Parking.Application.ParkingLayout.ManageLayout;
using ParkingSystem.Parking.Domain.ParkingLayout;
using ParkingSystem.Parking.Domain.ParkingSessions;
using Xunit;

namespace ParkingSystem.Tests.Parking.UnitTests;

public sealed class OwnerParkingLayoutTests
{
    private static LayoutSlotInput Slot(string code = "A01") => new(Guid.NewGuid(), code, null, new GridPoint(1, 2));
    private static LayoutZoneInput CarZone(params LayoutSlotInput[] slots) =>
        new(Guid.NewGuid(), "A", "Car", 3, false, slots);
    private static LayoutFloorInput Floor(params LayoutZoneInput[] zones) =>
        new(Guid.NewGuid(), "Ground", zones);

    [Fact]
    public void Valid_car_and_motorbike_layout_is_accepted()
    {
        var floors = new[] { Floor(CarZone(Slot()), new LayoutZoneInput(Guid.NewGuid(), "B", "Motorbike", 30, false, [], new GridBounds(0, 0, 6, 5))) };
        Assert.Null(ParkingLayoutInputValidator.Validate(floors));
    }

    [Fact]
    public void Duplicate_slot_code_is_rejected_case_insensitively_across_zones()
    {
        var floors = new[] { Floor(CarZone(Slot(" a-1 ")), CarZone(Slot("A-1")) with { Code = "B" }) };
        Assert.Equal("parking.layout.invalid", ParkingLayoutInputValidator.Validate(floors)?.Code);
    }

    [Fact]
    public void Duplicate_floor_id_and_missing_zone_are_rejected()
    {
        var floor = Floor(CarZone());
        Assert.NotNull(ParkingLayoutInputValidator.Validate([floor, floor with { Name = "Upper" }]));
        Assert.NotNull(ParkingLayoutInputValidator.Validate([floor with { Zones = null }]));
    }

    [Fact]
    public void Motorbike_zone_cannot_define_individual_slots()
    {
        Assert.NotNull(ParkingLayoutInputValidator.Validate([
            Floor(new LayoutZoneInput(Guid.NewGuid(), "M", "Motorbike", 20, false, [Slot()], new GridBounds(0, 0, 5, 5)))
        ]));
    }

    [Fact]
    public void Car_slots_require_unique_grid_positions_and_motorbike_regions_require_shapes()
    {
        Assert.NotNull(ParkingLayoutInputValidator.Validate([Floor(CarZone(Slot() with { Position = null }))]));
        Assert.NotNull(ParkingLayoutInputValidator.Validate([Floor(CarZone(Slot("A01"), Slot("A02")))]));
        Assert.NotNull(ParkingLayoutInputValidator.Validate([Floor(new LayoutZoneInput(Guid.NewGuid(), "M", "Motorbike", 20, false, []))]));
        Assert.Null(ParkingLayoutInputValidator.Validate([Floor(new LayoutZoneInput(Guid.NewGuid(), "M", "Motorbike", 20, false, [],
            Polygon: [new GridPoint(0, 0), new GridPoint(4, 0), new GridPoint(4, 4)]))]));
    }

    [Fact]
    public void Slot_overrides_only_accept_documented_values()
    {
        Assert.Null(ParkingLayoutInputValidator.Validate([Floor(CarZone(Slot() with { OverrideState = "maintenance" }))]));
        Assert.NotNull(ParkingLayoutInputValidator.Validate([Floor(CarZone(Slot() with { OverrideState = "Available" }))]));
    }

    [Fact]
    public void Slot_reconfiguration_preserves_identity_and_recalculates_status()
    {
        var slot = ParkingSlot.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "a1");
        Assert.True(slot.Reconfigure(" a2 ", OverrideState.Unavailable));
        Assert.Equal("A2", slot.SlotCode);
        Assert.Equal(SlotEffectiveStatus.Unavailable, slot.EffectiveStatus);
        Assert.Equal(1, slot.RowVersion);
        Assert.False(slot.Reconfigure("A2", OverrideState.Unavailable));
        Assert.Equal(1, slot.RowVersion);
    }

    [Fact]
    public void Floor_and_zone_can_be_reconfigured_without_changing_ids()
    {
        var floor = ParkingSystem.Parking.Domain.ParkingLayout.Floor.Create(Guid.NewGuid(), Guid.NewGuid(), "Old");
        var zone = Zone.Create(Guid.NewGuid(), floor.Id, "A", VehicleType.Car, 2, false);
        Assert.True(floor.Rename(" New "));
        Assert.True(zone.Reconfigure(" B ", 4, true));
        Assert.Equal("New", floor.Name);
        Assert.Equal("B", zone.Code);
        Assert.Equal(4, zone.Capacity);
        Assert.True(zone.HasSlotSensor);
    }
}
