using System.Net;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ParkingSystem.Parking.Infrastructure.Booking;
using Xunit;

namespace ParkingSystem.Tests.Parking.UnitTests;

// US-PARK-004, contract 3: Parking calls Booking's internal check-in with the arrival key; every failure means walk-in.
public sealed class BookingCheckInHttpClientTests
{
    private const string ArrivalKey = "arrival-key";
    private const string ServiceKey = "service-key";
    private static readonly Guid LotId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly Guid SessionId = Guid.Parse("20000000-0000-0000-0000-000000000001");
    private static readonly Guid ReservationId = Guid.Parse("30000000-0000-0000-0000-000000000001");
    private static readonly Guid UserId = Guid.Parse("40000000-0000-0000-0000-000000000001");
    private static readonly DateTimeOffset EntryTime = new(2026, 11, 10, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Calls_the_internal_route_with_the_arrival_key_and_maps_the_reservation()
    {
        var handler = new StubHandler(_ => Json(HttpStatusCode.OK,
            $$"""{"hasReservation":true,"reservationId":"{{ReservationId}}","userId":"{{UserId}}","vehicleId":null,"message":"ok"}"""));
        var logger = new ListLogger();

        var result = await NewClient(handler, logger, ArrivalKey).CheckInReservationAsync(LotId, "51A12345", EntryTime, SessionId, TestContext.Current.CancellationToken);

        Assert.Equal(ReservationId, result!.ReservationId);
        Assert.Equal(UserId, result.UserId);
        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("http://booking-api:8080/internal/booking/reservations/check-in", request.Uri);
        Assert.Equal(ArrivalKey, request.Headers[BookingCheckInHttpClient.ArrivalKeyHeader]);
        Assert.False(request.Headers.ContainsKey(BookingCheckInHttpClient.ServiceKeyHeader));
        Assert.Contains($"\"SessionId\":\"{SessionId}\"", request.Body);
    }

    [Fact]
    public async Task Sends_the_shared_service_key_when_it_is_configured()
    {
        var handler = new StubHandler(_ => Json(HttpStatusCode.OK, """{"hasReservation":false}"""));

        await NewClient(handler, new ListLogger(), ArrivalKey, ServiceKey).CheckInReservationAsync(LotId, "51A12345", EntryTime, SessionId, TestContext.Current.CancellationToken);

        Assert.Equal(ServiceKey, Assert.Single(handler.Requests).Headers[BookingCheckInHttpClient.ServiceKeyHeader]);
    }

    [Fact]
    public async Task No_reservation_returns_null()
    {
        var handler = new StubHandler(_ => Json(HttpStatusCode.OK, """{"hasReservation":false,"message":"none"}"""));

        var result = await NewClient(handler, new ListLogger(), ArrivalKey).CheckInReservationAsync(LotId, "51A12345", EntryTime, SessionId, TestContext.Current.CancellationToken);

        Assert.Null(result);
    }

    [Theory]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public async Task A_refused_key_is_logged_as_a_configuration_error_and_returns_null(HttpStatusCode status)
    {
        var logger = new ListLogger();
        var handler = new StubHandler(_ => new HttpResponseMessage(status));

        var result = await NewClient(handler, logger, ArrivalKey).CheckInReservationAsync(LotId, "51A12345", EntryTime, SessionId, TestContext.Current.CancellationToken);

        Assert.Null(result);
        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Error, entry.Level);
        Assert.Contains("ArrivalIntegration:Key", entry.Message);
    }

