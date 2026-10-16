using ParkingSystem.Parking.Application.ParkingLayout;
using ParkingSystem.Parking.Application.ParkingLayout.GetAvailableSlotCount;
using ParkingSystem.Parking.Application.ParkingLots;
using ParkingSystem.Parking.Domain.ParkingLayout;
using ParkingSystem.Parking.Domain.ParkingSessions;
using Xunit;

namespace ParkingSystem.Tests.Parking.UnitTests;

public sealed class ParkingLayoutTests
{
    private static readonly Guid LotId = Guid.Parse("10000000-0000-0000-0000-000000000001");

    // ASSUMPTION (open point 21): Unavailable > Maintenance > NeedsVerification > Assigned > Occupied > Available.
    [Theory]
    [InlineData(null, null, null, SlotEffectiveStatus.Available)]
    [InlineData(SensorState.Empty, null, null, SlotEffectiveStatus.Available)]
    [InlineData(SensorState.Occupied, null, null, SlotEffectiveStatus.Occupied)]
    [InlineData(SensorState.Empty, null, DispatchState.Assigned, SlotEffectiveStatus.Assigned)]
    [InlineData(SensorState.Occupied, null, DispatchState.Assigned, SlotEffectiveStatus.Assigned)]
    [InlineData(SensorState.Occupied, null, DispatchState.NeedsVerification, SlotEffectiveStatus.NeedsVerification)]
    [InlineData(SensorState.Empty, OverrideState.Maintenance, DispatchState.NeedsVerification, SlotEffectiveStatus.Maintenance)]
    [InlineData(SensorState.Occupied, OverrideState.Unavailable, DispatchState.Assigned, SlotEffectiveStatus.Unavailable)]
    [InlineData(null, OverrideState.Unavailable, null, SlotEffectiveStatus.Unavailable)]
    public void Effective_status_follows_the_priority_order(
        SensorState? sensor, OverrideState? @override, DispatchState? dispatch, SlotEffectiveStatus expected)
    {
        Assert.Equal(expected, ParkingSlot.ComputeEffectiveStatus(sensor, @override, dispatch));
    }

    [Fact]
    public void New_slot_is_available_with_a_normalized_code()
    {
        var slot = ParkingSlot.Create(Guid.NewGuid(), LotId, Guid.NewGuid(), " a-01 ");

        Assert.Equal("A-01", slot.SlotCode);
        Assert.Equal(SlotEffectiveStatus.Available, slot.EffectiveStatus);
        Assert.Null(slot.DispatchState);
        Assert.Null(slot.AssignedSessionId);
    }

    [Fact]
    public void Slot_declared_unavailable_in_the_layout_is_unavailable()
    {
        var slot = ParkingSlot.Create(Guid.NewGuid(), LotId, Guid.NewGuid(), "A-02", OverrideState.Unavailable);

        Assert.Equal(OverrideState.Unavailable, slot.OverrideState);
        Assert.Equal(SlotEffectiveStatus.Unavailable, slot.EffectiveStatus);
    }

    [Fact]
    public void Releasing_an_assignment_clears_only_the_dispatch_layer()
    {
        var slot = ParkingSlot.Create(Guid.NewGuid(), LotId, Guid.NewGuid(), "A-03", OverrideState.Maintenance);

        slot.ReleaseAssignment();

        Assert.Null(slot.DispatchState);
        Assert.Null(slot.AssignedSessionId);
        Assert.Null(slot.AssignedAt);
        Assert.Equal(OverrideState.Maintenance, slot.OverrideState);
        Assert.Equal(SlotEffectiveStatus.Maintenance, slot.EffectiveStatus);
        Assert.Equal(1, slot.RowVersion);
    }

    [Fact]
    public void Zone_rejects_negative_capacity()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Zone.Create(Guid.NewGuid(), Guid.NewGuid(), "A", VehicleType.Car, capacity: -1, hasSlotSensor: true));
    }

    [Fact]
    public async Task Available_count_is_returned_for_an_approved_lot()
    {
        var handler = new GetAvailableSlotCountQueryHandler(new FakeLotReader(approved: true), new FakeAvailableSlotReader(7));

        var result = await handler.Handle(new GetAvailableSlotCountQuery(LotId), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(7, result.Value);
    }

    [Fact]
    public async Task Available_count_rejects_an_unknown_or_unapproved_lot()
    {
        var handler = new GetAvailableSlotCountQueryHandler(new FakeLotReader(approved: false), new FakeAvailableSlotReader(7));

        var result = await handler.Handle(new GetAvailableSlotCountQuery(LotId), TestContext.Current.CancellationToken);

        Assert.Equal(GetAvailableSlotCountErrors.LotNotFound, result.Error);
    }

    // The real reader returns only approved lots, so "not found" covers unknown and not-approved lots.
    private sealed class FakeLotReader(bool approved) : IParkingLotDetailReader
    {
        public Task<ParkingLotDetail?> GetApprovedByIdAsync(Guid parkingLotId, CancellationToken cancellationToken) =>
            Task.FromResult(approved ? new ParkingLotDetail(parkingLotId, "Lot A", "1 Demo Street", 10.77m, 106.70m, "Outdoor") : null);
    }

    private sealed class FakeAvailableSlotReader(int count) : IAvailableSlotReader
    {
        public Task<int> CountAvailableCarSlotsAsync(Guid lotId, CancellationToken cancellationToken) => Task.FromResult(count);
    }
}
