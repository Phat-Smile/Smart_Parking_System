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
        Assert.False(Validator.Validate(new GetParkingLotRecommendationsQuery(10m, 106m, Priority: "unknown")).IsValid);
        Assert.True(Validator.Validate(new GetParkingLotRecommendationsQuery(10m, 106m, Priority: "cheapest")).IsValid);

        Assert.True(Validator.Validate(new GetParkingLotRecommendationsQuery(10.77m, 106.70m)).IsValid);
        Assert.True(Validator.Validate(new GetParkingLotRecommendationsQuery(10.77m, 106.70m, VehicleType: "Motorbike")).IsValid);
    }

    [Fact]
    public async Task Handle_WhenNoNearbyLots_ReturnsEmptyResult()
    {
        var nearby = new FakeNearbyReader([]);
        var metrics = new FakeMetricsReader();
        var snapshots = new FakeSnapshotReader();
        var heldQuota = new FakeArrivalHeldQuotaClient();
        var aiClient = new FakeAiClient();
        var handler = new GetParkingLotRecommendationsQueryHandler(
            nearby, metrics, snapshots, heldQuota, aiClient, new FakeDrivingEtaReader(), NullLogger<GetParkingLotRecommendationsQueryHandler>.Instance);

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
            [lot1Id] = new CustomerLotMetrics(10, 5, [new CustomerLotPrice("Car", 15000m, "Block", 60)]),
            [lot2Id] = new CustomerLotMetrics(5, 2, [new CustomerLotPrice("Car", 10000m, "Block", 60)])
        });

        var snapshots = new FakeSnapshotReader();
        var heldQuota = new FakeArrivalHeldQuotaClient(_ => 0);
        var aiClient = new FakeAiClient
        {
            ResponseToReturn = new RecommendationClientResponse(
                RecommendedLots: [
                    new RecommendedLotItemDto(
                        lot1Id, "Lot 1", 1, 5.0, 92.5, 30000.0, false, ["Gần nhất", "Tỷ lệ còn chỗ 92%"],
                        SampleCount: 15, InsufficientData: false, BaselineSource: "lot_weekday_15m"),
                    new RecommendedLotItemDto(
                        lot2Id, "Lot 2", 2, 8.0, null, 20000.0, false, ["Giá rẻ nhất"],
                        SampleCount: 2, InsufficientData: true, BaselineSource: "peer_daily_15m")
                ],
                FallbackUsed: false,
                FallbackReason: null)
        };

        var handler = new GetParkingLotRecommendationsQueryHandler(
            nearby, metrics, snapshots, heldQuota, aiClient,
            new FakeDrivingEtaReader(new Dictionary<Guid, double?> { [lot1Id] = 5.0, [lot2Id] = 8.0 }),
            NullLogger<GetParkingLotRecommendationsQueryHandler>.Instance);

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
        Assert.Null(second.ProbabilityPercent);
        Assert.True(second.InsufficientData);
        Assert.Equal("peer_daily_15m", second.BaselineSource);
    }

    [Theory]
    [InlineData(false, "ai_service_timeout_or_unavailable")]
    [InlineData(true, "ai_recommendation_unavailable")]
    public async Task Handle_WhenAiServiceFails_EngagesFailClosedFallbackWithNullProbability(
        bool explicitAiFallback, string expectedReason)
    {
        var lot1Id = Guid.NewGuid();
        var lot2Id = Guid.NewGuid();

        var nearby = new FakeNearbyReader([
            new NearbyParkingLotItem(lot1Id, "Lot Near", "123 Main St", 10.77m, 106.70m, "Standard", 0.8),
            new NearbyParkingLotItem(lot2Id, "Lot Far", "456 Far St", 10.80m, 106.75m, "Standard", 4.5)
        ]);

        var metrics = new FakeMetricsReader(new Dictionary<Guid, CustomerLotMetrics>
        {
            [lot1Id] = new CustomerLotMetrics(8, 0, [new CustomerLotPrice("Car", 10000m, "Block", 60)]),
            [lot2Id] = new CustomerLotMetrics(20, 0, [new CustomerLotPrice("Car", 12000m, "Block", 60)])
        });

        var snapshots = new FakeSnapshotReader();
        var heldQuota = new FakeArrivalHeldQuotaClient();
        var aiClient = new FakeAiClient
        {
            ResponseToReturn = explicitAiFallback
                ? new RecommendationClientResponse([], true, "ai_recommendation_unavailable")
                : null // Simulates timeout / network error
        };

        var handler = new GetParkingLotRecommendationsQueryHandler(
            nearby, metrics, snapshots, heldQuota, aiClient, new FakeDrivingEtaReader(), NullLogger<GetParkingLotRecommendationsQueryHandler>.Instance);

        var query = new GetParkingLotRecommendationsQuery(10.77m, 106.70m, "Car", DurationMinutes: 120);
        var result = await handler.Handle(query, TestContext.Current.CancellationToken);

        Assert.Equal(2, result.RecommendedLots.Count);
        Assert.True(result.FallbackUsed);
        Assert.Equal(expectedReason, result.FallbackReason);

        // Nearest lot should be Rank 1 in fallback
        var top = result.RecommendedLots[0];
        Assert.Equal(lot1Id, top.LotId);
        Assert.Equal(1, top.Rank);
        Assert.Equal(0.8, top.DistanceKm);
        Assert.Contains("Gần nhất theo khoảng cách", top.Reasons);
        Assert.Null(top.DriveTimeMinutes); // Distance is known, but no OSRM travel time is available.
        Assert.Null(top.ProbabilityPercent); // Fallback MUST NOT invent probabilities (WP-TRUST-AI)
        Assert.True(top.InsufficientData);
        Assert.Equal(0, top.SampleCount);
        Assert.Equal("fallback", top.BaselineSource);
    }

    [Fact]
    public async Task Handle_WhenAiIsDownAndTopKIsTen_ReturnsNoMoreThanThreeLotsWithoutFakeEta()
    {
        var ids = Enumerable.Range(0, 5).Select(_ => Guid.NewGuid()).ToArray();
        var nearby = new FakeNearbyReader(ids.Select((id, index) =>
            new NearbyParkingLotItem(id, $"Lot {index}", "Address", 10.77m, 106.70m,
                "Standard", index + 0.5)).ToList());
        var metrics = new FakeMetricsReader(ids.ToDictionary(id => id,
            _ => new CustomerLotMetrics(10, 5, [new CustomerLotPrice("Car", 10000m, "PerEntry")])));
        var handler = new GetParkingLotRecommendationsQueryHandler(
            nearby, metrics, new FakeSnapshotReader(), new FakeArrivalHeldQuotaClient(),
            new FakeAiClient(), new FakeDrivingEtaReader(), NullLogger<GetParkingLotRecommendationsQueryHandler>.Instance);

        var result = await handler.Handle(new GetParkingLotRecommendationsQuery(
            10.77m, 106.70m, TopK: 10), TestContext.Current.CancellationToken);

        Assert.True(result.FallbackUsed);
        Assert.Equal(3, result.RecommendedLots.Count);
        Assert.All(result.RecommendedLots, lot => Assert.Null(lot.DriveTimeMinutes));
        Assert.Equal(new[] { 1, 2, 3 }, result.RecommendedLots.Select(lot => lot.Rank).ToArray());
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
            [lot1Id] = new CustomerLotMetrics(10, 5, [new CustomerLotPrice("Car", 15000m, "Block", 60)]),
            [lot2Id] = new CustomerLotMetrics(5, 2, [new CustomerLotPrice("Car", 10000m, "Block", 60)])
        });

        var snapshots = new FakeSnapshotReader();
        var heldQuota = new FakeArrivalHeldQuotaClient(id => id == lot1Id ? 4 : null);
        var aiClient = new FakeAiClient();

        var handler = new GetParkingLotRecommendationsQueryHandler(
            nearby, metrics, snapshots, heldQuota, aiClient, new FakeDrivingEtaReader(), NullLogger<GetParkingLotRecommendationsQueryHandler>.Instance);

        var arrival = DateTimeOffset.UtcNow.AddHours(3);
        var query = new GetParkingLotRecommendationsQuery(10.77m, 106.70m, "Car", ArrivalAt: arrival, DurationMinutes: 60,
            Priority: "cheapest");
        await handler.Handle(query, TestContext.Current.CancellationToken);

        Assert.NotNull(aiClient.LastRequest);
        Assert.Equal("cheapest", aiClient.LastRequest.Priority);
        Assert.Equal(arrival, aiClient.LastRequest.ArrivalAt);
        Assert.Equal(2, heldQuota.Calls.Count);
        Assert.All(heldQuota.Calls, call => Assert.Equal(arrival, call.ArrivalAt));
        Assert.Contains(heldQuota.Calls, call => call.LotId == lot1Id);
        Assert.Contains(heldQuota.Calls, call => call.LotId == lot2Id);
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

        var metrics = new FakeMetricsReader(new Dictionary<Guid, CustomerLotMetrics>
        {
            [lot1] = new CustomerLotMetrics(1, 0, [new CustomerLotPrice("Car", 10000m, "Block", 60)]),
            [lot2] = new CustomerLotMetrics(1, 0, [new CustomerLotPrice("Car", 10000m, "Block", 60)]),
            [lot3] = new CustomerLotMetrics(1, 0, [new CustomerLotPrice("Car", 10000m, "Block", 60)])
        });
        var heldQuota = new FakeArrivalHeldQuotaClient();
        var aiClient = new FakeAiClient();
        var guardedReader = new ThreadSafeGuardedSnapshotReader();

        var handler = new GetParkingLotRecommendationsQueryHandler(
            nearby, metrics, guardedReader, heldQuota, aiClient, new FakeDrivingEtaReader(), NullLogger<GetParkingLotRecommendationsQueryHandler>.Instance);

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

    private sealed class FakeArrivalHeldQuotaClient(Func<Guid, int?>? getHeld = null) : IArrivalHeldQuotaClient
    {
        public System.Collections.Concurrent.ConcurrentQueue<(Guid LotId, DateTimeOffset ArrivalAt)> Calls { get; } = new();
        public Task<int?> GetHeldQuotaAsync(Guid lotId, DateTimeOffset arrivalAt, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Calls.Enqueue((lotId, arrivalAt));
            return Task.FromResult(getHeld?.Invoke(lotId));
        }
    }

    [Fact]
    public async Task Handle_Uses_actual_per_entry_and_block_pricing_and_skips_missing_price()
    {
        var perEntry = Guid.NewGuid();
        var block = Guid.NewGuid();
        var unpriced = Guid.NewGuid();
        var nearby = new FakeNearbyReader([
            new NearbyParkingLotItem(perEntry, "PerEntry", "Address", 10.77m, 106.70m, "Standard", 1.0),
            new NearbyParkingLotItem(block, "Block", "Address", 10.78m, 106.71m, "Standard", 2.0),
            new NearbyParkingLotItem(unpriced, "Missing price", "Address", 10.79m, 106.72m, "Standard", 3.0)
        ]);
        var metrics = new FakeMetricsReader(new Dictionary<Guid, CustomerLotMetrics>
        {
            [perEntry] = new CustomerLotMetrics(10, 0, [new CustomerLotPrice("Car", 15000m, "PerEntry")]),
            [block] = new CustomerLotMetrics(10, 0, [new CustomerLotPrice("Car", 4000m, "Block", 30)]),
            [unpriced] = new CustomerLotMetrics(10, 0, [])
        });
        var client = new FakeAiClient();
        var handler = new GetParkingLotRecommendationsQueryHandler(
            nearby, metrics, new FakeSnapshotReader(), new FakeArrivalHeldQuotaClient(), client,
            new FakeDrivingEtaReader(), NullLogger<GetParkingLotRecommendationsQueryHandler>.Instance);

        var result = await handler.Handle(
            new GetParkingLotRecommendationsQuery(10.77m, 106.70m, DurationMinutes: 95),
            TestContext.Current.CancellationToken);

        Assert.NotNull(client.LastRequest);
        Assert.Equal(2, client.LastRequest.CandidateLots.Count);
        Assert.Equal(15000d, client.LastRequest.CandidateLots.Single(x => x.LotId == perEntry).PriceForDuration);
        Assert.Equal(16000d, client.LastRequest.CandidateLots.Single(x => x.LotId == block).PriceForDuration);
        Assert.DoesNotContain(client.LastRequest.CandidateLots, x => x.LotId == unpriced);
        Assert.True(result.FallbackUsed);
        Assert.Equal(2, result.RecommendedLots.Count);
        Assert.DoesNotContain(result.RecommendedLots, x => x.LotId == unpriced);
    }

    [Fact]
    public async Task Handle_WithNoKnownPrice_DoesNotInventCostOrInvokeAi()
    {
        var lotId = Guid.NewGuid();
        var nearby = new FakeNearbyReader([
            new NearbyParkingLotItem(lotId, "No pricing", "Address", 10.77m, 106.70m, "Standard", 1.0)
        ]);
        var handlerClient = new FakeAiClient();
        var handler = new GetParkingLotRecommendationsQueryHandler(
            nearby, new FakeMetricsReader(), new FakeSnapshotReader(), new FakeArrivalHeldQuotaClient(), handlerClient,
            new FakeDrivingEtaReader(), NullLogger<GetParkingLotRecommendationsQueryHandler>.Instance);

        var result = await handler.Handle(
            new GetParkingLotRecommendationsQuery(10.77m, 106.70m), TestContext.Current.CancellationToken);

        Assert.Null(handlerClient.LastRequest);
        Assert.Empty(result.RecommendedLots);
        Assert.True(result.FallbackUsed);
        Assert.Equal("no_applicable_pricing_policy", result.FallbackReason);
    }

    [Theory]
    [InlineData("unknown_id")]
    [InlineData("wrong_rank")]
    [InlineData("wrong_price")]
    [InlineData("untrusted_probability")]
    [InlineData("invalid_time")]
    [InlineData("contradictory_fallback")]
    [InlineData("unexpected_fallback_reason")]
    [InlineData("empty_success")]
    public async Task Handle_WhenAiResponseIsUntrusted_ReturnsLabelledNonAiFallback(string problem)
    {
        var lotId = Guid.NewGuid();
        var nearby = new FakeNearbyReader([
            new NearbyParkingLotItem(lotId, "Lot Test", "Address", 10.77m, 106.70m, "Standard", 0.9)
        ]);
        var metrics = new FakeMetricsReader(new Dictionary<Guid, CustomerLotMetrics>
        {
            [lotId] = new CustomerLotMetrics(10, 0, [new CustomerLotPrice("Car", 10000m, "Block", 60)])
        });
        var row = new RecommendedLotItemDto(
            lotId, "Lot Test", 1, 3.0, null, 20000.0, false, ["Dữ liệu chưa đủ"],
            SampleCount: 0, InsufficientData: true, BaselineSource: "insufficient");
        row = problem switch
        {
            "unknown_id" => row with { LotId = Guid.NewGuid() },
            "wrong_rank" => row with { Rank = 2 },
            "wrong_price" => row with { Price = 123456.0 },
            "untrusted_probability" => row with { ProbabilityPercent = 85.0 },
            "invalid_time" => row with { DriveTimeMinutes = double.NaN },
            "contradictory_fallback" or "unexpected_fallback_reason" or "empty_success" => row,
            _ => throw new InvalidOperationException("Unexpected case")
        };
        var client = new FakeAiClient
        {
            ResponseToReturn = new RecommendationClientResponse(
                problem == "empty_success" ? [] : [row],
                problem == "contradictory_fallback",
                problem == "unexpected_fallback_reason" ? "unexpected_reason" : null)
        };
        var handler = new GetParkingLotRecommendationsQueryHandler(
            nearby, metrics, new FakeSnapshotReader(), new FakeArrivalHeldQuotaClient(_ => 0), client,
            new FakeDrivingEtaReader(), NullLogger<GetParkingLotRecommendationsQueryHandler>.Instance);

        var result = await handler.Handle(
            new GetParkingLotRecommendationsQuery(10.77m, 106.70m), TestContext.Current.CancellationToken);

        Assert.True(result.FallbackUsed);
        Assert.Equal("ai_response_invalid", result.FallbackReason);
        Assert.Single(result.RecommendedLots);
        Assert.Equal(lotId, result.RecommendedLots[0].LotId);
        Assert.Null(result.RecommendedLots[0].ProbabilityPercent);
    }

    [Fact]
    public async Task Handle_Preserves_model_chosen_order_instead_of_reordering_by_distance()
    {
        var near = Guid.NewGuid();
        var far = Guid.NewGuid();
        var nearby = new FakeNearbyReader([
            new NearbyParkingLotItem(near, "Near", "Address near", 10.77m, 106.70m, "Standard", 0.8),
            new NearbyParkingLotItem(far, "Far", "Address far", 10.79m, 106.72m, "Standard", 3.2)
        ]);
        var metrics = new FakeMetricsReader(new Dictionary<Guid, CustomerLotMetrics>
        {
            [near] = new CustomerLotMetrics(10, 5, [new CustomerLotPrice("Car", 10000m, "Block", 60)]),
            [far] = new CustomerLotMetrics(10, 5, [new CustomerLotPrice("Car", 15000m, "Block", 60)])
        });
        var client = new FakeAiClient
        {
            ResponseToReturn = new RecommendationClientResponse([
                new RecommendedLotItemDto(far, "Far", 1, 9, null, 30000, false, ["Model chose far"],
                    0, true, "insufficient"),
                new RecommendedLotItemDto(near, "Near", 2, 3, null, 20000, false, ["Model chose near"],
                    0, true, "insufficient")
            ], false, null)
        };
        var handler = new GetParkingLotRecommendationsQueryHandler(
            nearby, metrics, new FakeSnapshotReader(), new FakeArrivalHeldQuotaClient(), client,
            new FakeDrivingEtaReader(new Dictionary<Guid, double?> { [far] = 9.0, [near] = 3.0 }), NullLogger<GetParkingLotRecommendationsQueryHandler>.Instance);

        var result = await handler.Handle(
            new GetParkingLotRecommendationsQuery(10.77m, 106.70m), TestContext.Current.CancellationToken);

        Assert.False(result.FallbackUsed);
        Assert.Equal(new[] { far, near }, result.RecommendedLots.Select(x => x.LotId).ToArray());
    }

    [Theory]
    [InlineData("Car", 0, true, 1)]
    [InlineData("Car", -1, false, 1)]
    [InlineData("Motorbike", 7, true, 0)]
    public async Task Handle_Distinguishes_known_zero_invalid_quota_and_motorbike_policy(
        string vehicleType, int quota, bool known, int expectedCalls)
    {
        var lotId = Guid.NewGuid();
        var nearby = new FakeNearbyReader([
            new NearbyParkingLotItem(lotId, "Lot", "Address", 10.77m, 106.70m, "Standard", 1.0)
        ]);
        var metrics = new FakeMetricsReader(new Dictionary<Guid, CustomerLotMetrics>
        {
            [lotId] = new CustomerLotMetrics(5, 5, [new CustomerLotPrice(vehicleType, 10000m, "PerEntry")])
        });
        var quotaClient = new FakeArrivalHeldQuotaClient(_ => quota);
        var ai = new FakeAiClient();
        var handler = new GetParkingLotRecommendationsQueryHandler(nearby, metrics,
            new FakeSnapshotReader(), quotaClient, ai, new FakeDrivingEtaReader(), NullLogger<GetParkingLotRecommendationsQueryHandler>.Instance);
        var firstArrival = DateTimeOffset.UtcNow.AddHours(1);
        var secondArrival = firstArrival.AddHours(1);
        await handler.Handle(new GetParkingLotRecommendationsQuery(10.77m, 106.70m, vehicleType, firstArrival),
            TestContext.Current.CancellationToken);
        await handler.Handle(new GetParkingLotRecommendationsQuery(10.77m, 106.70m, vehicleType, secondArrival),
            TestContext.Current.CancellationToken);

        Assert.NotNull(ai.LastRequest);
        var candidate = Assert.Single(ai.LastRequest.CandidateLots);
        Assert.Equal(0, candidate.HeldQuota);
        Assert.Equal(known, candidate.HeldQuotaKnown);
        Assert.Equal(expectedCalls * 2, quotaClient.Calls.Count);
        if (expectedCalls > 0)
            Assert.Equal(new[] { firstArrival, secondArrival }, quotaClient.Calls.Select(x => x.ArrivalAt).ToArray());
    }

    [Fact]
    public async Task Handle_Rejects_probability_when_booking_quota_is_unknown()
    {
        var lotId = Guid.NewGuid();
        var nearby = new FakeNearbyReader([
            new NearbyParkingLotItem(lotId, "Lot", "Address", 10.77m, 106.70m, "Standard", 1.0)
        ]);
        var metrics = new FakeMetricsReader(new Dictionary<Guid, CustomerLotMetrics>
        {
            [lotId] = new CustomerLotMetrics(5, 0, [new CustomerLotPrice("Car", 10000m, "PerEntry")])
        });
        var ai = new FakeAiClient { ResponseToReturn = new RecommendationClientResponse([
            new RecommendedLotItemDto(lotId, "Lot", 1, 3, 90, 10000, false, ["High probability"], 10, false)
        ], false, null) };
        var handler = new GetParkingLotRecommendationsQueryHandler(nearby, metrics,
            new FakeSnapshotReader(), new FakeArrivalHeldQuotaClient(), ai,
            new FakeDrivingEtaReader(), NullLogger<GetParkingLotRecommendationsQueryHandler>.Instance);
        var result = await handler.Handle(new GetParkingLotRecommendationsQuery(10.77m, 106.70m),
            TestContext.Current.CancellationToken);

        Assert.True(result.FallbackUsed);
        Assert.Equal("ai_response_invalid", result.FallbackReason);
        Assert.Null(Assert.Single(result.RecommendedLots).ProbabilityPercent);
    }

    [Fact]
    public async Task Handle_Flows_Real_Osrm_Durations_Into_AI_And_Back_To_Client()
    {
        var near = Guid.NewGuid();
        var far = Guid.NewGuid();
        var nearby = new FakeNearbyReader([
            new NearbyParkingLotItem(near, "Near", "Address", 10.77m, 106.70m, "Standard", 0.2),
            new NearbyParkingLotItem(far, "Far", "Address", 10.78m, 106.71m, "Standard", 1.7)
        ]);
        var metrics = new FakeMetricsReader(new Dictionary<Guid, CustomerLotMetrics>
        {
            [near] = new CustomerLotMetrics(7, 0, [new CustomerLotPrice("Car", 10000m, "PerEntry")]),
            [far] = new CustomerLotMetrics(7, 0, [new CustomerLotPrice("Car", 10000m, "PerEntry")])
        });
        var routes = new FakeDrivingEtaReader(new Dictionary<Guid, double?>
        {
            [near] = 13.5, [far] = 4.2
        });
        var ai = new FakeAiClient
        {
            ResponseToReturn = new RecommendationClientResponse([
                new RecommendedLotItemDto(far, "Far", 1, 4.2, null, 10000, false, ["OSRM"], 0, true),
                new RecommendedLotItemDto(near, "Near", 2, 13.5, null, 10000, false, ["OSRM"], 0, true)
            ], false, null)
        };
        var handler = new GetParkingLotRecommendationsQueryHandler(nearby, metrics,
            new FakeSnapshotReader(), new FakeArrivalHeldQuotaClient(_ => 0), ai, routes,
            NullLogger<GetParkingLotRecommendationsQueryHandler>.Instance);

        var response = await handler.Handle(new GetParkingLotRecommendationsQuery(10.77m, 106.7m),
            TestContext.Current.CancellationToken);

        Assert.False(response.FallbackUsed);
        Assert.Equal(new[] {far, near}, response.RecommendedLots.Select(x => x.LotId).ToArray());
        Assert.Equal(4.2, response.RecommendedLots[0].DriveTimeMinutes);
        Assert.Equal(13.5, response.RecommendedLots[1].DriveTimeMinutes);
        Assert.Equal(1, routes.Calls);
        Assert.Equal(2, routes.LastDestinations!.Count);
        Assert.NotNull(ai.LastRequest);
        Assert.Equal(13.5, ai.LastRequest.CandidateLots.Single(x => x.LotId == near).DriveTimeMinutes);
        Assert.Equal(4.2, ai.LastRequest.CandidateLots.Single(x => x.LotId == far).DriveTimeMinutes);
    }

    [Fact]
    public async Task Handle_Rejects_AI_Fabricated_Travel_Time_And_Uses_Route_If_AI_Unavailable()
    {
        var id = Guid.NewGuid();
        var nearby = new FakeNearbyReader([
            new NearbyParkingLotItem(id, "A", "Address", 10.77m, 106.70m, "Standard", 0.5)
        ]);
        var metrics = new FakeMetricsReader(new Dictionary<Guid, CustomerLotMetrics>
        {
            [id] = new CustomerLotMetrics(7, 0, [new CustomerLotPrice("Car", 10000m, "PerEntry")])
        });
        var ai = new FakeAiClient { ResponseToReturn = new RecommendationClientResponse([
            new RecommendedLotItemDto(id, "A", 1, 99, null, 10000, false, ["False time"], 0, true)
        ], false, null) };
        var routes = new FakeDrivingEtaReader(new Dictionary<Guid, double?> { [id] = 4.2 });
        var handler = new GetParkingLotRecommendationsQueryHandler(nearby, metrics,
            new FakeSnapshotReader(), new FakeArrivalHeldQuotaClient(_ => 0), ai, routes,
            NullLogger<GetParkingLotRecommendationsQueryHandler>.Instance);

        var response = await handler.Handle(new GetParkingLotRecommendationsQuery(10.77m, 106.70m),
            TestContext.Current.CancellationToken);
        Assert.True(response.FallbackUsed);
        Assert.Equal("ai_response_invalid", response.FallbackReason);
        Assert.Equal(4.2, Assert.Single(response.RecommendedLots).DriveTimeMinutes);
        Assert.Equal(1, routes.Calls);
    }

    [Fact]
    public async Task Handle_When_Osrm_Is_Unavailable_Never_Reports_Invented_Eta()
    {
        var id = Guid.NewGuid();
        var nearby = new FakeNearbyReader([
            new NearbyParkingLotItem(id, "A", "Address", 10.77m, 106.70m, "Standard", 0.5)
        ]);
        var metrics = new FakeMetricsReader(new Dictionary<Guid, CustomerLotMetrics>
        {
            [id] = new CustomerLotMetrics(7, 0, [new CustomerLotPrice("Car", 10000m, "PerEntry")])
        });
        var handler = new GetParkingLotRecommendationsQueryHandler(nearby, metrics,
            new FakeSnapshotReader(), new FakeArrivalHeldQuotaClient(_ => 0), new FakeAiClient(),
            new FakeDrivingEtaReader(), NullLogger<GetParkingLotRecommendationsQueryHandler>.Instance);
        var response = await handler.Handle(new GetParkingLotRecommendationsQuery(10.77m, 106.70m),
            TestContext.Current.CancellationToken);
        Assert.Null(Assert.Single(response.RecommendedLots).DriveTimeMinutes);
        Assert.True(response.FallbackUsed);
    }

    private sealed class FakeDrivingEtaReader(
        IReadOnlyDictionary<Guid, double?>? known = null) : IRecommendationDrivingEtaReader
    {
        public int Calls { get; private set; }
        public IReadOnlyList<RecommendationRouteDestination>? LastDestinations { get; private set; }

        public Task<IReadOnlyDictionary<Guid, double?>> GetDrivingMinutesAsync(
            decimal originLatitude, decimal originLongitude,
            IReadOnlyList<RecommendationRouteDestination> destinations,
            string vehicleGroup, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Calls++;
            LastDestinations = destinations;
            return Task.FromResult(known ?? (IReadOnlyDictionary<Guid, double?>)new Dictionary<Guid, double?>());
        }
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
