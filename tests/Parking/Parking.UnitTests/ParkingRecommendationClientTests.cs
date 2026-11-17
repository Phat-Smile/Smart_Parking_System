using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using ParkingSystem.Parking.Application.ParkingLots.Recommendations;
using ParkingSystem.Parking.WebAPI.Clients;
using Xunit;

namespace ParkingSystem.Tests.Parking.UnitTests;

public sealed class ParkingRecommendationClientTests
{
    [Fact]
    public async Task Calls_existing_internal_http_endpoint_with_priority_and_service_key()
    {
        var count = 0;
        using var transport = new StubHandler(async (request, ct) =>
        {
            count++;
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal("http://127.0.0.1:8000/internal/ai/recommendations", request.RequestUri!.ToString());
            Assert.Equal("qa-service-key", request.Headers.GetValues("X-Arrival-Service-Key").Single());
            using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct));
            Assert.Equal("cheapest", body.RootElement.GetProperty("priority").GetString());
            Assert.Equal(1, body.RootElement.GetProperty("candidateLots").GetArrayLength());
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    "{\"recommendedLots\":[],\"fallbackUsed\":true,\"fallbackReason\":\"ai_recommendation_unavailable\"}",
                    Encoding.UTF8, "application/json")
            };
        });
        using var http = new HttpClient(transport) { Timeout = Timeout.InfiniteTimeSpan };
        var client = new ParkingRecommendationClient(http, Config(), NullLogger<ParkingRecommendationClient>.Instance);

        var result = await client.GetRecommendationsAsync(Request("cheapest"), TestContext.Current.CancellationToken);

        Assert.Equal(1, count);
        Assert.NotNull(result);
        Assert.True(result.FallbackUsed);
        Assert.Empty(result.RecommendedLots);
    }

    [Fact]
    public async Task Configured_timeout_returns_null_instead_of_waiting_indefinitely()
    {
        using var transport = new StubHandler(async (_, ct) =>
        {
            await Task.Delay(TimeSpan.FromSeconds(3), ct);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        using var http = new HttpClient(transport) { Timeout = Timeout.InfiniteTimeSpan };
        var client = new ParkingRecommendationClient(http,
            Config([new KeyValuePair<string, string?>("AiService:RecommendationTimeoutMs", "100")]),
            NullLogger<ParkingRecommendationClient>.Instance);

        var result = await client.GetRecommendationsAsync(Request(), TestContext.Current.CancellationToken);

        Assert.Null(result);
    }

    [Fact]
    public async Task Caller_cancellation_is_propagated_not_masked_as_a_fallback()
    {
        using var transport = new StubHandler(async (_, ct) =>
        {
            await Task.Delay(TimeSpan.FromSeconds(3), ct);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        using var http = new HttpClient(transport) { Timeout = Timeout.InfiniteTimeSpan };
        var client = new ParkingRecommendationClient(http, Config(), NullLogger<ParkingRecommendationClient>.Instance);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.GetRecommendationsAsync(Request(), cts.Token));
    }

    [Fact]
    public async Task Missing_service_key_does_not_send_a_request()
    {
        var count = 0;
        using var transport = new StubHandler((_, _) =>
        {
            count++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        });
        using var http = new HttpClient(transport) { Timeout = Timeout.InfiniteTimeSpan };
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["AiService:BaseUrl"] = "http://127.0.0.1:8000" })
            .Build();
        var client = new ParkingRecommendationClient(http, config, NullLogger<ParkingRecommendationClient>.Instance);

        Assert.Null(await client.GetRecommendationsAsync(Request(), TestContext.Current.CancellationToken));
        Assert.Equal(0, count);
    }

    private static RecommendationClientRequest Request(string priority = "balanced") =>
        new(10.77, 106.70, "Car", DateTimeOffset.Parse("2026-11-16T10:00:00+07:00"), 120,
            [new CandidateLotInputDto(Guid.NewGuid(), "Bai thu nghiem", 10.77, 106.70,
                20000, 0, true, [])], priority);

    private static IConfiguration Config(IEnumerable<KeyValuePair<string, string?>>? extra = null)
    {
        var values = new Dictionary<string, string?>
        {
            ["ArrivalIntegration:Key"] = "qa-service-key",
            ["AiService:BaseUrl"] = "http://127.0.0.1:8000"
        };
        if (extra is not null)
            foreach (var pair in extra) values[pair.Key] = pair.Value;
        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }

    private sealed class StubHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) => respond(request, cancellationToken);
    }
}
