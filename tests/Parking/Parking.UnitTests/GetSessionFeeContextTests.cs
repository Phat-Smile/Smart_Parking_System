using ParkingSystem.Parking.Application.Common.Interfaces;
using ParkingSystem.Parking.Application.ParkingSessions;
using ParkingSystem.Parking.Application.ParkingSessions.GetSessionFeeContext;
using ParkingSystem.Parking.Domain.Entities;
using ParkingSystem.Parking.Domain.ParkingSessions;
using Xunit;

namespace ParkingSystem.Tests.Parking.UnitTests;

// Contract 6: what Payment needs to compute the fee, with the price in force at entry (BR-09.a).
public sealed class GetSessionFeeContextTests
{
    private static readonly Guid LotId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly Guid UserId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly DateTimeOffset EntryTime = new(2026, 11, 6, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Returns_the_session_and_its_block_price()
    {
        var session = ParkingSession.Open(Guid.NewGuid(), LotId, "51A-123.45", VehicleType.Car, EntryTime, "qr", UserId, null);
        var policy = PricingPolicy.Create(LotId, "Car", 1, "Block", 30, 15000m, 20000m);
        var prices = new FakePrices(policy);

        var result = await Handle(session, prices);

        var fee = result.Value!;
        Assert.Null(fee.LastPaidAtUtc);
        Assert.Null(fee.ArrivedAtExitUtc);
        Assert.Equal(15, fee.GracePeriodMinutes);
        Assert.Equal(session.Id, fee.SessionId);
        Assert.Equal(LotId, fee.LotId);
        Assert.Equal("51A-123.45", fee.PlateNumber);
        Assert.Equal("Car", fee.VehicleType);
        Assert.Equal("Active", fee.Status);
        Assert.Equal(EntryTime, fee.EntryTimeUtc);
        Assert.Equal("RegisteredWalkIn", fee.CustomerType);
        Assert.Equal(UserId, fee.AccountId);
        Assert.Null(fee.BookingId);
        Assert.Equal(new SessionPricing(policy.Id, "Block", 30, 15000, 20000), fee.Pricing);
    }

    [Fact]
    public async Task A_paid_session_returns_what_payment_needs_for_the_overstay_fee()
    {
        // US-PAY-005 (BR-09.d): paid at T, window extended once by staff, reached the exit gate at T + 20 minutes.
        var paidAt = EntryTime.AddHours(2);
        var session = ParkingSession.Open(Guid.NewGuid(), LotId, "51A-123.45", VehicleType.Car, EntryTime, "qr", UserId, null);
        Assert.True(session.MarkPaid(paidAt));
        Assert.True(session.ExtendExitWindow(paidAt.AddMinutes(5), "traffic jam on ramp B2"));
        Assert.True(session.RecordExitArrival(paidAt.AddMinutes(20)));

        var result = await Handle(session, new FakePrices(PricingPolicy.Create(LotId, "Car", 1, "Block", 30, 15000m, 0m)));

        Assert.Equal(paidAt, result.Value!.LastPaidAtUtc);
        Assert.Equal(paidAt.AddMinutes(20), result.Value.ArrivedAtExitUtc);
        Assert.Equal(30, result.Value.GracePeriodMinutes);
    }

    [Fact]
    public async Task Asks_for_the_price_in_force_at_entry_in_the_stored_vehicle_type_form()
    {
        var session = ParkingSession.Open(Guid.NewGuid(), LotId, "59X1-123.45", VehicleType.Motorbike, EntryTime, "qr", null, null);
        var prices = new FakePrices(PricingPolicy.Create(LotId, "Motorbike", 1, "PerEntry", null, 5000m, 0m));

        var result = await Handle(session, prices);

        Assert.Equal(EntryTime, prices.AskedAt);
        Assert.Equal("MOTORBIKE", prices.AskedVehicleType);
        Assert.Equal("UnregisteredWalkIn", result.Value!.CustomerType);
        Assert.Null(result.Value.AccountId);
        Assert.Equal("PerEntry", result.Value.Pricing.CalculationMethod);
        Assert.Equal(0, result.Value.Pricing.BlockMinutes);
    }

    [Fact]
    public async Task A_session_with_a_reservation_returns_its_booking()
    {
        var reservationId = Guid.NewGuid();
        var session = ParkingSession.Open(Guid.NewGuid(), LotId, "51A-123.45", VehicleType.Car, EntryTime, "qr", UserId, null, reservationId: reservationId);

        var result = await Handle(session, new FakePrices(PricingPolicy.Create(LotId, "Car", 1, "PerEntry", null, 5000m, 0m)));

        Assert.Equal(reservationId, result.Value!.BookingId);
        Assert.Equal("Booking", result.Value.CustomerType);
    }

    [Fact]
    public async Task Unknown_session_is_not_found()
    {
        var result = await new GetSessionFeeContextQueryHandler(new FakeSessions(null), new FakePrices(null))
            .Handle(new GetSessionFeeContextQuery(Guid.NewGuid()), TestContext.Current.CancellationToken);

        Assert.Equal(GetSessionFeeContextErrors.SessionNotFound, result.Error);
    }

    [Fact]
    public async Task No_price_list_at_entry_is_reported()
    {
        var session = ParkingSession.Open(Guid.NewGuid(), LotId, "51A-123.45", VehicleType.Car, EntryTime, "qr", null, null);

        var result = await Handle(session, new FakePrices(null));

        Assert.Equal(GetSessionFeeContextErrors.PricingNotFound, result.Error);
    }

    private static Task<ParkingSystem.BuildingBlocks.Results.Result<SessionFeeContext>> Handle(ParkingSession session, FakePrices prices) =>
        new GetSessionFeeContextQueryHandler(new FakeSessions(session), prices)
            .Handle(new GetSessionFeeContextQuery(session.Id), TestContext.Current.CancellationToken);

    private sealed class FakePrices(PricingPolicy? policy) : IPricingPolicyRepository
    {
        public DateTimeOffset? AskedAt { get; private set; }
        public string? AskedVehicleType { get; private set; }

        public Task<PricingPolicy?> GetEffectiveAtAsync(Guid lotId, string vehicleType, DateTimeOffset at, CancellationToken cancellationToken)
        {
            AskedAt = at;
            AskedVehicleType = vehicleType;
            return Task.FromResult(policy);
        }

        public Task<PricingPolicy?> GetLatestAsync(Guid lotId, string vehicleType, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task AddAsync(PricingPolicy added, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<bool> SaveChangesAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class FakeSessions(ParkingSession? session) : IParkingSessionRepository
    {
        public Task<ParkingSession?> FindByIdAsync(Guid sessionId, CancellationToken cancellationToken) =>
            Task.FromResult(session?.Id == sessionId ? session : null);

        public Task<ParkingSession?> FindByIdForUpdateAsync(Guid sessionId, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<bool> HasOpenSessionAsync(Guid lotId, string normalizedPlate, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<IReadOnlyList<ParkingSession>> ListOpenByUserAsync(Guid userId, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<ParkingSession?> FindOpenByQrTokenAsync(string qrToken, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<(IReadOnlyList<ParkingSession> Items, int TotalCount)> ListByLotAsync(
            Guid lotId, ParkingSessionStatus? status, int skip, int take, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<ParkingSession?> FindOpenByPlateAsync(Guid lotId, string normalizedPlate, CancellationToken cancellationToken) => throw new NotSupportedException();

        public void Add(ParkingSession added) => throw new NotSupportedException();

        public Task<bool> TrySaveChangesAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
