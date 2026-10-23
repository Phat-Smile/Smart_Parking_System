using ParkingSystem.BuildingBlocks.Identity;
using ParkingSystem.Parking.Application.ParkingLayout;
using ParkingSystem.Parking.Application.ParkingLayout.GetMotorbikeAvailability;
using ParkingSystem.Parking.Application.ParkingLots;
using Xunit;

namespace ParkingSystem.Tests.Parking.UnitTests;

public sealed class MotorbikeAvailabilityTests
{
    private static readonly Guid LotId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly Guid StaffId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Fact]
    public async Task Returns_the_lot_totals_and_each_motorbike_zone()
    {
        // Two motorbike zones on different floors, each with its own capacity (context 6.3).
        var handler = NewHandler(TwoZones(groundOccupied: 20, basementOccupied: 50));

        var result = await handler.Handle(new GetMotorbikeAvailabilityQuery(LotId, StaffId), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(150, result.Value!.TotalCapacity);
        Assert.Equal(70, result.Value.Occupied);
        Assert.Equal(80, result.Value.Available);
        Assert.False(result.Value.IsFull);
        Assert.Collection(result.Value.Zones,
            ground => { Assert.Equal("M1", ground.ZoneCode); Assert.Equal("Tầng 1", ground.FloorName); Assert.Equal(80, ground.Available); },
            basement => { Assert.Equal("M2", basement.ZoneCode); Assert.Equal(0, basement.Available); });
    }

    [Fact]
    public async Task The_lot_is_full_only_when_every_motorbike_zone_is_full()
    {
        var handler = NewHandler(TwoZones(groundOccupied: 100, basementOccupied: 50));

        var result = await handler.Handle(new GetMotorbikeAvailabilityQuery(LotId, StaffId), TestContext.Current.CancellationToken);

        Assert.Equal(0, result.Value!.Available);
        Assert.True(result.Value.IsFull);
    }

    [Fact]
    public async Task A_zone_over_its_capacity_shows_zero_and_does_not_lower_the_other_zones()
    {
        // The owner lowered a capacity below the current count: that zone shows 0, never a negative number.
        var handler = NewHandler(TwoZones(groundOccupied: 30, basementOccupied: 55));

        var result = await handler.Handle(new GetMotorbikeAvailabilityQuery(LotId, StaffId), TestContext.Current.CancellationToken);

        Assert.Equal(70, result.Value!.Available);
        Assert.Equal(0, result.Value.Zones[1].Available);
    }

    private static MotorbikeAvailability TwoZones(int groundOccupied, int basementOccupied) => new(
    [
        new MotorbikeZoneAvailability(Guid.NewGuid(), "M1", "Tầng 1", 100, groundOccupied),
        new MotorbikeZoneAvailability(Guid.NewGuid(), "M2", "Tầng hầm", 50, basementOccupied)
    ]);

    [Fact]
    public async Task Rejects_an_unknown_or_unapproved_lot()
    {
        var handler = NewHandler(TwoZones(0, 0), lotApproved: false);

        var result = await handler.Handle(new GetMotorbikeAvailabilityQuery(LotId, StaffId), TestContext.Current.CancellationToken);

        Assert.Equal(GetMotorbikeAvailabilityErrors.LotNotFound, result.Error);
    }

    [Fact]
    public async Task Refuses_staff_not_assigned_to_the_lot()
    {
        var handler = NewHandler(TwoZones(0, 0), assigned: false);

        var result = await handler.Handle(new GetMotorbikeAvailabilityQuery(LotId, StaffId), TestContext.Current.CancellationToken);

        Assert.Equal(ParkingLotAccessErrors.AccessDenied, result.Error);
    }

    [Fact]
    public async Task Reports_a_lot_without_a_motorbike_zone()
    {
        var handler = NewHandler(availability: null);

        var result = await handler.Handle(new GetMotorbikeAvailabilityQuery(LotId, StaffId), TestContext.Current.CancellationToken);

        Assert.Equal(GetMotorbikeAvailabilityErrors.MotorbikeZoneNotConfigured, result.Error);
    }

    private static GetMotorbikeAvailabilityQueryHandler NewHandler(
        MotorbikeAvailability? availability, bool lotApproved = true, bool assigned = true) =>
        new(new FakeParkingLotReader(lotApproved), new FakeLotAccess(assigned), new FakeMotorbikeZones(availability));

    private sealed class FakeParkingLotReader(bool approved) : IParkingLotDetailReader
    {
        public Task<ParkingLotDetail?> GetApprovedByIdAsync(Guid parkingLotId, CancellationToken cancellationToken) =>
            Task.FromResult(approved ? new ParkingLotDetail(parkingLotId, "Lot A", "1 Test Street", 10.77m, 106.70m, "Flat") : null);
    }

    private sealed class FakeLotAccess(bool allowed) : IParkingLotAccess
    {
        public Task<bool> CanOperateAsync(Guid accountId, Guid lotId, CancellationToken cancellationToken) => Task.FromResult(allowed);
    }

    private sealed class FakeMotorbikeZones(MotorbikeAvailability? availability) : IMotorbikeZoneCounter
    {
        public Task<MotorbikeSpaceTaken?> TakeSpaceAsync(Guid lotId, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task ReleaseSpaceAsync(Guid zoneId, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<MotorbikeAvailability?> GetAvailabilityAsync(Guid lotId, CancellationToken cancellationToken) => Task.FromResult(availability);
    }
}
