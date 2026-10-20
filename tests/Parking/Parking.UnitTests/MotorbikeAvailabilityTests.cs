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

    [Theory]
    [InlineData(100, 20, 80, false)]
    [InlineData(100, 100, 0, true)]
    // The count can exceed the capacity after the owner lowers it; the screen then shows 0, never a negative number.
    [InlineData(100, 105, 0, true)]
    public async Task Returns_capacity_occupied_and_spaces_left(int capacity, int occupied, int expectedAvailable, bool expectedFull)
    {
        var handler = NewHandler(new MotorbikeAvailability(capacity, occupied));

        var result = await handler.Handle(new GetMotorbikeAvailabilityQuery(LotId, StaffId), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(capacity, result.Value!.Capacity);
        Assert.Equal(occupied, result.Value.Occupied);
        Assert.Equal(expectedAvailable, result.Value.Available);
        Assert.Equal(expectedFull, result.Value.IsFull);
    }

    [Fact]
    public async Task Rejects_an_unknown_or_unapproved_lot()
    {
        var handler = NewHandler(new MotorbikeAvailability(100, 0), lotApproved: false);

        var result = await handler.Handle(new GetMotorbikeAvailabilityQuery(LotId, StaffId), TestContext.Current.CancellationToken);

        Assert.Equal(GetMotorbikeAvailabilityErrors.LotNotFound, result.Error);
    }

    [Fact]
    public async Task Refuses_staff_not_assigned_to_the_lot()
    {
        var handler = NewHandler(new MotorbikeAvailability(100, 0), assigned: false);

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
