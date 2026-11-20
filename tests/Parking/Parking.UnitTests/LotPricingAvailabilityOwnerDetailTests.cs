using ParkingSystem.BuildingBlocks.Time;
using ParkingSystem.Parking.Application.Common.Constants;
using ParkingSystem.Parking.Application.Common.Interfaces;
using ParkingSystem.Parking.Application.Features.Pricing.Queries.GetLotPricing;
using ParkingSystem.Parking.Application.ParkingLayout;
using ParkingSystem.Parking.Application.ParkingLots;
using ParkingSystem.Parking.Application.ParkingLots.AdminLots;
using ParkingSystem.Parking.Application.ParkingLots.Availability;
using ParkingSystem.Parking.Application.ParkingLayout.HeldQuotaShortage;
using ParkingSystem.Parking.Application.ParkingLots.OwnerLots;
using ParkingSystem.Parking.Domain.Entities;
using Xunit;

namespace ParkingSystem.Tests.Parking.UnitTests;

// Demo 1 steps 10 and 13: a customer sees a lot's prices and free spaces; an owner reads a lot in any approval state.
public sealed class LotPricingAvailabilityOwnerDetailTests
{
    private static readonly Guid ApprovedLot = Guid.Parse("a0000000-0000-0000-0000-000000000001");
    private static readonly Guid PendingLot = Guid.Parse("a0000000-0000-0000-0000-000000000002");
    private static readonly Guid Owner = Guid.Parse("b0000000-0000-0000-0000-000000000001");
    private static readonly Guid Stranger = Guid.Parse("b0000000-0000-0000-0000-000000000002");
    private static readonly DateTimeOffset Now = new(2026, 10, 30, 8, 0, 0, TimeSpan.Zero);

    // ---- PK-05: every vehicle type's current price in one call ----

