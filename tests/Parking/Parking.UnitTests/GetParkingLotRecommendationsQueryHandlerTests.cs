using Microsoft.Extensions.Logging.Abstractions;
using ParkingSystem.Parking.Application.ParkingLayout.HeldQuotaShortage;
using ParkingSystem.Parking.Application.ParkingLots;
using ParkingSystem.Parking.Application.ParkingLots.Availability;
using ParkingSystem.Parking.Application.ParkingLots.Recommendations;
using ParkingSystem.Parking.Application.ParkingLots.SearchCustomerLots;
using ParkingSystem.Parking.Domain.ParkingSessions;
using Xunit;

namespace ParkingSystem.Tests.Parking.UnitTests;

public sealed class GetParkingLotRecommendationsQueryHandlerTests
{
    private static readonly GetParkingLotRecommendationsQueryValidator Validator = new();

    [Fact]
    public void Validator_rejects_invalid_inputs()
    {
        Assert.False(Validator.Validate(new GetParkingLotRecommendationsQuery(91m, 106m)).IsValid);
        Assert.False(Validator.Validate(new GetParkingLotRecommendationsQuery(10m, 181m)).IsValid);
        Assert.False(Validator.Validate(new GetParkingLotRecommendationsQuery(10m, 106m, RadiusKm: 0)).IsValid);
        Assert.False(Validator.Validate(new GetParkingLotRecommendationsQuery(10m, 106m, RadiusKm: 51)).IsValid);
        Assert.False(Validator.Validate(new GetParkingLotRecommendationsQuery(10m, 106m, DurationMinutes: 10)).IsValid);
        Assert.False(Validator.Validate(new GetParkingLotRecommendationsQuery(10m, 106m, DurationMinutes: 1500)).IsValid);
        Assert.False(Validator.Validate(new GetParkingLotRecommendationsQuery(10m, 106m, TopK: 0)).IsValid);
        Assert.False(Validator.Validate(new GetParkingLotRecommendationsQuery(10m, 106m, VehicleType: "Airplane")).IsValid);

        Assert.True(Validator.Validate(new GetParkingLotRecommendationsQuery(10.77m, 106.70m)).IsValid);
        Assert.True(Validator.Validate(new GetParkingLotRecommendationsQuery(10.77m, 106.70m, VehicleType: "Motorbike")).IsValid);
    }

    [Fact]
    public async Task Handle_WhenNoNearbyLots_ReturnsEmptyResult()
    {
        var nearby = new FakeNearbyReader([]);
        var metrics = new FakeMetricsReader();
        var snapshots = new FakeSnapshotReader();
        var heldQuota = new FakeHeldQuotaProvider();
        var aiClient = new FakeAiClient();
        var handler = new GetParkingLotRecommendationsQueryHandler(
            nearby, metrics, snapshots, heldQuota, aiClient, NullLogger<GetParkingLotRecommendationsQueryHandler>.Instance);

        var query = new GetParkingLotRecommendationsQuery(10.77m, 106.70m);
        var result = await handler.Handle(query, TestContext.Current.CancellationToken);

        Assert.Empty(result.RecommendedLots);
        Assert.False(result.FallbackUsed);
        Assert.Null(result.FallbackReason);
    }

    [Fact]
    public async Task Handle_WhenAiServiceSucceeds_ReturnsAiRankedRecommendations()
    {
        var lot1Id = Guid.NewGuid();
        var lot2Id = Guid.NewGuid();

        var nearby = new FakeNearbyReader([
            new NearbyParkingLotItem(lot1Id, "Lot 1", "Address 1", 10.77m, 106.70m, "Standard", 1.2),
            new NearbyParkingLotItem(lot2Id, "Lot 2", "Address 2", 10.78m, 106.71m, "Standard", 2.5)
        ]);

        var metrics = new FakeMetricsReader(new Dictionary<Guid, CustomerLotMetrics>
        {
            [lot1Id] = new CustomerLotMetrics(10, 5, [new CustomerLotPrice("Car", 15000m, "Hourly")]),
            [lot2Id] = new CustomerLotMetrics(5, 2, [new CustomerLotPrice("Car", 10000m, "Hourly")])
        });

        var snapshots = new FakeSnapshotReader();
        var heldQuota = new FakeHeldQuotaProvider();
        var aiClient = new FakeAiClient
        {
            ResponseToReturn = new RecommendationClientResponse(
                RecommendedLots: [
                    new RecommendedLotItemDto(
                        lot1Id, "Lot 1", 1, 5.0, 92.5, 30000.0, false, ["Gần nhất", "Tỷ lệ còn chỗ 92%"],
                        SampleCount: 15, InsufficientData: false, BaselineSource: "lot_weekday_15m"),
                    new RecommendedLotItemDto(
                        lot2Id, "Lot 2", 2, 8.0, 78.0, 20000.0, false, ["Giá rẻ nhất"],
                        SampleCount: 2, InsufficientData: true, BaselineSource: "peer_daily_15m")
                ],
                FallbackUsed: false,
                FallbackReason: null)
        };

        var handler = new GetParkingLotRecommendationsQueryHandler(
            nearby, metrics, snapshots, heldQuota, aiClient, NullLogger<GetParkingLotRecommendationsQueryHandler>.Instance);

        var query = new GetParkingLotRecommendationsQuery(10.77m, 106.70m, "Car", DurationMinutes: 120);
        var result = await handler.Handle(query, TestContext.Current.CancellationToken);

        Assert.Equal(2, result.RecommendedLots.Count);
        Assert.False(result.FallbackUsed);

        var first = result.RecommendedLots[0];
        Assert.Equal(lot1Id, first.LotId);
        Assert.Equal(1, first.Rank);
        Assert.Equal(5.0, first.DriveTimeMinutes);
        Assert.Equal(92.5, first.ProbabilityPercent);
        Assert.Equal(30000m, first.Price);
        Assert.Equal(1.2, first.DistanceKm);
        Assert.Contains("Gần nhất", first.Reasons);
        Assert.Equal(15, first.SampleCount);
        Assert.False(first.InsufficientData);
        Assert.Equal("lot_weekday_15m", first.BaselineSource);

        var second = result.RecommendedLots[1];
        Assert.Equal(2, second.SampleCount);
        Assert.True(second.InsufficientData);
        Assert.Equal("peer_daily_15m", second.BaselineSource);
    }

