using Microsoft.Extensions.Logging.Abstractions;
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
        var aiClient = new FakeAiClient();
        var handler = new GetParkingLotRecommendationsQueryHandler(
            nearby, metrics, snapshots, aiClient, NullLogger<GetParkingLotRecommendationsQueryHandler>.Instance);

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
        var aiClient = new FakeAiClient
        {
            ResponseToReturn = new RecommendationClientResponse(
                RecommendedLots: [
                    new RecommendedLotItemDto(
                        lot1Id, "Lot 1", 1, 5.0, 92.5, 30000.0, false, ["Gần nhất", "Tỷ lệ còn chỗ 92%"]),
                    new RecommendedLotItemDto(
                        lot2Id, "Lot 2", 2, 8.0, 78.0, 20000.0, false, ["Giá rẻ nhất"])
                ],
                FallbackUsed: false,
                FallbackReason: null)
        };

        var handler = new GetParkingLotRecommendationsQueryHandler(
            nearby, metrics, snapshots, aiClient, NullLogger<GetParkingLotRecommendationsQueryHandler>.Instance);

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
    }

    [Fact]
    public async Task Handle_WhenAiServiceFails_EngagesFailClosedFallback()
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
        var aiClient = new FakeAiClient
        {
            ResponseToReturn = null // Simulates timeout / network error
        };

        var handler = new GetParkingLotRecommendationsQueryHandler(
            nearby, metrics, snapshots, aiClient, NullLogger<GetParkingLotRecommendationsQueryHandler>.Instance);

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
        Assert.True(top.ProbabilityPercent > 0);
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

    private sealed class FakeAiClient : IParkingRecommendationClient
    {
        public RecommendationClientResponse? ResponseToReturn { get; set; }

        public Task<RecommendationClientResponse?> GetRecommendationsAsync(
            RecommendationClientRequest request, CancellationToken cancellationToken) =>
            Task.FromResult(ResponseToReturn);
    }
}
