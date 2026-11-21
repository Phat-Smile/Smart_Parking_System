using ParkingSystem.Parking.Application.Common.Interfaces;
using ParkingSystem.Parking.Application.ParkingSessions;
using ParkingSystem.Parking.Application.ParkingSessions.GetSessionFeeContext;
using ParkingSystem.Parking.Domain.Entities;
using ParkingSystem.Parking.Domain.ParkingSessions;
using Xunit;

namespace ParkingSystem.Tests.Parking.UnitTests;

public sealed class GetSessionFeeContextByQrTests
{
    private static readonly Guid LotId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly Guid UserId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly DateTimeOffset EntryTime = new(2026, 11, 20, 8, 0, 0, TimeSpan.Zero);
    private const string ValidQrToken = "qr-token-active-123456";

    [Fact]
    public async Task Returns_context_when_qr_token_is_valid_and_open_session_exists()
    {
        var session = ParkingSession.Open(Guid.NewGuid(), LotId, "51A-123.45", VehicleType.Car, EntryTime, ValidQrToken, UserId, null);
        var policy = PricingPolicy.Create(LotId, "Car", 1, "Block", 30, 15000m, 20000m);
        var prices = new FakePrices(policy);
        var sessions = new FakeSessions(session, isOpen: true);

        var handler = new GetSessionFeeContextByQrQueryHandler(sessions, prices);
        var result = await handler.Handle(new GetSessionFeeContextByQrQuery(ValidQrToken), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        var fee = result.Value!;
        Assert.Equal(session.Id, fee.SessionId);
        Assert.Equal(LotId, fee.LotId);
        Assert.Equal("51A-123.45", fee.PlateNumber);
        Assert.Equal("Car", fee.VehicleType);
        Assert.Equal("Active", fee.Status);
        Assert.Equal(EntryTime, fee.EntryTimeUtc);
        Assert.Equal("RegisteredWalkIn", fee.CustomerType);
        Assert.Equal(UserId, fee.AccountId);
        Assert.Equal(new SessionPricing(policy.Id, "Block", 30, 15000, 20000), fee.Pricing);
    }

    [Fact]
    public async Task Returns_qr_invalid_error_when_qr_token_is_empty_or_whitespace()
    {
        var handler = new GetSessionFeeContextByQrQueryHandler(new FakeSessions(null, isOpen: false), new FakePrices(null));

        var resultEmpty = await handler.Handle(new GetSessionFeeContextByQrQuery(""), TestContext.Current.CancellationToken);
        var resultWhitespace = await handler.Handle(new GetSessionFeeContextByQrQuery("   "), TestContext.Current.CancellationToken);

        Assert.True(resultEmpty.IsFailure);
        Assert.Equal(GetSessionFeeContextByQrErrors.InvalidQr, resultEmpty.Error);

        Assert.True(resultWhitespace.IsFailure);
        Assert.Equal(GetSessionFeeContextByQrErrors.InvalidQr, resultWhitespace.Error);
    }

    [Fact]
    public async Task Returns_session_not_found_when_session_does_not_exist()
    {
        var handler = new GetSessionFeeContextByQrQueryHandler(new FakeSessions(null, isOpen: false), new FakePrices(null));
        var result = await handler.Handle(new GetSessionFeeContextByQrQuery("non-existent-qr"), TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal(GetSessionFeeContextByQrErrors.SessionNotFound, result.Error);
    }

    [Fact]
    public async Task Returns_session_not_found_when_session_is_closed_or_cancelled()
    {
        // When a session is closed (Completed) or Cancelled, FindOpenByQrTokenAsync returns null (BR-11.e)
        var session = ParkingSession.Open(Guid.NewGuid(), LotId, "51A-123.45", VehicleType.Car, EntryTime, "closed-qr", UserId, null);
        session.MarkPaid(EntryTime.AddMinutes(30));
        session.Complete(EntryTime.AddMinutes(35));

        // FakeSessions returns null when isOpen is false (mimicking FindOpenByQrTokenAsync)
        var sessions = new FakeSessions(session, isOpen: false);
        var prices = new FakePrices(PricingPolicy.Create(LotId, "Car", 1, "Block", 30, 15000m, 0m));

        var handler = new GetSessionFeeContextByQrQueryHandler(sessions, prices);
        var result = await handler.Handle(new GetSessionFeeContextByQrQuery("closed-qr"), TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal(GetSessionFeeContextByQrErrors.SessionNotFound, result.Error);
    }

    [Fact]
    public async Task Returns_pricing_not_found_when_no_pricing_policy_matches_entry_time()
    {
        var session = ParkingSession.Open(Guid.NewGuid(), LotId, "51A-123.45", VehicleType.Car, EntryTime, ValidQrToken, UserId, null);
        var sessions = new FakeSessions(session, isOpen: true);
        var prices = new FakePrices(null);

        var handler = new GetSessionFeeContextByQrQueryHandler(sessions, prices);
        var result = await handler.Handle(new GetSessionFeeContextByQrQuery(ValidQrToken), TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal(GetSessionFeeContextByQrErrors.PricingNotFound, result.Error);
    }

    private sealed class FakePrices(PricingPolicy? policy) : IPricingPolicyRepository
    {
        public Task<PricingPolicy?> GetEffectiveAtAsync(Guid lotId, string vehicleType, DateTimeOffset at, CancellationToken cancellationToken) =>
            Task.FromResult(policy);

        public Task<PricingPolicy?> GetLatestAsync(Guid lotId, string vehicleType, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task AddAsync(PricingPolicy added, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<bool> SaveChangesAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class FakeSessions(ParkingSession? session, bool isOpen) : IParkingSessionRepository
    {
        public Task<ParkingSession?> FindOpenByQrTokenAsync(string qrToken, CancellationToken cancellationToken) =>
            Task.FromResult(isOpen && session?.QrToken == qrToken ? session : null);

        public Task<ParkingSession?> FindByIdAsync(Guid sessionId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ParkingSession?> FindByIdForUpdateAsync(Guid sessionId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<bool> HasOpenSessionAsync(Guid lotId, string normalizedPlate, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<ParkingSession>> ListOpenByUserAsync(Guid userId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<(IReadOnlyList<ParkingSession> Items, int TotalCount)> ListByLotAsync(
            Guid lotId, ParkingSessionStatus? status, int skip, int take, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ParkingSession?> FindOpenByPlateAsync(Guid lotId, string normalizedPlate, CancellationToken cancellationToken) => throw new NotSupportedException();
        public void Add(ParkingSession added) => throw new NotSupportedException();
        public Task<bool> TrySaveChangesAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