    [Fact]
    public async Task Pricing_lists_the_latest_price_of_each_vehicle_type_of_an_approved_lot()
    {
        var prices = new FakePricing()
            .With(ApprovedLot, "CAR", version: 2, price: 30000m)
            .With(ApprovedLot, "MOTORBIKE", version: 1, price: 5000m);
        var handler = new GetLotPricingQueryHandler(prices, new FakeOwnership());

        var result = await handler.Handle(new GetLotPricingQuery(ApprovedLot), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(ApprovedLot, result.Value!.LotId);
        Assert.Collection(
            result.Value.Items,
            car => { Assert.Equal("CAR", car.VehicleType); Assert.Equal(2, car.Version); Assert.Equal(30000m, car.Price); },
            motorbike => { Assert.Equal("MOTORBIKE", motorbike.VehicleType); Assert.Equal(5000m, motorbike.Price); });
    }

    [Fact]
    public async Task Pricing_of_an_approved_lot_without_prices_is_an_empty_list()
    {
        var handler = new GetLotPricingQueryHandler(new FakePricing(), new FakeOwnership());

        var result = await handler.Handle(new GetLotPricingQuery(ApprovedLot), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value!.Items);
    }

    [Fact]
    public async Task Pricing_of_a_pending_lot_is_hidden_from_anonymous_callers_and_other_accounts()
    {
        var prices = new FakePricing().With(PendingLot, "CAR", version: 1, price: 20000m);
        var handler = new GetLotPricingQueryHandler(prices, new FakeOwnership());

        var anonymous = await handler.Handle(new GetLotPricingQuery(PendingLot), TestContext.Current.CancellationToken);
        var stranger = await handler.Handle(new GetLotPricingQuery(PendingLot, Stranger), TestContext.Current.CancellationToken);

        Assert.Equal(ParkingErrors.Pricing.LotNotFound, anonymous.Error);
        Assert.Equal(ParkingErrors.Pricing.LotNotFound, stranger.Error);
    }

    [Fact]
    public async Task Pricing_of_a_pending_lot_is_visible_to_its_owner()
    {
        var prices = new FakePricing().With(PendingLot, "CAR", version: 1, price: 20000m);
        var handler = new GetLotPricingQueryHandler(prices, new FakeOwnership());

        var result = await handler.Handle(new GetLotPricingQuery(PendingLot, Owner), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value!.Items);
    }

    [Fact]
    public async Task Pricing_of_an_unknown_or_empty_lot_id_fails()
    {
        var handler = new GetLotPricingQueryHandler(new FakePricing(), new FakeOwnership());

        var unknown = await handler.Handle(new GetLotPricingQuery(Guid.NewGuid()), TestContext.Current.CancellationToken);
        var empty = await handler.Handle(new GetLotPricingQuery(Guid.Empty), TestContext.Current.CancellationToken);

        Assert.Equal(ParkingErrors.Pricing.LotNotFound, unknown.Error);
        Assert.Equal(ParkingErrors.Pricing.LotIdEmpty, empty.Error);
    }

    // ---- PK-06: free spaces of an approved lot ----

    [Fact]
    public async Task Availability_reports_available_car_slots_and_motorbike_spaces_per_zone()
    {
        var motorbikes = new FakeMotorbikeZones(new MotorbikeAvailability(
        [
            new MotorbikeZoneAvailability(Guid.Parse("c0000000-0000-0000-0000-000000000001"), "M1", "Tầng 1", 40, 10),
            new MotorbikeZoneAvailability(Guid.Parse("c0000000-0000-0000-0000-000000000002"), "M2", "Tầng 2", 20, 25),
        ]));
        var handler = new GetLotAvailabilityQueryHandler(new FakeApprovedLots(), new FakeCarSlots(7), motorbikes, new FixedClock(), new FakeHeldQuotaProvider());

        var result = await handler.Handle(new GetLotAvailabilityQuery(ApprovedLot), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        var availability = result.Value!;
        Assert.Equal(ApprovedLot, availability.LotId);
        Assert.Equal(Now, availability.AsOfUtc);
        Assert.Equal(7, availability.Car.Available);
        Assert.NotNull(availability.Motorbike);
        Assert.Equal(60, availability.Motorbike!.Capacity);
        // A zone counted above its capacity shows 0 free spaces, never a negative number.
        Assert.Equal(30, availability.Motorbike.Available);
        Assert.Collection(
            availability.Motorbike.Zones,
            first => { Assert.Equal("M1", first.ZoneCode); Assert.Equal(30, first.Available); },
            second => { Assert.Equal("M2", second.ZoneCode); Assert.Equal(0, second.Available); });
    }

    [Fact]
    public async Task Availability_of_a_lot_without_motorbike_zone_has_no_motorbike_part()
    {
        var handler = new GetLotAvailabilityQueryHandler(new FakeApprovedLots(), new FakeCarSlots(3), new FakeMotorbikeZones(null), new FixedClock(), new FakeHeldQuotaProvider());

        var result = await handler.Handle(new GetLotAvailabilityQuery(ApprovedLot), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.Value!.Car.Available);
        Assert.Null(result.Value.Motorbike);
    }

    [Fact]
    public async Task Availability_of_a_lot_that_is_not_approved_is_not_found()
    {
        var handler = new GetLotAvailabilityQueryHandler(new FakeApprovedLots(), new FakeCarSlots(3), new FakeMotorbikeZones(null), new FixedClock(), new FakeHeldQuotaProvider());

        var result = await handler.Handle(new GetLotAvailabilityQuery(PendingLot), TestContext.Current.CancellationToken);

        Assert.Equal(GetLotAvailabilityErrors.LotNotFound, result.Error);
        Assert.Equal("parking.lot_not_found", result.Error!.Code);
    }

    [Fact]
    public async Task Availability_validator_rejects_an_empty_lot_id()
    {
        var validation = await new GetLotAvailabilityQueryValidator()
            .ValidateAsync(new GetLotAvailabilityQuery(Guid.Empty), TestContext.Current.CancellationToken);

        Assert.False(validation.IsValid);
    }

    // ---- PK-02: owner reads a lot in any approval state ----

    [Fact]
    public async Task Owner_reads_a_pending_lot_with_its_status_reason_and_prices()
    {
        var prices = new FakePricing().With(PendingLot, "CAR", version: 1, price: 20000m);
        var handler = new GetOwnerParkingLotDetailQueryHandler(new FakeAnyStateLots(), new FakeOwnership(), prices);

        var result = await handler.Handle(new GetOwnerParkingLotDetailQuery(PendingLot, Owner), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        var lot = result.Value!;
        Assert.Equal(PendingLot, lot.Id);
        Assert.Equal("Bãi chờ duyệt", lot.Name);
        Assert.Equal("Rejected", lot.ApprovalStatus);
        Assert.Equal("Thiếu giấy phép", lot.RejectionReason);
        Assert.Single(lot.Pricing);
    }

    [Fact]
    public async Task Owner_detail_denies_an_account_that_does_not_own_the_lot()
    {
        var handler = new GetOwnerParkingLotDetailQueryHandler(new FakeAnyStateLots(), new FakeOwnership(), new FakePricing());

        var result = await handler.Handle(new GetOwnerParkingLotDetailQuery(PendingLot, Stranger), TestContext.Current.CancellationToken);

        Assert.Equal(ParkingLotAccessErrors.AccessDenied, result.Error);
    }

    [Fact]
    public async Task Owner_detail_of_an_unknown_lot_is_not_found()
    {
        var handler = new GetOwnerParkingLotDetailQueryHandler(new FakeAnyStateLots(), new FakeOwnership(), new FakePricing());

        var result = await handler.Handle(new GetOwnerParkingLotDetailQuery(Guid.NewGuid(), Owner), TestContext.Current.CancellationToken);

        Assert.Equal(GetOwnerParkingLotDetailErrors.LotNotFound, result.Error);
        Assert.Equal("parking.lot_not_found", result.Error!.Code);
    }

    // ---- fakes ----

    private sealed class FakePricing : IPricingPolicyRepository
    {
        public Task<PricingPolicy?> GetEffectiveAtAsync(Guid lotId, string vehicleType, DateTimeOffset at, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        private readonly List<PricingPolicy> _policies = [];

        public FakePricing With(Guid lotId, string vehicleType, int version, decimal price)
        {
            _policies.Add(PricingPolicy.Create(Guid.NewGuid(), lotId, vehicleType, version, "Block", 60, price, 0m,
                new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc)));
            return this;
        }

        public Task<PricingPolicy?> GetLatestAsync(Guid lotId, string vehicleType, CancellationToken cancellationToken) =>
            Task.FromResult(_policies
                .Where(policy => policy.LotId == lotId && policy.VehicleType == vehicleType)
                .OrderByDescending(policy => policy.Version)
                .FirstOrDefault());

        public Task AddAsync(PricingPolicy policy, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<bool> SaveChangesAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class FakeOwnership : ILotOwnershipService
    {
        public Task<bool> LotExistsAsync(Guid lotId) => Task.FromResult(lotId == ApprovedLot || lotId == PendingLot);

        public Task<bool> IsLotOwnerAsync(Guid lotId, Guid callerId) =>
            Task.FromResult((lotId == ApprovedLot || lotId == PendingLot) && callerId == Owner);

        public Task<bool> IsLotApprovedAsync(Guid lotId) => Task.FromResult(lotId == ApprovedLot);
    }

    private sealed class FakeApprovedLots : IParkingLotDetailReader
    {
        public Task<ParkingLotDetail?> GetApprovedByIdAsync(Guid parkingLotId, CancellationToken cancellationToken) =>
            Task.FromResult(parkingLotId == ApprovedLot
                ? new ParkingLotDetail(ApprovedLot, "Bãi trung tâm", "1 Lê Lợi", 10.77m, 106.70m, "Outdoor")
                : null);
    }

    private sealed class FakeAnyStateLots : IAdminParkingLotDetailReader
    {
        public Task<AdminParkingLotDetail?> GetByIdAsync(Guid parkingLotId, CancellationToken cancellationToken) =>
            Task.FromResult(parkingLotId == PendingLot
                ? new AdminParkingLotDetail(PendingLot, Owner, "Bãi chờ duyệt", "2 Lê Lợi", 10.78m, 106.71m, "Outdoor",
                    "Rejected", "Thiếu giấy phép", null)
                : null);
    }

    private sealed class FakeCarSlots(int available) : IAvailableSlotReader
    {
        public Task<int> CountAvailableCarSlotsAsync(Guid lotId, CancellationToken cancellationToken) => Task.FromResult(available);
    }

    private sealed class FakeMotorbikeZones(MotorbikeAvailability? availability) : IMotorbikeZoneCounter
    {
        public Task<MotorbikeSpaceTaken?> TakeSpaceAsync(Guid lotId, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task ReleaseSpaceAsync(Guid zoneId, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<MotorbikeAvailability?> GetAvailabilityAsync(Guid lotId, CancellationToken cancellationToken) =>
            Task.FromResult(availability);
    }

    private sealed class FixedClock : IClock
    {
        public DateTimeOffset UtcNow => Now;
    }

    private sealed class FakeHeldQuotaProvider(int? held = 0) : IHeldQuotaProvider
    {
        public Task<int?> GetHeldReservationsAsync(Guid lotId, CancellationToken cancellationToken) => Task.FromResult(held);
    }
}
