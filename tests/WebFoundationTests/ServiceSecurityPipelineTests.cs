using System.Net;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ParkingSystem.AspNetCore;
using ParkingSystem.AspNetCore.Security;
using Xunit;

namespace ParkingSystem.Tests.WebFoundation;

// TECH-18: the key must be enforced by the pipeline every service actually uses,
// not only by a hand-built test host.
public sealed class ServiceSecurityPipelineTests
{
    private const string Key = "pipeline-test-key-1234567890";

    [Fact]
    public async Task Service_pipeline_rejects_calls_that_skip_the_gateway()
    {
        using var keys = new TempPublicKey();
        await using var app = await CreateServiceApp(keys.Path, Key);
        var client = app.GetTestClient();

        using var withoutKey = await client.GetAsync("/open", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Forbidden, withoutKey.StatusCode);

        using var wrongKey = await Send(client, "/open", "wrong-key");
        Assert.Equal(HttpStatusCode.Forbidden, wrongKey.StatusCode);

        using var correct = await Send(client, "/open", Key);
        Assert.Equal(HttpStatusCode.OK, correct.StatusCode);
    }

    [Fact]
    public async Task A_valid_key_does_not_replace_the_jwt_check()
    {
        using var keys = new TempPublicKey();
        await using var app = await CreateServiceApp(keys.Path, Key);
        var client = app.GetTestClient();

        using var response = await Send(client, "/protected", Key);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("/health/live")]
    [InlineData("/health/ready")]
    public async Task Health_probes_work_without_the_service_key(string path)
    {
        using var keys = new TempPublicKey();
        await using var app = await CreateServiceApp(keys.Path, Key);
        var client = app.GetTestClient();

        using var response = await client.GetAsync(path, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task A_key_that_only_shares_a_prefix_is_rejected()
    {
        using var keys = new TempPublicKey();
        await using var app = await CreateServiceApp(keys.Path, Key);
        var client = app.GetTestClient();

        using var prefix = await Send(client, "/open", Key[..^1]);
        using var longer = await Send(client, "/open", Key + "x");

        Assert.Equal(HttpStatusCode.Forbidden, prefix.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, longer.StatusCode);
    }

    [Fact]
    public async Task Gateway_pipeline_does_not_demand_the_key_from_browsers()
    {
        using var keys = new TempPublicKey();
        var builder = CreateBuilder(keys.Path, Key);
        builder.WebHost.UseTestServer();
        builder.AddParkingSystemWebFoundation();
        builder.AddParkingSystemGatewaySecurity();
        var app = builder.Build();
        app.UseParkingSystemWebFoundation();
        app.UseParkingSystemGatewaySecurity();
        app.MapGet("/open", () => Results.Ok());
        await app.StartAsync(TestContext.Current.CancellationToken);
        await using (app)
        {
            using var response = await app.GetTestClient().GetAsync("/open", TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }

    private static Task<HttpResponseMessage> Send(HttpClient client, string path, string key)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add(ServiceKeyValidationMiddleware.HeaderName, key);
        return client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private static async Task<WebApplication> CreateServiceApp(string publicKeyPath, string serviceKey)
    {
        var builder = CreateBuilder(publicKeyPath, serviceKey);
        builder.WebHost.UseTestServer();
        builder.AddParkingSystemWebFoundation();
        builder.AddParkingSystemSecurity();
        builder.Services.AddHealthChecks();
        var app = builder.Build();
        app.UseParkingSystemWebFoundation();
        app.UseParkingSystemServiceSecurity();
        app.MapGet("/open", () => Results.Ok()).AllowAnonymous();
        app.MapGet("/protected", () => Results.Ok()).RequireAuthorization();
        app.MapHealthChecks("/health/live");
        app.MapHealthChecks("/health/ready");
        await app.StartAsync(TestContext.Current.CancellationToken);
        return app;
    }

    private static WebApplicationBuilder CreateBuilder(string publicKeyPath, string serviceKey)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development" });
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Security:Jwt:Issuer"] = "ParkingSystem.Identity",
            ["Security:Jwt:Audience"] = "ParkingSystem",
            ["Security:Jwt:PublicKeyPath"] = publicKeyPath,
            ["Security:Jwt:ClockSkewSeconds"] = "30",
            ["Security:Cors:AllowedOrigins"] = "",
            [$"{ServiceKeyOptions.SectionName}:ServiceKey"] = serviceKey
        });
        return builder;
    }

    private sealed class TempPublicKey : IDisposable
    {
        private readonly string directory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"svc-key-test-{Guid.NewGuid():N}");

        public TempPublicKey()
        {
            Directory.CreateDirectory(directory);
            using var rsa = RSA.Create(2048);
            Path = System.IO.Path.Combine(directory, "public.pem");
            File.WriteAllText(Path, rsa.ExportSubjectPublicKeyInfoPem());
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }
}