    [Fact]
    public async Task Handle_WhenAiServiceFails_EngagesFailClosedFallbackWithNullProbability()
    {
        var lot1Id = Guid.NewGuid();
        var lot2Id = Guid.NewGuid();

        var nearby = new FakeNearbyReader([
            new NearbyParkingLotItem(lot1Id, "Lot Near", "123 Main St", 10.77m, 106.70m, "Standard", 0.8),
            new NearbyParkingLotItem(lot2Id, "Lot Far", "456 Far St", 10.80m, 106.75m, "Standard", 4.5)
        ]);

        var metrics = new FakeMetricsReader(new Dictionary<Guid, CustomerLotMetrics>
        {
            [lot1Id] = new CustomerLotMetrics(8, 0, [new CustomerLotPrice("Car", 10000m, "Hourly")]),
            [lot2Id] = new CustomerLotMetrics(20, 0, [new CustomerLotPrice("Car", 12000m, "Hourly")])
        });

        var snapshots = new FakeSnapshotReader();
        var heldQuota = new FakeHeldQuotaProvider();
        var aiClient = new FakeAiClient
        {
            ResponseToReturn = null // Simulates timeout / network error
        };

        var handler = new GetParkingLotRecommendationsQueryHandler(
            nearby, metrics, snapshots, heldQuota, aiClient, NullLogger<GetParkingLotRecommendationsQueryHandler>.Instance);

        var query = new GetParkingLotRecommendationsQuery(10.77m, 106.70m, "Car", DurationMinutes: 120);
        var result = await handler.Handle(query, TestContext.Current.CancellationToken);

        Assert.Equal(2, result.RecommendedLots.Count);
        Assert.True(result.FallbackUsed);
        Assert.Equal("ai_service_timeout_or_unavailable", result.FallbackReason);

        // Nearest lot should be Rank 1 in fallback
        var top = result.RecommendedLots[0];
        Assert.Equal(lot1Id, top.LotId);
        Assert.Equal(1, top.Rank);
        Assert.Equal(0.8, top.DistanceKm);
        Assert.Contains("Gần nhất", top.Reasons);
        Assert.True(top.DriveTimeMinutes > 0);
        Assert.Null(top.ProbabilityPercent); // Fallback MUST NOT invent probabilities (WP-TRUST-AI)
        Assert.True(top.InsufficientData);
        Assert.Equal(0, top.SampleCount);
        Assert.Equal("fallback", top.BaselineSource);
    }

    [Fact]
    public async Task Handle_PassesHeldQuotaAndHeldQuotaKnownToAiClient()
    {
        var lot1Id = Guid.NewGuid();
        var lot2Id = Guid.NewGuid();

        var nearby = new FakeNearbyReader([
            new NearbyParkingLotItem(lot1Id, "Lot 1", "Address 1", 10.77m, 106.70m, "Standard", 1.0),
            new NearbyParkingLotItem(lot2Id, "Lot 2", "Address 2", 10.78m, 106.71m, "Standard", 2.0)
        ]);

        var metrics = new FakeMetricsReader(new Dictionary<Guid, CustomerLotMetrics>
        {
            [lot1Id] = new CustomerLotMetrics(10, 5, [new CustomerLotPrice("Car", 15000m, "Hourly")]),
            [lot2Id] = new CustomerLotMetrics(5, 2, [new CustomerLotPrice("Car", 10000m, "Hourly")])
        });

        var snapshots = new FakeSnapshotReader();
        var heldQuota = new FakeHeldQuotaProvider(id => id == lot1Id ? 4 : null);
        var aiClient = new FakeAiClient();

        var handler = new GetParkingLotRecommendationsQueryHandler(
            nearby, metrics, snapshots, heldQuota, aiClient, NullLogger<GetParkingLotRecommendationsQueryHandler>.Instance);

        var query = new GetParkingLotRecommendationsQuery(10.77m, 106.70m, "Car", DurationMinutes: 60);
        await handler.Handle(query, TestContext.Current.CancellationToken);

        Assert.NotNull(aiClient.LastRequest);
        Assert.Equal(2, aiClient.LastRequest.CandidateLots.Count);

        var c1 = aiClient.LastRequest.CandidateLots.Single(x => x.LotId == lot1Id);
        Assert.Equal(4, c1.HeldQuota);
        Assert.True(c1.HeldQuotaKnown);

        var c2 = aiClient.LastRequest.CandidateLots.Single(x => x.LotId == lot2Id);
        Assert.Equal(0, c2.HeldQuota);
        Assert.False(c2.HeldQuotaKnown);
    }

