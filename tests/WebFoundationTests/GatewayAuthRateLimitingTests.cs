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

// TECH-10: Gateway thật (YARP + rate limiter) trước một service giả; ngưỡng login hạ xuống 2 qua config để test nhanh.
public sealed class GatewayAuthRateLimitingTests : IAsyncLifetime
{
    private const int LoginPermitsInTest = 2;
    private readonly string _keyDirectory = Path.Combine(Path.GetTempPath(), $"gateway-rate-limit-{Guid.NewGuid():N}");
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
            [$"{GatewayAuthRateLimiting.SectionName}:auth-login"] = LoginPermitsInTest.ToString()
        });
        builder.AddParkingSystemWebFoundation();
        builder.AddParkingSystemGatewaySecurity();
        builder.AddParkingSystemAuthRateLimiting();
        // The real Gateway registers both limiters and they share one rejection handler.
        builder.AddParkingSystemSearchRateLimiting();
        builder.Services.AddReverseProxy().LoadFromMemory(
            [
                Route("auth-login", "/api/identity/accounts/login", "auth-login"),
                Route("auth-refresh", "/api/identity/accounts/refresh", "auth-refresh"),
                Route("identity", "/api/identity/{**catch-all}", rateLimiterPolicy: null)
            ],
            [new ClusterConfig
            {
                ClusterId = "identity",
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
    public async Task Requests_over_the_login_limit_get_429_with_retry_after_and_never_reach_identity()
    {
        for (var i = 0; i < LoginPermitsInTest; i++)
        {
            using var allowed = await PostAsync("/api/identity/accounts/login");
            Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
        }

        using var rejected = await PostAsync("/api/identity/accounts/login");

        Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
        Assert.InRange(rejected.Headers.RetryAfter!.Delta!.Value.TotalSeconds, 1, 60);
        var problem = await rejected.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal(GatewayAuthRateLimiting.RejectedErrorCode, problem.GetProperty("errorCode").GetString());
        Assert.DoesNotContain("tìm kiếm", problem.GetProperty("detail").GetString(), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(LoginPermitsInTest, DestinationHits("/api/identity/accounts/login"));
    }

    [Fact]
    public async Task Each_route_has_its_own_limit_and_unlisted_routes_are_not_limited()
    {
        for (var i = 0; i <= LoginPermitsInTest; i++)
            (await PostAsync("/api/identity/accounts/login")).Dispose();

        using var refresh = await PostAsync("/api/identity/accounts/refresh");
        Assert.Equal(HttpStatusCode.OK, refresh.StatusCode);

        for (var i = 0; i < 20; i++)
        {
            using var other = await PostAsync("/api/identity/accounts/logout");
            Assert.Equal(HttpStatusCode.OK, other.StatusCode);
        }
    }

    [Fact]
    public void Every_auth_policy_is_attached_to_a_post_route_in_the_gateway_configuration()
    {
        var configuration = new ConfigurationBuilder()
            .AddJsonFile(Path.Combine(FindRepositoryRoot(), "src", "Gateway", "appsettings.json"))
            .Build();
        var routes = configuration.GetSection("ReverseProxy:Routes").GetChildren().ToList();

        foreach (var policy in GatewayAuthRateLimiting.DefaultPermitsPerMinute.Keys)
        {
            var route = Assert.Single(routes, r => r["RateLimiterPolicy"] == policy);
            Assert.Equal("identity", route["ClusterId"]);
            Assert.Equal("POST", route["Match:Methods:0"]);
        }
    }

    private static RouteConfig Route(string id, string path, string? rateLimiterPolicy) => new()
    {
        RouteId = id,
        ClusterId = "identity",
        RateLimiterPolicy = rateLimiterPolicy,
        Match = new RouteMatch { Path = path }
    };

    private async Task<HttpResponseMessage> PostAsync(string path) =>
        await _client.PostAsync(path, JsonContent.Create(new { }), TestContext.Current.CancellationToken);

    private int DestinationHits(string path) =>
        _destination.Services.GetRequiredService<HitCounter>().Count(path);

    private static async Task<WebApplication> StartDestinationAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddSingleton<HitCounter>();
        var app = builder.Build();
        app.MapPost("/{**path}", (HttpContext context, HitCounter hits) =>
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
