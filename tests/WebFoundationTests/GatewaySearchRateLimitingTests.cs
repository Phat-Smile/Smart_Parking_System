using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ParkingSystem.AspNetCore;
using ParkingSystem.AspNetCore.Security;
using Xunit;
using Yarp.ReverseProxy.Configuration;

namespace ParkingSystem.Tests.WebFoundation;

// US-AUTH-007: Gateway rate limiting for search endpoints (per-IP, 60 req/min default, tested with limit of 3).
public sealed class GatewaySearchRateLimitingTests : IAsyncLifetime
{
    private const int SearchPermitsInTest = 3;
    private readonly string _keyDirectory = Path.Combine(Path.GetTempPath(), $"gateway-search-rate-limit-{Guid.NewGuid():N}");
    private WebApplication _destination = null!;
    private WebApplication _gateway = null!;
    private HttpClient _client = null!;

    public async ValueTask InitializeAsync()
    {
        Directory.CreateDirectory(_keyDirectory);
        var publicKeyPath = Path.Combine(_keyDirectory, "public.pem");
        using (var rsa = RSA.Create(2048))
            await File.WriteAllTextAsync(publicKeyPath, rsa.ExportSubjectPublicKeyInfoPem());

        _destination = await StartDestinationAsync();
        var destinationAddress = _destination.Services.GetRequiredService<IServer>()
            .Features.Get<IServerAddressesFeature>()!.Addresses.Single();

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development" });
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Security:Jwt:Issuer"] = "ParkingSystem.Identity",
            ["Security:Jwt:Audience"] = "ParkingSystem",
            ["Security:Jwt:PublicKeyPath"] = publicKeyPath,
            ["Security:Cors:AllowedOrigins"] = "",
            [$"{GatewaySearchRateLimiting.SectionName}:{GatewaySearchRateLimiting.PolicyName}"] = SearchPermitsInTest.ToString()
        });
        builder.AddParkingSystemWebFoundation();
        builder.AddParkingSystemGatewaySecurity();
        builder.AddParkingSystemSearchRateLimiting();
        builder.Services.AddReverseProxy().LoadFromMemory(
            [
                new RouteConfig
                {
                    RouteId = "parking-search",
                    ClusterId = "parking",
                    RateLimiterPolicy = GatewaySearchRateLimiting.PolicyName,
                    Match = new RouteMatch { Path = "/api/parking/lots/search", Methods = ["GET"] }
                },
                new RouteConfig
                {
                    RouteId = "parking",
                    ClusterId = "parking",
                    RateLimiterPolicy = null,
                    Match = new RouteMatch { Path = "/api/parking/{**catch-all}" }
                }
            ],
            [new ClusterConfig
            {
                ClusterId = "parking",
                Destinations = new Dictionary<string, DestinationConfig> { ["default"] = new() { Address = destinationAddress } }
            }]);

        _gateway = builder.Build();
        _gateway.UseParkingSystemWebFoundation();
        _gateway.UseParkingSystemGatewaySecurity();
        _gateway.UseRateLimiter();
        _gateway.MapReverseProxy();
        await _gateway.StartAsync(TestContext.Current.CancellationToken);
        _client = _gateway.GetTestClient();
    }

    [Fact]
    public async Task Requests_over_the_search_limit_get_429_with_retry_after_and_never_reach_parking_service()
    {
        for (var i = 0; i < SearchPermitsInTest; i++)
        {
            using var allowed = await _client.GetAsync("/api/parking/lots/search", TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
        }

        using var rejected = await _client.GetAsync("/api/parking/lots/search", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
        Assert.InRange(rejected.Headers.RetryAfter!.Delta!.Value.TotalSeconds, 1, 60);
        var problem = await rejected.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal(GatewaySearchRateLimiting.RejectedErrorCode, problem.GetProperty("errorCode").GetString());
        Assert.Equal(SearchPermitsInTest, DestinationHits("/api/parking/lots/search"));
    }

    [Fact]
    public void Search_routes_are_attached_with_search_rate_limiter_policy_in_gateway_configuration()
    {
        var configuration = new ConfigurationBuilder()
            .AddJsonFile(Path.Combine(FindRepositoryRoot(), "src", "Gateway", "appsettings.json"))
            .Build();
        var routes = configuration.GetSection("ReverseProxy:Routes").GetChildren().ToList();

        var searchRoute = Assert.Single(routes, r => r.Key == "parking-search");
        Assert.Equal("parking", searchRoute["ClusterId"]);
        Assert.Equal(GatewaySearchRateLimiting.PolicyName, searchRoute["RateLimiterPolicy"]);
        Assert.Equal("GET", searchRoute["Match:Methods:0"]);
        Assert.Equal("/api/parking/lots/search", searchRoute["Match:Path"]);

        var nearbyRoute = Assert.Single(routes, r => r.Key == "parking-nearby");
        Assert.Equal("parking", nearbyRoute["ClusterId"]);
        Assert.Equal(GatewaySearchRateLimiting.PolicyName, nearbyRoute["RateLimiterPolicy"]);
        Assert.Equal("GET", nearbyRoute["Match:Methods:0"]);
        Assert.Equal("/api/parking/lots/nearby", nearbyRoute["Match:Path"]);
    }

    private int DestinationHits(string path) =>
        _destination.Services.GetRequiredService<HitCounter>().Count(path);

    private static async Task<WebApplication> StartDestinationAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddSingleton<HitCounter>();
        var app = builder.Build();
        app.MapGet("/{**path}", (HttpContext context, HitCounter hits) =>
        {
            hits.Record(context.Request.Path.Value!);
            return Results.Ok();
        });
        await app.StartAsync(TestContext.Current.CancellationToken);
        return app;
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ParkingSystem.slnx")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Không tìm thấy thư mục gốc repo.");
    }

    public async ValueTask DisposeAsync()
    {
        _client.Dispose();
        await _gateway.DisposeAsync();
        await _destination.DisposeAsync();
        if (Directory.Exists(_keyDirectory))
            Directory.Delete(_keyDirectory, recursive: true);
    }

    private sealed class HitCounter
    {
        private readonly System.Collections.Concurrent.ConcurrentDictionary<string, int> _hits = new();

        public void Record(string path) => _hits.AddOrUpdate(path, 1, (_, count) => count + 1);

        public int Count(string path) => _hits.GetValueOrDefault(path);
    }
}
