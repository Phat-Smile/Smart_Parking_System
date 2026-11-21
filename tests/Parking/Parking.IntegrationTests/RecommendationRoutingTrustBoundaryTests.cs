using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using ParkingSystem.Parking.Application.ParkingLots;
using ParkingSystem.Parking.Application.ParkingLots.Availability;
using ParkingSystem.Parking.Application.ParkingLots.Recommendations;
using ParkingSystem.Parking.Application.ParkingLots.SearchCustomerLots;
using ParkingSystem.Parking.Domain.ParkingSessions;
using ParkingSystem.Parking.WebAPI.Clients;
using ParkingSystem.Parking.WebAPI.Routing;
using Xunit;

namespace ParkingSystem.Tests.Parking.IntegrationTests;

/// <summary>
/// R6 integration at the actual Parking application and HTTP client boundaries:
/// OSRM Table -> Parking -> Booking held quota -> AI HTTP -> Parking response.
/// External services are deterministic in-process stubs, not live business E2E.
/// </summary>
public sealed class RecommendationRoutingTrustBoundaryTests
{
    private static readonly Guid FirstId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid SecondId = Guid.Parse("00000000-0000-0000-0000-000000000002");

    [Theory]
    [InlineData("valid", false)]
    [InlineData("forged_eta", true)]
    [InlineData("invented_unroutable_eta", true)]
    [InlineData("incorrect_warning", true)]
    [InlineData("risky_top_one", true)]
    [InlineData("osrm_unavailable", false)]
    public async Task Routing_quota_and_ai_contract_are_validated_before_returning_to_client(
        string scenario, bool expectFallback)
    {
        var ct = TestContext.Current.CancellationToken;
        var arrivalAt = DateTimeOffset.UtcNow.AddHours(2).ToOffset(TimeSpan.FromHours(7));
        var aiCalls = new ConcurrentQueue<RecommendationClientRequest>();
        var bookingCalls = new ConcurrentQueue<Guid>();
        var bookingHeaders = new ConcurrentQueue<(string ArrivalKey, string ServiceKey)>();
        var aiHeaders = new ConcurrentQueue<string>();
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        await using var app = builder.Build();

        app.MapGet("/internal/booking/held-quota", (Guid lotId, DateTimeOffset arrivalAt, HttpContext context) =>
        {
            bookingCalls.Enqueue(lotId);
            bookingHeaders.Enqueue((context.Request.Headers["X-Arrival-Service-Key"].ToString(),
                context.Request.Headers["X-Service-Key"].ToString()));
            return Results.Ok(new { lotId, arrivalAt, heldQuota = 0 });
        });

        app.MapPost("/internal/ai/recommendations", async (HttpContext context) =>
        {
            var request = await context.Request.ReadFromJsonAsync<RecommendationClientRequest>(
                cancellationToken: context.RequestAborted);
            if (request is null) return Results.BadRequest();
            aiCalls.Enqueue(request);
            aiHeaders.Enqueue(context.Request.Headers["X-Arrival-Service-Key"].ToString());
            var first = request.CandidateLots.Single(x => x.LotId == FirstId);
            var second = request.CandidateLots.Single(x => x.LotId == SecondId);
            var riskyFirst = scenario == "risky_top_one";

            var result = new RecommendationClientResponse([
                new RecommendedLotItemDto(first.LotId, first.Name, 1,
                    scenario == "forged_eta" ? 9d : first.DriveTimeMinutes,
                    riskyFirst ? 20d : 70d, first.PriceForDuration,
                    riskyFirst || scenario == "incorrect_warning", ["Verified candidate"],
                    12, false, "lot_weekday_15m"),
                new RecommendedLotItemDto(second.LotId, second.Name, 2,
                    scenario == "invented_unroutable_eta" ? 9d : second.DriveTimeMinutes,
                    riskyFirst ? 70d : 20d, second.PriceForDuration,
                    !riskyFirst, ["Verified candidate"],
                    12, false, "lot_weekday_15m")
            ], false, null);
            return Results.Ok(result);
        });

        await app.StartAsync(ct);
        using var http = app.GetTestClient();
        var baseUrl = http.BaseAddress!.ToString();
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["BookingService:BaseUrl"] = baseUrl,
            ["AiService:BaseUrl"] = baseUrl,
            ["ArrivalIntegration:Key"] = "local-test-key",
            ["ServiceAuth:ServiceKey"] = "local-internal-key",
            ["BookingService:ArrivalQuotaTimeoutMs"] = "5000",
            ["Osrm:BaseUrl"] = "http://osrm.test"
        }).Build();

        var osrmHandler = new FakeOsrmHandler(scenario == "osrm_unavailable");
        using var osrmHttp = new HttpClient(osrmHandler);
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var routeReader = new OsrmMatrixClient(osrmHttp, cache, config);
        var quotaClient = new ArrivalHeldQuotaClient(http, config,
            NullLogger<ArrivalHeldQuotaClient>.Instance);
        var aiClient = new ParkingRecommendationClient(http, config,
            NullLogger<ParkingRecommendationClient>.Instance);
        var handler = new GetParkingLotRecommendationsQueryHandler(
            new NearbyReader(), new MetricsReader(), new SnapshotReader(),
            quotaClient, aiClient, routeReader,
            NullLogger<GetParkingLotRecommendationsQueryHandler>.Instance);

        var response = await handler.Handle(new GetParkingLotRecommendationsQuery(
            10.75m, 106.69m, "Car", arrivalAt, TopK: 10), ct);

        Assert.Equal(1, osrmHandler.Calls); // One Table call for both lots, even when OSRM fails.
        Assert.Equal(2, bookingCalls.Count);
        Assert.All(bookingHeaders, x =>
        {
            Assert.Equal("local-test-key", x.ArrivalKey);
            Assert.Equal("local-internal-key", x.ServiceKey);
        });
        Assert.Equal("local-test-key", Assert.Single(aiHeaders));
        Assert.Contains(FirstId, bookingCalls);
        Assert.Contains(SecondId, bookingCalls);
        var aiRequest = Assert.Single(aiCalls);
        Assert.Equal(arrivalAt, aiRequest.ArrivalAt);
        Assert.All(aiRequest.CandidateLots, x => Assert.True(x.HeldQuotaKnown));
        Assert.All(aiRequest.CandidateLots, x => Assert.Equal(0, x.HeldQuota));
        var firstInput = aiRequest.CandidateLots.Single(x => x.LotId == FirstId);
        var secondInput = aiRequest.CandidateLots.Single(x => x.LotId == SecondId);
        Assert.Equal(scenario == "osrm_unavailable" ? (double?)null : 5d, firstInput.DriveTimeMinutes);
        Assert.Null(secondInput.DriveTimeMinutes);

        Assert.Equal(expectFallback, response.FallbackUsed);
        Assert.Equal(expectFallback ? "ai_response_invalid" : null, response.FallbackReason);
        Assert.Equal(2, response.RecommendedLots.Count);
        Assert.Equal(new[] { 1, 2 }, response.RecommendedLots.Select(x => x.Rank).ToArray());
        Assert.Equal(scenario == "osrm_unavailable" ? (double?)null : 5d,
            response.RecommendedLots.Single(x => x.LotId == FirstId).DriveTimeMinutes);
        Assert.Null(response.RecommendedLots.Single(x => x.LotId == SecondId).DriveTimeMinutes);
        if (expectFallback)
        {
            Assert.All(response.RecommendedLots, x =>
            {
                Assert.Null(x.ProbabilityPercent);
                Assert.True(x.InsufficientData);
                Assert.Equal("fallback", x.BaselineSource);
            });
        }
        else
        {
            Assert.Equal(70d, response.RecommendedLots.Single(x => x.LotId == FirstId).ProbabilityPercent);
            Assert.Equal(20d, response.RecommendedLots.Single(x => x.LotId == SecondId).ProbabilityPercent);
            Assert.True(response.RecommendedLots.Single(x => x.LotId == SecondId).WarningHighFullRisk);
        }
    }

    private sealed class FakeOsrmHandler(bool unavailable) : HttpMessageHandler
    {
        private int _calls;
        public int Calls => Volatile.Read(ref _calls);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _calls);
            return Task.FromResult(unavailable
                ? new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
                : new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("""{"code":"Ok","durations":[[300,null]]}""",
                        Encoding.UTF8, "application/json")
                });
        }
    }

    private sealed class NearbyReader : INearbyParkingLotReader
    {
        public Task<IReadOnlyList<NearbyParkingLotItem>> SearchAsync(decimal latitude, decimal longitude,
            double radiusKm, int limit, CancellationToken cancellationToken, VehicleType? vehicleType = null) =>
            Task.FromResult<IReadOnlyList<NearbyParkingLotItem>>([
                new(FirstId, "Near", "Address 1", 10.77m, 106.70m, "Standard", 1.0),
                new(SecondId, "Far", "Address 2", 10.78m, 106.71m, "Standard", 2.0)
            ]);
    }

    private sealed class MetricsReader : ICustomerLotMetricsReader
    {
        public Task<IReadOnlyDictionary<Guid, CustomerLotMetrics>> ReadAsync(
            IReadOnlyCollection<Guid> lotIds, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyDictionary<Guid, CustomerLotMetrics>>(
                lotIds.ToDictionary(id => id,
                    _ => new CustomerLotMetrics(10, 10,
                        [new CustomerLotPrice("Car", 10000m, "PerEntry")])));
    }

    private sealed class SnapshotReader : IArrivalSnapshotReader
    {
        public Task<IReadOnlyList<ArrivalAvailabilitySnapshot>> GetSessionProxyAsync(
            Guid lotId, string vehicleType, DateTimeOffset arrivalAt, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ArrivalAvailabilitySnapshot>>([]);
    }
}
