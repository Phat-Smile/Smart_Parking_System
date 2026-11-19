using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using ParkingSystem.Parking.WebAPI.Clients;
using Xunit;

namespace ParkingSystem.Tests.Parking.UnitTests;

public sealed class ArrivalHeldQuotaClientTests
{
    private static readonly Guid LotId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly DateTimeOffset Arrival = new(2026, 11, 20, 14, 30, 0, TimeSpan.FromHours(7));

    [Theory]
    [InlineData(5)]
    [InlineData(0)]
    public async Task Valid_response_preserves_count_and_arrival_instant(int count)
    {
        using var transport = new StubHandler((request, _) =>
        {
            Assert.Equal(HttpMethod.Get, request.Method);
            Assert.Equal("/internal/booking/held-quota", request.RequestUri!.AbsolutePath);
            var query = QueryHelpers.ParseQuery(request.RequestUri.Query);
            Assert.Equal(LotId, Guid.Parse(query["lotId"].ToString()));
            Assert.Equal(Arrival, DateTimeOffset.Parse(query["arrivalAt"].ToString()));
            Assert.Equal("arrival-test-key", request.Headers.GetValues("X-Arrival-Service-Key").Single());
            Assert.Equal("global-test-key", request.Headers.GetValues("X-Service-Key").Single());
            // Different timezone offset, same instant: accept.
            return Task.FromResult(Json(new { lotId = LotId, arrivalAt = Arrival.ToUniversalTime(), heldQuota = count }));
        });
        using var http = new HttpClient(transport);
        Assert.Equal(count, await Client(http).GetHeldQuotaAsync(LotId, Arrival, TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public async Task Non_success_status_means_unknown(HttpStatusCode status)
    {
        using var http = new HttpClient(new StubHandler((_, _) => Task.FromResult(new HttpResponseMessage(status))));
        Assert.Null(await Client(http).GetHeldQuotaAsync(LotId, Arrival, TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("wrong_lot")]
    [InlineData("wrong_arrival")]
    [InlineData("negative")]
    [InlineData("missing_count")]
    [InlineData("missing_arrival")]
    [InlineData("null_count")]
    [InlineData("malformed")]
    public async Task Invalid_or_incomplete_response_means_unknown(string problem)
    {
        using var http = new HttpClient(new StubHandler((_, _) => Task.FromResult(problem switch
        {
            "wrong_lot" => Json(new { lotId = Guid.NewGuid(), arrivalAt = Arrival, heldQuota = 5 }),
            "wrong_arrival" => Json(new { lotId = LotId, arrivalAt = Arrival.AddMinutes(1), heldQuota = 5 }),
            "negative" => Json(new { lotId = LotId, arrivalAt = Arrival, heldQuota = -1 }),
            "missing_count" => Json(new { lotId = LotId, arrivalAt = Arrival }),
            "missing_arrival" => Json(new { lotId = LotId, heldQuota = 0 }),
            "null_count" => Json(new { lotId = LotId, arrivalAt = Arrival, heldQuota = (int?)null }),
            _ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{bad", Encoding.UTF8, "application/json") }
        })));
        Assert.Null(await Client(http).GetHeldQuotaAsync(LotId, Arrival, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Missing_key_never_sends_request()
    {
        var calls = 0;
        using var http = new HttpClient(new StubHandler((_, _) =>
        {
            calls++;
            return Task.FromResult(Json(new { lotId = LotId, arrivalAt = Arrival, heldQuota = 0 }));
        }));
        Assert.Null(await Client(http, key: null).GetHeldQuotaAsync(LotId, Arrival, TestContext.Current.CancellationToken));
        Assert.Equal(0, calls);
    }

    [Fact]
    public async Task Network_failure_means_unknown()
    {
        using var http = new HttpClient(new StubHandler((_, _) => throw new HttpRequestException("offline")));
        Assert.Null(await Client(http).GetHeldQuotaAsync(LotId, Arrival, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Deadline_means_unknown()
    {
        var deadlineCancellationObserved = false;
        using var http = new HttpClient(new StubHandler(async (_, ct) =>
        {
            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, ct);
                throw new InvalidOperationException("Unreachable");
            }
            finally
            {
                deadlineCancellationObserved = ct.IsCancellationRequested;
            }
        })) { Timeout = Timeout.InfiniteTimeSpan };
        Assert.Null(await Client(http, timeoutMs: "100").GetHeldQuotaAsync(
            LotId, Arrival, TestContext.Current.CancellationToken));
        Assert.True(deadlineCancellationObserved);
    }

    [Fact]
    public async Task Caller_cancellation_is_not_converted_to_unknown()
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        using var http = new HttpClient(new StubHandler((_, ct) =>
        {
            cts.Cancel();
            ct.ThrowIfCancellationRequested();
            throw new InvalidOperationException("Unreachable");
        }));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Client(http).GetHeldQuotaAsync(LotId, Arrival, cts.Token));
    }

    // Give ordinary contract tests room for first-use JSON/JIT and parallel test scheduling.
    // Only Deadline_means_unknown intentionally uses a short deadline.
    private static ArrivalHeldQuotaClient Client(
        HttpClient http, string? key = "arrival-test-key", string timeoutMs = "5000") =>
        new(http, new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["BookingService:BaseUrl"] = "http://booking.test",
            ["BookingService:ArrivalQuotaTimeoutMs"] = timeoutMs,
            ["ArrivalIntegration:Key"] = key,
            ["ServiceAuth:ServiceKey"] = "global-test-key"
        }).Build(), NullLogger<ArrivalHeldQuotaClient>.Instance);

    private static HttpResponseMessage Json(object value) => new(HttpStatusCode.OK) { Content = JsonContent.Create(value) };

    private sealed class StubHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            send(request, cancellationToken);
    }
}
