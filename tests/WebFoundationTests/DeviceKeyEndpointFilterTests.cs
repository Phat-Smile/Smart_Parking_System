using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using ParkingSystem.AspNetCore;
using ParkingSystem.AspNetCore.Security;
using Xunit;

namespace ParkingSystem.Tests.WebFoundation;

public sealed class DeviceKeyEndpointFilterTests
{
    [Fact]
    public async Task Missing_device_key_header_returns_401()
    {
        await using var app = await BuildApp(sensorKey: "device-secret");

        using var response = await app.GetTestClient().PostAsync("/device", null, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Wrong_device_key_returns_401()
    {
        await using var app = await BuildApp(sensorKey: "device-secret");

        using var response = await Send(app, "not-the-key");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Correct_device_key_returns_200()
    {
        await using var app = await BuildApp(sensorKey: "device-secret");

        using var response = await Send(app, "device-secret");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Device_key_comparison_is_case_sensitive()
    {
        await using var app = await BuildApp(sensorKey: "DeviceSecret");

        using var response = await Send(app, "devicesecret");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // A route that needs a device key must never open up because the key was not configured.
    [Theory]
    [InlineData("")]
    [InlineData("anything")]
    public async Task When_no_device_key_is_configured_every_request_is_rejected(string sentKey)
    {
        await using var app = await BuildApp(sensorKey: "");

        using var response = await Send(app, sentKey);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task The_service_key_does_not_open_a_device_route()
    {
        await using var app = await BuildApp(sensorKey: "device-secret");
        using var request = new HttpRequestMessage(HttpMethod.Post, "/device");
        request.Headers.Add(ServiceKeyValidationMiddleware.HeaderName, "device-secret");

        using var response = await app.GetTestClient().SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static async Task<HttpResponseMessage> Send(WebApplication app, string key)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/device");
        request.Headers.TryAddWithoutValidation(DeviceKeyEndpointFilter.HeaderName, key);
        return await app.GetTestClient().SendAsync(request, TestContext.Current.CancellationToken);
    }

    private static async Task<WebApplication> BuildApp(string sensorKey)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            [$"{DeviceKeyOptions.SectionName}:SensorKey"] = sensorKey
        });
        builder.AddParkingSystemWebFoundation();
        builder.AddParkingSystemDeviceKey();

        var app = builder.Build();
        app.UseParkingSystemWebFoundation();
        app.MapPost("/device", () => Results.Ok()).RequireDeviceKey();

        await app.StartAsync(TestContext.Current.CancellationToken);
        return app;
    }
}