    [Fact]
    public async Task Another_error_is_a_warning_and_returns_null()
    {
        var logger = new ListLogger();
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError));

        var result = await NewClient(handler, logger, ArrivalKey).CheckInReservationAsync(LotId, "51A12345", EntryTime, SessionId, TestContext.Current.CancellationToken);

        Assert.Null(result);
        Assert.Equal(LogLevel.Warning, Assert.Single(logger.Entries).Level);
    }

    [Fact]
    public async Task Missing_key_skips_the_call_with_a_warning()
    {
        var logger = new ListLogger();
        var handler = new StubHandler(_ => throw new InvalidOperationException("must not be called"));

        var result = await NewClient(handler, logger, arrivalKey: null).CheckInReservationAsync(LotId, "51A12345", EntryTime, SessionId, TestContext.Current.CancellationToken);

        Assert.Null(result);
        Assert.Empty(handler.Requests);
        Assert.Equal(LogLevel.Warning, Assert.Single(logger.Entries).Level);
    }

    [Fact]
    public async Task A_timeout_returns_null()
    {
        var handler = new StubHandler(_ => throw new TaskCanceledException("timeout"));

        var result = await NewClient(handler, new ListLogger(), ArrivalKey).CheckInReservationAsync(LotId, "51A12345", EntryTime, SessionId, TestContext.Current.CancellationToken);

        Assert.Null(result);
    }

    [Fact]
    public async Task No_suitable_slot_report_calls_its_internal_route_and_returns_the_cancelled_reservation()
    {
        var handler = new StubHandler(_ => Json(HttpStatusCode.OK, $$"""{"cancelled":true,"reservationId":"{{ReservationId}}"}"""));

        var result = await NewClient(handler, new ListLogger(), ArrivalKey)
            .ReportNoSuitableSlotAsync(LotId, "51A12345", EntryTime, TestContext.Current.CancellationToken);

        Assert.Equal(ReservationId, result);
        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("http://booking-api:8080/internal/booking/reservations/no-suitable-slot", request.Uri);
        Assert.Equal(ArrivalKey, request.Headers[BookingCheckInHttpClient.ArrivalKeyHeader]);
        Assert.Contains("\"PlateNumber\":\"51A12345\"", request.Body);
        Assert.Contains("\"ArrivedAt\"", request.Body);
    }

    [Theory]
    [InlineData(HttpStatusCode.OK, """{"cancelled":false}""")]
    [InlineData(HttpStatusCode.NotFound, "")]
    [InlineData(HttpStatusCode.InternalServerError, "")]
    public async Task No_suitable_slot_report_returns_null_without_a_cancelled_reservation(HttpStatusCode status, string body)
    {
        // 404 also covers a Booking that does not have the route yet.
        var handler = new StubHandler(_ => Json(status, body));

        var result = await NewClient(handler, new ListLogger(), ArrivalKey)
            .ReportNoSuitableSlotAsync(LotId, "51A12345", EntryTime, TestContext.Current.CancellationToken);

        Assert.Null(result);
    }

    [Fact]
    public async Task No_suitable_slot_report_is_skipped_without_the_arrival_key()
    {
        var handler = new StubHandler(_ => Json(HttpStatusCode.OK, """{"cancelled":true}"""));

        var result = await NewClient(handler, new ListLogger(), arrivalKey: null)
            .ReportNoSuitableSlotAsync(LotId, "51A12345", EntryTime, TestContext.Current.CancellationToken);

        Assert.Null(result);
        Assert.Empty(handler.Requests);
    }

    private static BookingCheckInHttpClient NewClient(StubHandler handler, ListLogger logger, string? arrivalKey, string? serviceKey = null)
    {
        var settings = new Dictionary<string, string?>
        {
            ["ArrivalIntegration:Key"] = arrivalKey,
            ["ServiceAuth:ServiceKey"] = serviceKey
        };
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        var options = Options.Create(new BookingServiceOptions { BaseUrl = "http://booking-api:8080/", TimeoutSeconds = 3 });
        return new BookingCheckInHttpClient(new HttpClient(handler), options, configuration, logger);
    }

    private static HttpResponseMessage Json(HttpStatusCode status, string body) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private sealed record CapturedRequest(HttpMethod Method, string Uri, IReadOnlyDictionary<string, string> Headers, string Body);

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> handle) : HttpMessageHandler
    {
        public List<CapturedRequest> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
            Requests.Add(new CapturedRequest(
                request.Method,
                request.RequestUri!.ToString(),
                request.Headers.ToDictionary(header => header.Key, header => string.Join(",", header.Value)),
                body));
            return handle(request);
        }
    }

    private sealed class ListLogger : ILogger<BookingCheckInHttpClient>
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Entries.Add((logLevel, formatter(state, exception)));
    }
}
