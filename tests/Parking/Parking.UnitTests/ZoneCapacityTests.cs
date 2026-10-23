using ParkingSystem.Parking.Application.ParkingLayout;
using ParkingSystem.Parking.Application.ParkingLayout.HeldQuotaShortage;
using ParkingSystem.Parking.Application.ParkingLayout.ZoneCapacity;
using Xunit;

namespace ParkingSystem.Tests.Parking.UnitTests;

// US-OWNER-003: zone capacity view and the shared BR-02.f check.
public sealed class ZoneCapacityTests
{
    private static readonly Guid LotId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly Guid OwnerId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    [Fact]
    public async Task Owner_sees_each_zone_and_the_totals_per_vehicle_group()
    {
        var floors = new[]
        {
            new FloorCapacity(Guid.NewGuid(), "Tầng 1",
            [
                new ZoneCapacityItem(Guid.NewGuid(), "A1", "Car", 20, 15, 13, 6),
                new ZoneCapacityItem(Guid.NewGuid(), "M1", "Motorbike", 100, null, null, 80)
            ]),
            new FloorCapacity(Guid.NewGuid(), "Tầng hầm", [new ZoneCapacityItem(Guid.NewGuid(), "M2", "Motorbike", 50, null, null, 0)])
        };
        var handler = new GetZoneCapacityQueryHandler(new FakeReader(isOwner: true, floors), new NoWarning());

        var result = await handler.Handle(new GetZoneCapacityQuery(LotId, OwnerId), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(new CapacityTotals(20, 150), result.Value!.Totals);
        Assert.Equal(2, result.Value.Floors.Count);
        Assert.Empty(result.Value.Warnings);
    }

    [Fact]
    public async Task A_caller_who_is_not_owner_of_the_lot_gets_not_found()
    {
        var handler = new GetZoneCapacityQueryHandler(new FakeReader(isOwner: false, []), new NoWarning());

        var result = await handler.Handle(new GetZoneCapacityQuery(LotId, OwnerId), TestContext.Current.CancellationToken);

        Assert.Equal(ZoneCapacityErrors.LotNotFoundOrNotOwned, result.Error);
    }

    [Fact]
    public async Task Shortage_check_warns_only_when_available_slots_are_below_the_held_quota()
    {
        var token = TestContext.Current.CancellationToken;
        Assert.Null(await new HeldQuotaShortageCheck(new FixedSlots(3), new FixedHeld(0)).CheckAsync(LotId, token));
        Assert.Null(await new HeldQuotaShortageCheck(new FixedSlots(5), new FixedHeld(5)).CheckAsync(LotId, token));

        var warning = await new HeldQuotaShortageCheck(new FixedSlots(8), new FixedHeld(10)).CheckAsync(LotId, token);

        Assert.Equal(HeldQuotaShortageCheck.WarningCode, warning!.Code);
        Assert.Equal(8, warning.AvailableSlots);
        Assert.Equal(10, warning.HeldQuota);
    }

    [Fact]
    public async Task The_view_carries_the_shortage_warning_without_blocking()
    {
        var handler = new GetZoneCapacityQueryHandler(
            new FakeReader(isOwner: true, []),
            new HeldQuotaShortageCheck(new FixedSlots(1), new FixedHeld(2)));

        var result = await handler.Handle(new GetZoneCapacityQuery(LotId, OwnerId), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(HeldQuotaShortageCheck.WarningCode, Assert.Single(result.Value!.Warnings).Code);
    }

    private sealed class FakeReader(bool isOwner, IReadOnlyList<FloorCapacity> floors) : IZoneCapacityReader
    {
        public Task<bool> IsOwnerAsync(Guid lotId, Guid ownerAccountId, CancellationToken cancellationToken) => Task.FromResult(isOwner);

        public Task<IReadOnlyList<FloorCapacity>> ReadAsync(Guid lotId, CancellationToken cancellationToken) => Task.FromResult(floors);
    }

    private sealed class NoWarning : IHeldQuotaShortageCheck
    {
        public Task<CapacityWarning?> CheckAsync(Guid lotId, CancellationToken cancellationToken) => Task.FromResult<CapacityWarning?>(null);
    }

    private sealed class FixedSlots(int count) : IAvailableSlotReader
    {
        public Task<int> CountAvailableCarSlotsAsync(Guid lotId, CancellationToken cancellationToken) => Task.FromResult(count);
    }

    private sealed class FixedHeld(int count) : IHeldQuotaReader
    {
        public Task<int> CountHeldReservationsAsync(Guid lotId, CancellationToken cancellationToken) => Task.FromResult(count);
    }
}
