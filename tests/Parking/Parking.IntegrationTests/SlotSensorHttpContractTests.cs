using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ParkingSystem.AspNetCore.Security;
using ParkingSystem.BuildingBlocks.Results;
using ParkingSystem.Parking.Application.ParkingLayout.ProcessSlotSensorEvent;
using ParkingSystem.Parking.WebAPI.Endpoints;
using Xunit;

namespace ParkingSystem.Tests.Parking.IntegrationTests;

/// <summary>IoT device ingress must preserve the device's original dedup key and event time.</summary>
public sealed class SlotSensorHttpContractTests
{
    private const string Route = "/api/parking/slots/sensor-events";
    private static readonly Guid LotId = Guid.Parse("10000000-0000-0000-0000-000000000001");

    [Theory]
    [InlineData("eventId")]
    [InlineData("lotId")]
    [InlineData("eventTime")]
    [InlineData("deviceId")]
    [InlineData("slotCode")]
    public async Task Missing_device_field_returns_400_without_dispatch(string missing)
    {
        var capture = new CaptureSender();
        await using var app = await CreateAppAsync(capture);
        var body = Payload();
        body.Remove(missing);

        using var response = await PostAsync(app, body, includeKey: true);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Null(capture.LastRequest);
    }

    [Theory]
    [InlineData("eventId")]
    [InlineData("lotId")]
    public async Task Empty_guid_is_rejected_before_dispatch(string field)
    {
        var capture = new CaptureSender();
        await using var app = await CreateAppAsync(capture);
        var body = Payload();
        body[field] = Guid.Empty.ToString();
        using var response = await PostAsync(app, body, includeKey: true);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Null(capture.LastRequest);
    }

    [Theory]
    [InlineData("1")]
    [InlineData("0")]
    [InlineData("Unknown")]
    public async Task Invalid_or_numeric_sensor_state_returns_400(string state)
    {
        var capture = new CaptureSender();
        await using var app = await CreateAppAsync(capture);
        var body = Payload();
        body["state"] = state;
        using var response = await PostAsync(app, body, includeKey: true);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Null(capture.LastRequest);
    }

    [Fact]
    public async Task Unauthorized_device_is_rejected_before_dispatch()
    {
        var capture = new CaptureSender();
        await using var app = await CreateAppAsync(capture);
        using var response = await PostAsync(app, Payload(), includeKey: false);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Null(capture.LastRequest);
    }

    [Fact]
    public async Task Valid_device_event_preserves_identifiers_and_timestamp()
    {
        var capture = new CaptureSender();
        await using var app = await CreateAppAsync(capture);
        var body = Payload();
        using var response = await PostAsync(app, body, includeKey: true);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var command = Assert.IsType<ProcessSlotSensorEventCommand>(capture.LastRequest);
        Assert.Equal(Guid.Parse((string)body["eventId"]!), command.EventId);
        Assert.Equal(LotId, command.LotId);
        Assert.Equal(DateTimeOffset.Parse((string)body["eventTime"]!), command.EventTime);
    }

    private static Dictionary<string, object?> Payload() => new()
    {
        ["eventId"] = "2c79c400-b9e3-4a50-a160-57a0b9d67c15",
        ["deviceId"] = "sensor-01",
        ["lotId"] = LotId.ToString(),
        ["slotCode"] = "A-01",
        ["state"] = "Occupied",
        ["eventTime"] = "2026-11-20T02:59:59Z"
    };

    private static async Task<HttpResponseMessage> PostAsync(WebApplication app, Dictionary<string, object?> body, bool includeKey)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, Route)
        {
            Content = JsonContent.Create(body)
        };
        if (includeKey) request.Headers.Add(DeviceKeyEndpointFilter.HeaderName, "test-sensor-key");
        return await app.GetTestClient().SendAsync(request, TestContext.Current.CancellationToken);
    }

    private static async Task<WebApplication> CreateAppAsync(CaptureSender sender)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["DeviceAuth:SensorKey"] = "test-sensor-key"
        });
        builder.AddParkingSystemDeviceKey();
        builder.Services.AddSingleton<ISender>(sender);
        var app = builder.Build();
        app.MapSlotSensorEndpoints();
        await app.StartAsync(TestContext.Current.CancellationToken);
        return app;
    }

    private sealed class CaptureSender : ISender
    {
        public object? LastRequest { get; private set; }

        public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            var sensorEvent = (ProcessSlotSensorEventCommand)(object)request;
            var reply = new ProcessedSensorEventResponse(sensorEvent.EventId, sensorEvent.LotId, sensorEvent.SlotCode,
                "Processed", "Occupied", false, sensorEvent.EventTime, sensorEvent.EventTime);
            return Task.FromResult((TResponse)(object)Result<ProcessedSensorEventResponse>.Success(reply));
        }

        public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default) where TRequest : IRequest =>
            throw new NotSupportedException();
        public Task<object?> Send(object request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
