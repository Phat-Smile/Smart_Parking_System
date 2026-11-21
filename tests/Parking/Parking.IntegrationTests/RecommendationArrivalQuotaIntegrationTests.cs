using System.Collections.Concurrent;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using ParkingSystem.Parking.Application.ParkingLots;
using ParkingSystem.Parking.Application.ParkingLots.Availability;
using ParkingSystem.Parking.Application.ParkingLots.Recommendations;
using ParkingSystem.Parking.Application.ParkingLots.SearchCustomerLots;
using ParkingSystem.Parking.Domain.ParkingSessions;
using ParkingSystem.Parking.WebAPI.Clients;
using Xunit;

namespace ParkingSystem.Tests.Parking.IntegrationTests;

/// <summary>Real Parking handler and HTTP clients against in-process stubs for Booking and AI.
/// No external network, database or LLM. Booking's real database semantics are covered separately
/// by Booking.IntegrationTests.ArrivalHeldQuotaIntegrationTests.</summary>
public sealed class RecommendationArrivalQuotaIntegrationTests
{
    [Theory]
    [InlineData("Car", false)]
    [InlineData("Car", true)]
    [InlineData("Motorbike", false)]
    public async Task Quota_flows_from_booking_to_ai_for_the_requested_arrival(string vehicleType, bool failSecond)
    {
        var ct = TestContext.Current.CancellationToken;
        var firstId = Guid.NewGuid();
        var secondId = Guid.NewGuid();
        var arrival = DateTimeOffset.UtcNow.AddHours(2).ToOffset(TimeSpan.FromHours(7));
        var bookingCalls = new ConcurrentQueue<(Guid LotId, DateTimeOffset ArrivalAt)>();
        var aiCalls = new ConcurrentQueue<RecommendationClientRequest>();
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        await using var app = builder.Build();
        app.MapGet("/internal/booking/held-quota", (Guid lotId, DateTimeOffset arrivalAt, HttpContext context) =>
        {
            Assert.Equal("arrival-test-key", context.Request.Headers["X-Arrival-Service-Key"].ToString());
            Assert.Equal("global-test-key", context.Request.Headers["X-Service-Key"].ToString());
            bookingCalls.Enqueue((lotId, arrivalAt));
            if (failSecond && lotId == secondId)
                return (IResult)Results.StatusCode(503);
            return Results.Ok(new { lotId, arrivalAt, heldQuota = lotId == firstId ? (arrivalAt == arrival ? 5 : 2) : 0 });
        });
        app.MapPost("/internal/ai/recommendations", async (HttpContext context) =>
        {
            Assert.Equal("arrival-test-key", context.Request.Headers["X-Arrival-Service-Key"].ToString());
            var request = await context.Request.ReadFromJsonAsync<RecommendationClientRequest>(cancellationToken: context.RequestAborted);
            Assert.NotNull(request);
            aiCalls.Enqueue(request);
            return Results.Ok(new RecommendationClientResponse(request.CandidateLots.Select((lot, index) =>
                new RecommendedLotItemDto(lot.LotId, lot.Name, index + 1, lot.DriveTimeMinutes,
                    lot.HeldQuotaKnown ? 70d : null, lot.PriceForDuration, false,
                    ["Test result"], lot.HeldQuotaKnown ? 10 : 0, !lot.HeldQuotaKnown)).ToList(), false, null));
        });
        await app.StartAsync(ct);
        using var bookingHttp = app.GetTestClient();
        using var aiHttp = app.GetTestClient();
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["BookingService:BaseUrl"] = bookingHttp.BaseAddress!.ToString(),
            ["AiService:BaseUrl"] = aiHttp.BaseAddress!.ToString(),
            ["ArrivalIntegration:Key"] = "arrival-test-key",
            ["ServiceAuth:ServiceKey"] = "global-test-key",
            ["BookingService:ArrivalQuotaTimeoutMs"] = "5000"
        }).Build();
        var quotaClient = new ArrivalHeldQuotaClient(bookingHttp, config, NullLogger<ArrivalHeldQuotaClient>.Instance);
        var aiClient = new ParkingRecommendationClient(aiHttp, config, NullLogger<ParkingRecommendationClient>.Instance);
        var lots = new NearbyReader(firstId, secondId);
        var handler = new GetParkingLotRecommendationsQueryHandler(lots, new MetricsReader(vehicleType),
            new SnapshotReader(), quotaClient, aiClient, new EmptyRouteReader(), NullLogger<GetParkingLotRecommendationsQueryHandler>.Instance);

        foreach (var at in new[] { arrival, arrival.AddHours(1) })
        {
            var result = await handler.Handle(new GetParkingLotRecommendationsQuery(10.77m, 106.70m, vehicleType, at), ct);
            Assert.False(result.FallbackUsed);
            Assert.Equal(2, result.RecommendedLots.Count);
            Assert.Equal(vehicleType == "Car" && failSecond, result.RecommendedLots.Single(x => x.LotId == secondId).InsufficientData);
            if (vehicleType == "Car" && failSecond)
                Assert.Null(result.RecommendedLots.Single(x => x.LotId == secondId).ProbabilityPercent);
        }

        Assert.Equal(2, aiCalls.Count);
        foreach (var request in aiCalls)
        {
            var first = request.CandidateLots.Single(x => x.LotId == firstId);
            var second = request.CandidateLots.Single(x => x.LotId == secondId);
            Assert.Equal(vehicleType == "Car" ? (request.ArrivalAt == arrival ? 5 : 2) : 0, first.HeldQuota);
            Assert.True(first.HeldQuotaKnown);
            Assert.Equal(0, second.HeldQuota);
            Assert.Equal(!(vehicleType == "Car" && failSecond), second.HeldQuotaKnown);
        }
        Assert.Equal(new[] { arrival, arrival.AddHours(1) }, aiCalls.Select(x => x.ArrivalAt).ToArray());
        if (vehicleType == "Motorbike")
            Assert.Empty(bookingCalls);
        else
        {
            Assert.Equal(4, bookingCalls.Count);
            foreach (var at in new[] { arrival, arrival.AddHours(1) })
            {
                Assert.Contains(bookingCalls, x => x.LotId == firstId && x.ArrivalAt == at);
                Assert.Contains(bookingCalls, x => x.LotId == secondId && x.ArrivalAt == at);
            }
        }
    }

    private sealed class EmptyRouteReader : IRecommendationDrivingEtaReader
    {
        public Task<IReadOnlyDictionary<Guid, double?>> GetDrivingMinutesAsync(
            decimal originLatitude, decimal originLongitude,
            IReadOnlyList<RecommendationRouteDestination> destinations,
            string vehicleGroup, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyDictionary<Guid, double?>>(new Dictionary<Guid, double?>());
    }

    private sealed class NearbyReader(Guid firstId, Guid secondId) : INearbyParkingLotReader
    {
        public Task<IReadOnlyList<NearbyParkingLotItem>> SearchAsync(decimal latitude, decimal longitude,
            double radiusKm, int limit, CancellationToken cancellationToken, VehicleType? vehicleType = null) =>
            Task.FromResult<IReadOnlyList<NearbyParkingLotItem>>([
                new(firstId, "First", "Address 1", 10.77m, 106.70m, "Standard", 1),
                new(secondId, "Second", "Address 2", 10.78m, 106.71m, "Standard", 2)
            ]);
    }

    private sealed class MetricsReader(string vehicleType) : ICustomerLotMetricsReader
    {
        public Task<IReadOnlyDictionary<Guid, CustomerLotMetrics>> ReadAsync(IReadOnlyCollection<Guid> lotIds, CancellationToken ct) =>
            Task.FromResult<IReadOnlyDictionary<Guid, CustomerLotMetrics>>(lotIds.ToDictionary(x => x,
                _ => new CustomerLotMetrics(10, 10, [new CustomerLotPrice(vehicleType, 10000m, "PerEntry")])));
    }

    private sealed class SnapshotReader : IArrivalSnapshotReader
    {
        public Task<IReadOnlyList<ArrivalAvailabilitySnapshot>> GetSessionProxyAsync(Guid lotId, string vehicleType,
            DateTimeOffset arrivalAt, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ArrivalAvailabilitySnapshot>>([]);
    }
}