    [Fact]
    public async Task Handle_WithMultipleCandidateLots_ExecutesSnapshotReaderSequentiallyWithoutConcurrency()
    {
        var lot1 = Guid.NewGuid();
        var lot2 = Guid.NewGuid();
        var lot3 = Guid.NewGuid();

        var nearby = new FakeNearbyReader([
            new NearbyParkingLotItem(lot1, "Lot 1", "Addr 1", 10.77m, 106.70m, "Standard", 1.0),
            new NearbyParkingLotItem(lot2, "Lot 2", "Addr 2", 10.78m, 106.71m, "Standard", 2.0),
            new NearbyParkingLotItem(lot3, "Lot 3", "Addr 3", 10.79m, 106.72m, "Standard", 3.0),
        ]);

        var metrics = new FakeMetricsReader();
        var heldQuota = new FakeHeldQuotaProvider();
        var aiClient = new FakeAiClient();
        var guardedReader = new ThreadSafeGuardedSnapshotReader();

        var handler = new GetParkingLotRecommendationsQueryHandler(
            nearby, metrics, guardedReader, heldQuota, aiClient, NullLogger<GetParkingLotRecommendationsQueryHandler>.Instance);

        var query = new GetParkingLotRecommendationsQuery(10.77m, 106.70m);
        await handler.Handle(query, TestContext.Current.CancellationToken);

        Assert.Equal(3, guardedReader.CallCount);
        Assert.Equal(0, guardedReader.ConcurrencyViolations);
    }

    private sealed class ThreadSafeGuardedSnapshotReader : IArrivalSnapshotReader
    {
        private int _inFlight;
        public int ConcurrencyViolations { get; private set; }
        public int CallCount { get; private set; }

        public async Task<IReadOnlyList<ArrivalAvailabilitySnapshot>> GetSessionProxyAsync(
            Guid lotId, string vehicleType, DateTimeOffset arrivalAt, CancellationToken cancellationToken)
        {
            var current = Interlocked.Increment(ref _inFlight);
            if (current > 1)
            {
                ConcurrencyViolations++;
            }
            CallCount++;
            // Small async delay to simulate EF Core database query
            await Task.Delay(10, cancellationToken);
            Interlocked.Decrement(ref _inFlight);
            return [];
        }
    }

    private sealed class FakeNearbyReader(IReadOnlyList<NearbyParkingLotItem> items) : INearbyParkingLotReader
    {
        public Task<IReadOnlyList<NearbyParkingLotItem>> SearchAsync(
            decimal latitude, decimal longitude, double radiusKm, int limit,
            CancellationToken cancellationToken, VehicleType? vehicleType = null) =>
            Task.FromResult(items);
    }

    private sealed class FakeMetricsReader(Dictionary<Guid, CustomerLotMetrics>? map = null) : ICustomerLotMetricsReader
    {
        public Task<IReadOnlyDictionary<Guid, CustomerLotMetrics>> ReadAsync(
            IReadOnlyCollection<Guid> lotIds, CancellationToken ct) =>
            Task.FromResult<IReadOnlyDictionary<Guid, CustomerLotMetrics>>(map ?? new Dictionary<Guid, CustomerLotMetrics>());
    }

    private sealed class FakeSnapshotReader : IArrivalSnapshotReader
    {
        public Task<IReadOnlyList<ArrivalAvailabilitySnapshot>> GetSessionProxyAsync(
            Guid lotId, string vehicleType, DateTimeOffset arrivalAt, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ArrivalAvailabilitySnapshot>>([]);
    }

    private sealed class FakeHeldQuotaProvider(Func<Guid, int?>? getHeld = null) : IHeldQuotaProvider
    {
        public Task<int?> GetHeldReservationsAsync(Guid lotId, CancellationToken cancellationToken) =>
            Task.FromResult(getHeld?.Invoke(lotId));
    }

    private sealed class FakeAiClient : IParkingRecommendationClient
    {
        public RecommendationClientResponse? ResponseToReturn { get; set; }
        public RecommendationClientRequest? LastRequest { get; private set; }

        public Task<RecommendationClientResponse?> GetRecommendationsAsync(
            RecommendationClientRequest request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(ResponseToReturn);
        }
    }
}
