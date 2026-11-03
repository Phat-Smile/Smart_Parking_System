using ParkingSystem.BuildingBlocks.Results;
using ParkingSystem.BuildingBlocks.Time;
using ParkingSystem.Parking.Application.ParkingLayout.ManageLayout;
using ParkingSystem.Parking.Application.ParkingLots;
using Xunit;

namespace ParkingSystem.Tests.Parking.UnitTests;

public sealed class PublicParkingLayoutTests
{
    private static readonly Guid Approved = Guid.NewGuid();
    private static readonly Guid Pending = Guid.NewGuid();

    [Fact]
    public async Task Approved_lot_is_read_with_timestamp_and_full_layout()
    {
        var repo = new FakeLayouts();
        var handler = new GetPublicParkingLayoutQueryHandler(new FakeApprovedLots(), repo, new FixedClock());

        var result = await handler.Handle(new GetPublicParkingLayoutQuery(Approved), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, repo.ReadCount);
        Assert.Equal(Approved, result.Value!.LotId);
        Assert.Equal(2, result.Value.Version);
        Assert.Equal(new FixedClock().UtcNow, result.Value.AsOfUtc);
    }

    [Fact]
    public async Task Pending_and_unknown_lots_cannot_read_layout()
    {
        var repo = new FakeLayouts();
        var handler = new GetPublicParkingLayoutQueryHandler(new FakeApprovedLots(), repo, new FixedClock());

        var pending = await handler.Handle(new GetPublicParkingLayoutQuery(Pending), TestContext.Current.CancellationToken);
        var empty = await handler.Handle(new GetPublicParkingLayoutQuery(Guid.Empty), TestContext.Current.CancellationToken);

        Assert.Equal(ParkingLayoutErrors.LotNotFound, pending.Error);
        Assert.Equal(ParkingLayoutErrors.LotNotFound, empty.Error);
        Assert.Equal(0, repo.ReadCount);
    }

    [Fact]
    public async Task Approved_lot_without_configured_floors_returns_an_empty_layout()
    {
        var handler = new GetPublicParkingLayoutQueryHandler(new FakeApprovedLots(), new FakeLayouts(), new FixedClock());

        var result = await handler.Handle(new GetPublicParkingLayoutQuery(Approved), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value!.Floors);
        Assert.Equal(2, result.Value.Version);
    }

    [Fact]
    public async Task Empty_lot_id_is_rejected_before_consulting_the_database()
    {
        var lots = new FakeApprovedLots();
        var layouts = new FakeLayouts();
        var handler = new GetPublicParkingLayoutQueryHandler(lots, layouts, new FixedClock());

        var result = await handler.Handle(new GetPublicParkingLayoutQuery(Guid.Empty), TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(ParkingLayoutErrors.LotNotFound, result.Error);
        Assert.Equal(0, lots.ReadCount);
        Assert.Equal(0, layouts.ReadCount);
    }

    [Fact]
    public async Task Unknown_lot_cannot_read_private_layout_data()
    {
        var lots = new FakeApprovedLots();
        var layouts = new FakeLayouts();
        var handler = new GetPublicParkingLayoutQueryHandler(lots, layouts, new FixedClock());

        var result = await handler.Handle(new GetPublicParkingLayoutQuery(Guid.NewGuid()), TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(ParkingLayoutErrors.LotNotFound, result.Error);
        Assert.Equal(1, lots.ReadCount);
        Assert.Equal(0, layouts.ReadCount);
    }

    [Fact]
    public async Task Public_read_preserves_the_stored_geometry_and_slot_states()
    {
        var zone = new LayoutZone(Guid.NewGuid(), "A", "Car", 2, 1, true,
        [
            new LayoutSlot(Guid.NewGuid(), "A01", null, "Occupied", new GridPoint(4, 7)),
            new LayoutSlot(Guid.NewGuid(), "A02", "Maintenance", "Maintenance", new GridPoint(5, 7))
        ], null, null);
        var floor = new LayoutFloor(Guid.NewGuid(), "Level 1", [zone]);
        IReadOnlyList<LayoutFloor> originalFloors = [floor];
        var layouts = new FakeLayouts { Layout = new ParkingLotLayout(Approved, 17, originalFloors) };
        var handler = new GetPublicParkingLayoutQueryHandler(new FakeApprovedLots(), layouts, new FixedClock());

        var result = await handler.Handle(new GetPublicParkingLayoutQuery(Approved), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(17, result.Value!.Version);
        Assert.Same(originalFloors, result.Value.Floors);
        Assert.Equal(new GridPoint(4, 7), result.Value.Floors[0].Zones[0].Slots[0].Position);
        Assert.Equal("Maintenance", result.Value.Floors[0].Zones[0].Slots[1].EffectiveStatus);
    }

    [Fact]
    public async Task Public_read_forwards_the_cancellation_token_to_both_readers()
    {
        var lots = new FakeApprovedLots();
        var layouts = new FakeLayouts();
        var handler = new GetPublicParkingLayoutQueryHandler(lots, layouts, new FixedClock());
        using var source = new CancellationTokenSource();

        var result = await handler.Handle(new GetPublicParkingLayoutQuery(Approved), source.Token);

        Assert.True(result.IsSuccess);
        Assert.Equal(source.Token, lots.LastCancellationToken);
        Assert.Equal(source.Token, layouts.LastCancellationToken);
    }

    private sealed class FakeApprovedLots : IParkingLotDetailReader
    {
        public int ReadCount { get; private set; }
        public CancellationToken LastCancellationToken { get; private set; }

        public Task<ParkingLotDetail?> GetApprovedByIdAsync(Guid lotId, CancellationToken ct)
        {
            ReadCount++;
            LastCancellationToken = ct;
            return Task.FromResult<ParkingLotDetail?>(lotId == Approved
                ? new ParkingLotDetail(lotId, "Approved", "Address", 10, 106, "Outdoor") : null);
        }
    }

    private sealed class FakeLayouts : IManageParkingLayoutRepository
    {
        public int ReadCount { get; private set; }
        public CancellationToken LastCancellationToken { get; private set; }
        public ParkingLotLayout? Layout { get; init; }
        public Task<bool> LotExistsAsync(Guid id, CancellationToken ct) => Task.FromResult(true);
        public Task<bool> IsOwnerAsync(Guid id, Guid actor, CancellationToken ct) => Task.FromResult(false);
        public Task<ParkingLotLayout> ReadAsync(Guid id, CancellationToken ct)
        {
            ReadCount++;
            LastCancellationToken = ct;
            return Task.FromResult(Layout ?? new ParkingLotLayout(id, 2, []));
        }
        public Task<Error?> SaveAsync(Guid id, Guid actor, int version, IReadOnlyList<LayoutFloorInput> floors,
            CancellationToken ct) => throw new NotSupportedException();
    }

    private sealed class FixedClock : IClock
    {
        public DateTimeOffset UtcNow => new(2026, 11, 2, 10, 0, 0, TimeSpan.Zero);
    }
}
