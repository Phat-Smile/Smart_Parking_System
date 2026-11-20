using ParkingSystem.BuildingBlocks.Time;
using ParkingSystem.Parking.Application.ParkingLayout;
using ParkingSystem.Parking.Application.ParkingLayout.HeldQuotaShortage;
using ParkingSystem.Parking.Application.ParkingLots;
using ParkingSystem.Parking.Application.ParkingLots.Availability;
using Xunit;

namespace ParkingSystem.Tests.Parking.UnitTests;

public sealed class WalkInPlacesCalculatorTests
{
    private static readonly Guid ApprovedLotId = Guid.Parse("10000000-0000-0000-0000-000000000001");

    [Theory]
    [InlineData(0, 0, 0, WalkInPlacesCalculator.StatusFull, WalkInPlacesCalculator.FullMessage)]
    [InlineData(0, 5, 0, WalkInPlacesCalculator.StatusFull, WalkInPlacesCalculator.FullMessage)]
    [InlineData(-2, 3, 0, WalkInPlacesCalculator.StatusFull, WalkInPlacesCalculator.FullMessage)]
    [InlineData(4, 5, 0, WalkInPlacesCalculator.StatusWalkInFull, WalkInPlacesCalculator.NoPlacesForUnreservedMessage)]
    [InlineData(4, 4, 0, WalkInPlacesCalculator.StatusWalkInFull, WalkInPlacesCalculator.NoPlacesForUnreservedMessage)]
    [InlineData(10, 3, 7, WalkInPlacesCalculator.StatusAvailable, WalkInPlacesCalculator.AvailableMessage)]
    [InlineData(10, 0, 10, WalkInPlacesCalculator.StatusAvailable, WalkInPlacesCalculator.AvailableMessage)]
    [InlineData(10, -1, 10, WalkInPlacesCalculator.StatusAvailable, WalkInPlacesCalculator.AvailableMessage)]
    public void Calculate_correctly_computes_walk_in_places_and_3_states(
        int availableSlots, int heldQuota, int expectedPlaces, string expectedStatus, string expectedMessage)
    {
        var (places, status, message) = WalkInPlacesCalculator.Calculate(availableSlots, heldQuota);

        Assert.Equal(expectedPlaces, places);
        Assert.Equal(expectedStatus, status);
        Assert.Equal(expectedMessage, message);
    }

    [Fact]
    public async Task GetLotAvailability_populates_walk_in_places_when_held_quota_is_active()
    {
        var approvedLots = new FakeApprovedLots();
        var carSlots = new FakeCarSlots(10);
        var motorbikeZones = new FakeMotorbikeZones(null);
        var clock = new FixedClock();
        var heldQuotaProvider = new FakeHeldQuotaProvider(3);

        var handler = new GetLotAvailabilityQueryHandler(approvedLots, carSlots, motorbikeZones, clock, heldQuotaProvider);

        var result = await handler.Handle(new GetLotAvailabilityQuery(ApprovedLotId), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(10, result.Value!.Car.Available);
        Assert.Equal(7, result.Value.Car.WalkInPlaces);
        Assert.Equal(WalkInPlacesCalculator.StatusAvailable, result.Value.Car.WalkInStatus);
        Assert.Equal(WalkInPlacesCalculator.AvailableMessage, result.Value.Car.WalkInMessage);
    }

    [Fact]
    public async Task GetLotAvailability_returns_walk_in_full_when_available_slots_equal_or_below_held_quota()
    {
        var approvedLots = new FakeApprovedLots();
        var carSlots = new FakeCarSlots(4);
        var motorbikeZones = new FakeMotorbikeZones(null);
        var clock = new FixedClock();
        var heldQuotaProvider = new FakeHeldQuotaProvider(5);

        var handler = new GetLotAvailabilityQueryHandler(approvedLots, carSlots, motorbikeZones, clock, heldQuotaProvider);

        var result = await handler.Handle(new GetLotAvailabilityQuery(ApprovedLotId), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(4, result.Value!.Car.Available);
        Assert.Equal(0, result.Value.Car.WalkInPlaces);
        Assert.Equal(WalkInPlacesCalculator.StatusWalkInFull, result.Value.Car.WalkInStatus);
        Assert.Equal(WalkInPlacesCalculator.NoPlacesForUnreservedMessage, result.Value.Car.WalkInMessage);
    }

    [Fact]
    public async Task GetLotAvailability_returns_full_when_available_slots_is_zero()
    {
        var approvedLots = new FakeApprovedLots();
        var carSlots = new FakeCarSlots(0);
        var motorbikeZones = new FakeMotorbikeZones(null);
        var clock = new FixedClock();
        var heldQuotaProvider = new FakeHeldQuotaProvider(2);

        var handler = new GetLotAvailabilityQueryHandler(approvedLots, carSlots, motorbikeZones, clock, heldQuotaProvider);

        var result = await handler.Handle(new GetLotAvailabilityQuery(ApprovedLotId), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.Value!.Car.Available);
        Assert.Equal(0, result.Value.Car.WalkInPlaces);
        Assert.Equal(WalkInPlacesCalculator.StatusFull, result.Value.Car.WalkInStatus);
        Assert.Equal(WalkInPlacesCalculator.FullMessage, result.Value.Car.WalkInMessage);
    }

    [Fact]
    public async Task GetLotAvailability_handles_zero_held_quota_when_no_reservations_held()
    {
        var approvedLots = new FakeApprovedLots();
        var carSlots = new FakeCarSlots(8);
        var motorbikeZones = new FakeMotorbikeZones(null);
        var clock = new FixedClock();
        var heldQuotaProvider = new FakeHeldQuotaProvider(null);

        var handler = new GetLotAvailabilityQueryHandler(approvedLots, carSlots, motorbikeZones, clock, heldQuotaProvider);

        var result = await handler.Handle(new GetLotAvailabilityQuery(ApprovedLotId), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(8, result.Value!.Car.Available);
        Assert.Equal(8, result.Value.Car.WalkInPlaces);
        Assert.Equal(WalkInPlacesCalculator.StatusAvailable, result.Value.Car.WalkInStatus);
    }

    private sealed class FakeApprovedLots : IParkingLotDetailReader
    {
        public Task<ParkingLotDetail?> GetApprovedByIdAsync(Guid parkingLotId, CancellationToken cancellationToken) =>
            Task.FromResult<ParkingLotDetail?>(new ParkingLotDetail(parkingLotId, "Test Lot", "Address", 10m, 106m, "Surface"));
    }

    private sealed class FakeCarSlots(int available) : IAvailableSlotReader
    {
        public Task<int> CountAvailableCarSlotsAsync(Guid parkingLotId, CancellationToken cancellationToken) =>
            Task.FromResult(available);
    }

    private sealed class FakeMotorbikeZones(MotorbikeAvailability? availability) : IMotorbikeZoneCounter
    {
        public Task<MotorbikeSpaceTaken?> TakeSpaceAsync(Guid lotId, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task ReleaseSpaceAsync(Guid zoneId, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<MotorbikeAvailability?> GetAvailabilityAsync(Guid parkingLotId, CancellationToken cancellationToken) =>
            Task.FromResult(availability);
    }

    private sealed class FakeHeldQuotaProvider(int? held) : IHeldQuotaProvider
    {
        public Task<int?> GetHeldReservationsAsync(Guid lotId, CancellationToken cancellationToken) =>
            Task.FromResult(held);
    }

    private sealed class FixedClock : IClock
    {
        public DateTimeOffset UtcNow => new(2026, 11, 20, 10, 0, 0, TimeSpan.Zero);
    }
}
