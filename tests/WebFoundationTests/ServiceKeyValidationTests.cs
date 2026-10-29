using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ParkingSystem.AspNetCore;
using ParkingSystem.AspNetCore.Security;
using Xunit;

namespace ParkingSystem.Tests.WebFoundation;

public sealed class ServiceKeyValidationTests
{
    [Fact]
    public async Task When_service_key_not_configured_all_requests_pass_through()
    {
        await using var app = await BuildApp(serviceKey: "");
        var client = app.GetTestClient();

        using var withoutHeader = await client.GetAsync("/probe", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, withoutHeader.StatusCode);

        using var request = new HttpRequestMessage(HttpMethod.Get, "/probe");
        request.Headers.Add(ServiceKeyValidationMiddleware.HeaderName, "anything");
        using var withHeader = await client.SendAsync(request, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, withHeader.StatusCode);
    }

    [Fact]
    public async Task When_service_key_configured_correct_key_returns_200()
    {
        await using var app = await BuildApp(serviceKey: "correct-key");
        var client = app.GetTestClient();

        using var request = new HttpRequestMessage(HttpMethod.Get, "/probe");
        request.Headers.Add(ServiceKeyValidationMiddleware.HeaderName, "correct-key");
        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task When_service_key_configured_missing_header_returns_403()
    {
        await using var app = await BuildApp(serviceKey: "secret-key");
        var client = app.GetTestClient();

        using var response = await client.GetAsync("/probe", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task When_service_key_configured_wrong_key_returns_403()
    {
        await using var app = await BuildApp(serviceKey: "secret-key");
        var client = app.GetTestClient();

        using var request = new HttpRequestMessage(HttpMethod.Get, "/probe");
        request.Headers.Add(ServiceKeyValidationMiddleware.HeaderName, "wrong-key");
        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task When_service_key_configured_empty_header_value_returns_403()
    {
        await using var app = await BuildApp(serviceKey: "secret-key");
        var client = app.GetTestClient();

        using var request = new HttpRequestMessage(HttpMethod.Get, "/probe");
        request.Headers.TryAddWithoutValidation(ServiceKeyValidationMiddleware.HeaderName, string.Empty);
        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Service_key_comparison_is_case_sensitive()
    {
        await using var app = await BuildApp(serviceKey: "CaseSensitiveKey");
        var client = app.GetTestClient();

        using var upperRequest = new HttpRequestMessage(HttpMethod.Get, "/probe");
        upperRequest.Headers.Add(ServiceKeyValidationMiddleware.HeaderName, "CASESENSITIVEKEY");
        using var upperResponse = await client.SendAsync(upperRequest, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Forbidden, upperResponse.StatusCode);

        using var correctRequest = new HttpRequestMessage(HttpMethod.Get, "/probe");
        correctRequest.Headers.Add(ServiceKeyValidationMiddleware.HeaderName, "CaseSensitiveKey");
        using var correctResponse = await client.SendAsync(correctRequest, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, correctResponse.StatusCode);
    }

    private static async Task<WebApplication> BuildApp(string serviceKey)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            [$"{ServiceKeyOptions.SectionName}:ServiceKey"] = serviceKey
        });
        builder.AddParkingSystemWebFoundation();
        builder.AddParkingSystemServiceKeyValidation();

        var app = builder.Build();
        app.UseParkingSystemWebFoundation();
        app.UseServiceKeyValidation();
        app.MapGet("/probe", () => Results.Ok());

        await app.StartAsync(TestContext.Current.CancellationToken);
        return app;
    }
}
