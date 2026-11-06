using ParkingSystem.Parking.Application.Common.Interfaces;
using ParkingSystem.Parking.Application.ParkingLayout;
using ParkingSystem.Parking.Application.ParkingLayout.ZoneCapacity;
using ParkingSystem.Parking.Application.ParkingLots;
using ParkingSystem.Parking.Application.ParkingLots.ReservationInfo;
using ParkingSystem.Parking.Domain.Entities;
using Xunit;

namespace ParkingSystem.Tests.Parking.UnitTests;

// P0-A: the two facts Booking needs from Parking before it can create a reservation hold.
public sealed class LotReservationFactsTests
{
    private static readonly Guid LotId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly Guid OtherLotId = Guid.Parse("20000000-0000-0000-0000-000000000002");

    // ---- reservation info ----

    [Fact]
    public async Task Valid_capacity_sums_valid_car_slots_and_ignores_motorbike_zones()
    {
        var handler = InfoHandler(zones:
        [
            Zone("Car", validCapacity: 4),
            Zone("Car", validCapacity: 3),
            Zone("Motorbike", validCapacity: null, capacity: 40)
        ]);

        var result = await handler.Handle(new GetLotReservationInfoQuery(LotId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(7, result.Value!.ValidCapacity);
    }

    [Fact]
    public async Task Lot_without_policies_gets_the_documented_defaults()
    {
        var handler = InfoHandler(zones: [Zone("Car", 10)]);

        var result = await handler.Handle(new GetLotReservationInfoQuery(LotId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(ReservationRatioPolicy.DefaultReservationRatioPercent, result.Value!.ReservationRatioPercent);
        Assert.Equal(20_000L, result.Value.DepositAmount);
        Assert.True(result.Value.ReservationEnabled);
    }

    [Fact]
    public async Task Configured_policies_override_the_defaults()
    {
        var handler = InfoHandler(
            zones: [Zone("Car", 10)],
            ratio: ReservationRatioPolicy.Create(LotId, 30),
            deposit: ReservationDepositPolicy.Create(LotId, 35_000m),
            availability: ReservationAvailabilityPolicy.Create(LotId, false),
            availableSlots: 6);

        var result = await handler.Handle(new GetLotReservationInfoQuery(LotId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(30, result.Value!.ReservationRatioPercent);
        Assert.Equal(35_000L, result.Value.DepositAmount);
        Assert.False(result.Value.ReservationEnabled);
        Assert.Equal(6, result.Value.AvailableSlotCount);
    }

    [Fact]
    public async Task Lot_with_no_car_zone_has_zero_valid_capacity()
    {
        var handler = InfoHandler(zones: [Zone("Motorbike", null, capacity: 20)]);

        var result = await handler.Handle(new GetLotReservationInfoQuery(LotId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.Value!.ValidCapacity);
    }

    [Fact]
    public async Task Unknown_or_unapproved_lot_is_not_found_for_reservation_info()
    {
        var handler = InfoHandler(zones: [Zone("Car", 10)]);

        var result = await handler.Handle(new GetLotReservationInfoQuery(OtherLotId), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(LotReservationFactsErrors.LotNotFound, result.Error);
    }

    // ---- pricing ----

    [Fact]
    public async Task Pricing_returns_the_latest_policy_of_the_vehicle_group()
    {
        var handler = PricingHandler(
            PricingPolicy.Create(LotId, "CAR", 1, "Block", 60, 10_000m, 0m),
            PricingPolicy.Create(LotId, "CAR", 2, "Block", 30, 12_000m, 30_000m),
            PricingPolicy.Create(LotId, "MOTORBIKE", 1, "PerEntry", null, 5_000m, 0m));

        var result = await handler.Handle(new GetLotPricingForBookingQuery(LotId, "Car"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(12_000L, result.Value!.Price);
        Assert.Equal("Block", result.Value.BillingMode);
        Assert.Equal(30, result.Value.BlockMinutes);
        Assert.Equal(30_000L, result.Value.OvernightFee);
        Assert.Equal(2, result.Value.PolicyVersion);
    }

    [Fact]
    public async Task Per_entry_pricing_has_zero_block_minutes()
    {
        var handler = PricingHandler(PricingPolicy.Create(LotId, "MOTORBIKE", 1, "PerEntry", null, 5_000m, 0m));

        var result = await handler.Handle(new GetLotPricingForBookingQuery(LotId, "Motorbike"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("PerEntry", result.Value!.BillingMode);
        Assert.Equal(0, result.Value.BlockMinutes);
    }

    [Theory]
    [InlineData("")]
    [InlineData("Truck")]
    [InlineData("Bicycle")]
    public async Task Unknown_vehicle_group_is_rejected(string group)
    {
        var handler = PricingHandler(PricingPolicy.Create(LotId, "CAR", 1, "Block", 60, 10_000m, 0m));

        var result = await handler.Handle(new GetLotPricingForBookingQuery(LotId, group), CancellationToken.None);

        Assert.Equal(LotPricingForBookingErrors.InvalidVehicleGroup, result.Error);
    }

    [Fact]
    public async Task Lot_without_a_price_list_for_the_group_is_a_precondition_failure()
    {
        var handler = PricingHandler(PricingPolicy.Create(LotId, "MOTORBIKE", 1, "PerEntry", null, 5_000m, 0m));

        var result = await handler.Handle(new GetLotPricingForBookingQuery(LotId, "Car"), CancellationToken.None);

        Assert.Equal(LotPricingForBookingErrors.PolicyNotFound, result.Error);
    }

    [Theory]
    [InlineData(10_000.5, 0)]
    [InlineData(10_000, 100.25)]
    public async Task Fractional_money_is_never_rounded(decimal price, decimal overnight)
    {
        var handler = PricingHandler(PricingPolicy.Create(LotId, "CAR", 1, "Block", 60, price, overnight));

        var result = await handler.Handle(new GetLotPricingForBookingQuery(LotId, "Car"), CancellationToken.None);

        Assert.Equal(LotPricingForBookingErrors.NotWholeAmount, result.Error);
    }

    [Fact]
    public async Task Unknown_or_unapproved_lot_is_not_found_for_pricing()
    {
        var handler = PricingHandler(PricingPolicy.Create(OtherLotId, "CAR", 1, "Block", 60, 10_000m, 0m));

        var result = await handler.Handle(new GetLotPricingForBookingQuery(OtherLotId, "Car"), CancellationToken.None);

        Assert.Equal(LotPricingForBookingErrors.LotNotFound, result.Error);
    }

    // ---- helpers ----

    private static ZoneCapacityItem Zone(string group, int? validCapacity, int capacity = 10) =>
        new(Guid.NewGuid(), "Z" + Guid.NewGuid().ToString("N")[..4], group, capacity,
            group == "Car" ? validCapacity : null, validCapacity, 0);

    private static GetLotReservationInfoQueryHandler InfoHandler(
        IReadOnlyList<ZoneCapacityItem> zones,
        ReservationRatioPolicy? ratio = null,
        ReservationDepositPolicy? deposit = null,
        ReservationAvailabilityPolicy? availability = null,
        int availableSlots = 3) =>
        new(new FakeLots(),
            new FakeZones(zones),
            new FakeAvailable(availableSlots),
            new FakeRatio(ratio),
            new FakeDeposit(deposit),
            new FakeAvailability(availability));

    private static GetLotPricingForBookingQueryHandler PricingHandler(params PricingPolicy[] policies) =>
        new(new FakeLots(), new FakePricing(policies));

    private sealed class FakeLots : IParkingLotDetailReader
    {
        public Task<ParkingLotDetail?> GetApprovedByIdAsync(Guid parkingLotId, CancellationToken cancellationToken) =>
            Task.FromResult<ParkingLotDetail?>(parkingLotId == LotId
                ? new ParkingLotDetail(parkingLotId, "Lot A", "1 Demo Street", 10.77m, 106.70m, "Outdoor")
                : null);
    }

    private sealed class FakeZones(IReadOnlyList<ZoneCapacityItem> zones) : IZoneCapacityReader
    {
        public Task<bool> IsOwnerAsync(Guid lotId, Guid ownerAccountId, CancellationToken cancellationToken) =>
            Task.FromResult(false);

        public Task<IReadOnlyList<FloorCapacity>> ReadAsync(Guid lotId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<FloorCapacity>>([new FloorCapacity(Guid.NewGuid(), "F1", zones)]);
    }

    private sealed class FakeAvailable(int count) : IAvailableSlotReader
    {
        public Task<int> CountAvailableCarSlotsAsync(Guid lotId, CancellationToken cancellationToken) =>
            Task.FromResult(count);
    }

    private sealed class FakeRatio(ReservationRatioPolicy? policy) : IReservationRatioPolicyRepository
    {
        public Task<ReservationRatioPolicy?> GetByLotIdAsync(Guid lotId, CancellationToken cancellationToken = default) =>
            Task.FromResult(policy);
        public Task AddAsync(ReservationRatioPolicy policy, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<bool> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task<ReservationRatioPolicy> UpsertAsync(Guid lotId, int ratioPercent, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class FakeDeposit(ReservationDepositPolicy? policy) : IReservationDepositPolicyRepository
    {
        public Task<ReservationDepositPolicy?> GetByLotIdAsync(Guid lotId, CancellationToken cancellationToken = default) =>
            Task.FromResult(policy);
        public Task AddAsync(ReservationDepositPolicy policy, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<bool> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);
    }

    private sealed class FakeAvailability(ReservationAvailabilityPolicy? policy) : IReservationAvailabilityPolicyRepository
    {
        public Task<ReservationAvailabilityPolicy?> GetByLotIdAsync(Guid lotId, CancellationToken cancellationToken = default) =>
            Task.FromResult(policy);
        public Task AddAsync(ReservationAvailabilityPolicy policy, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<bool> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task<ReservationAvailabilityPolicy> UpsertAsync(Guid lotId, bool reservationEnabled, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class FakePricing(PricingPolicy[] policies) : IPricingPolicyRepository
    {
        public Task<PricingPolicy?> GetLatestAsync(Guid lotId, string vehicleType, CancellationToken cancellationToken) =>
            Task.FromResult(policies
                .Where(p => p.LotId == lotId && p.VehicleType == vehicleType)
                .OrderByDescending(p => p.Version)
                .FirstOrDefault());
        public Task AddAsync(PricingPolicy policy, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<bool> SaveChangesAsync(CancellationToken cancellationToken) => Task.FromResult(true);
    }
}
