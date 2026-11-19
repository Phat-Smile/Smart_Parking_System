using System.Globalization;
using System.Net;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ParkingSystem.BuildingBlocks.Time;
using ParkingSystem.Parking.Infrastructure.Booking;
using Xunit;

namespace ParkingSystem.Tests.Parking.UnitTests;

public sealed class BookingHeldQuotaHttpClientTests
{
    private const string ArrivalKey = "arrival-key";
    private const string ServiceKey = "service-key";
    private static readonly Guid LotId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly DateTimeOffset Now = new(2026, 11, 10, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Calls_the_internal_held_quota_route_with_the_arrival_key_and_maps_held_quota()
    {
        var handler = new StubHandler(_ => Json(HttpStatusCode.OK,
            $$"""{"lotId":"{{LotId}}","arrivalAt":"2026-11-10T08:00:00Z","vehicleGroup":"Car","heldQuota":3}"""));
        var logger = new ListLogger();
        var client = NewClient(handler, logger, ArrivalKey);

        var result = await client.CountHeldReservationsAsync(LotId, TestContext.Current.CancellationToken);

        Assert.Equal(3, result);
        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Get, request.Method);
        var expectedTimestamp = Uri.EscapeDataString(Now.ToString("O", CultureInfo.InvariantCulture));
        Assert.Equal($"http://booking-api:8080/internal/booking/held-quota?lotId={LotId:D}&arrivalAt={expectedTimestamp}&vehicleGroup=Car", request.Uri);
        Assert.Equal(ArrivalKey, request.Headers[BookingHeldQuotaHttpClient.ArrivalKeyHeader]);
        Assert.False(request.Headers.ContainsKey(BookingHeldQuotaHttpClient.ServiceKeyHeader));
    }

    [Fact]
    public async Task Sends_the_shared_service_key_when_it_is_configured()
    {
        var handler = new StubHandler(_ => Json(HttpStatusCode.OK,
            $$"""{"lotId":"{{LotId}}","arrivalAt":"2026-11-10T08:00:00Z","vehicleGroup":"Car","heldQuota":0}"""));
        var client = NewClient(handler, new ListLogger(), ArrivalKey, ServiceKey);

        await client.CountHeldReservationsAsync(LotId, TestContext.Current.CancellationToken);

        Assert.Equal(ServiceKey, Assert.Single(handler.Requests).Headers[BookingHeldQuotaHttpClient.ServiceKeyHeader]);
    }

    [Fact]
    public async Task Missing_arrival_key_throws_invalid_operation_exception()
    {
        var handler = new StubHandler(_ => throw new InvalidOperationException("must not be called"));
        var client = NewClient(handler, new ListLogger(), arrivalKey: null);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.CountHeldReservationsAsync(LotId, TestContext.Current.CancellationToken));
        Assert.Empty(handler.Requests);
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public async Task Http_error_throws_http_request_exception(HttpStatusCode statusCode)
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(statusCode));
        var client = NewClient(handler, new ListLogger(), ArrivalKey);

        var ex = await Assert.ThrowsAsync<HttpRequestException>(() =>
            client.CountHeldReservationsAsync(LotId, TestContext.Current.CancellationToken));
        Assert.Equal(statusCode, ex.StatusCode);
    }

    [Fact]
    public async Task Malformed_json_response_throws_exception()
    {
        var handler = new StubHandler(_ => Json(HttpStatusCode.OK, """{"invalid":true}"""));
        var client = NewClient(handler, new ListLogger(), ArrivalKey);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.CountHeldReservationsAsync(LotId, TestContext.Current.CancellationToken));
    }

    private static BookingHeldQuotaHttpClient NewClient(
        StubHandler handler,
        ListLogger logger,
        string? arrivalKey,
        string? serviceKey = null)
    {
        var settings = new Dictionary<string, string?>
        {
            ["ArrivalIntegration:Key"] = arrivalKey,
            ["ServiceAuth:ServiceKey"] = serviceKey
        };
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        var options = Options.Create(new BookingServiceOptions { BaseUrl = "http://booking-api:8080/", TimeoutSeconds = 3 });
        var clock = new FixedClock(Now);
        return new BookingHeldQuotaHttpClient(new HttpClient(handler), options, configuration, clock, logger);
    }

    private static HttpResponseMessage Json(HttpStatusCode status, string body) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private sealed record CapturedRequest(HttpMethod Method, string Uri, IReadOnlyDictionary<string, string> Headers);

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> handle) : HttpMessageHandler
    {
        public List<CapturedRequest> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(new CapturedRequest(
                request.Method,
                request.RequestUri!.ToString(),
                request.Headers.ToDictionary(header => header.Key, header => string.Join(",", header.Value))));
            return Task.FromResult(handle(request));
        }
    }

    private sealed class ListLogger : ILogger<BookingHeldQuotaHttpClient>
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Entries.Add((logLevel, formatter(state, exception)));
    }

    private sealed class FixedClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset UtcNow => now;
    }
}
