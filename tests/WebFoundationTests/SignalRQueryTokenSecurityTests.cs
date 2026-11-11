using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ParkingSystem.AspNetCore;
using ParkingSystem.AspNetCore.Security;
using Xunit;

namespace ParkingSystem.Tests.WebFoundation;

public sealed class SignalRQueryTokenSecurityTests
{
    private const string Issuer = "ParkingSystem.Identity";
    private const string Audience = "ParkingSystem";

    [Theory]
    [InlineData("/hubs/parking")]
    [InlineData("/hubs/parking/negotiate")]
    [InlineData("/HUBS/parking")]
    public async Task Hub_paths_accept_valid_query_access_token_without_leaking_token_to_logs_or_traces(string path)
    {
        using var keys = new TestJwtKeys();
        var logs = new CapturingLoggerProvider();
        var capturedTraceData = new ConcurrentBag<string>();
        using var traceListener = new ActivityListener
        {
            ShouldListenTo = source => source.Name is "Microsoft.AspNetCore" or "System.Net.Http",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            SampleUsingParentId = (ref ActivityCreationOptions<string> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity => capturedTraceData.Add(string.Join(" ",
                new[] { activity.DisplayName }
                    .Concat(activity.Tags.Select(tag => $"{tag.Key}={tag.Value}"))
                    .Concat(activity.Baggage.Select(item => $"{item.Key}={item.Value}"))))
        };
        ActivitySource.AddActivityListener(traceListener);

        await using var app = await CreateSecurityAppAsync(keys.PublicKeyPath, logs);
        var token = CreateToken(keys.PrivateKey, subject: "11111111-1111-1111-1111-111111111111", role: "Staff");

        using var response = await app.GetTestClient().GetAsync(
            $"{path}?access_token={Uri.EscapeDataString(token)}",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var identity = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal("11111111-1111-1111-1111-111111111111", identity.GetProperty("sub").GetString());
        Assert.Equal("Staff", identity.GetProperty("role").GetString());
        Assert.True(identity.GetProperty("isAuthenticated").GetBoolean());

        var logText = string.Join(Environment.NewLine, logs.Messages);
        var traceText = string.Join(Environment.NewLine, capturedTraceData);
        Assert.DoesNotContain(token, logText, StringComparison.Ordinal);
        Assert.DoesNotContain("access_token=", logText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(token, traceText, StringComparison.Ordinal);
        Assert.DoesNotContain("access_token=", traceText, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("/api/parking/lots")]
    [InlineData("/api/protected")]
    [InlineData("/hubs-other")]
    [InlineData("/hubs")]
    public async Task Non_hub_paths_reject_valid_query_access_token(string path)
    {
        using var keys = new TestJwtKeys();
        await using var app = await CreateSecurityAppAsync(keys.PublicKeyPath);
        var token = CreateToken(keys.PrivateKey, subject: "11111111-1111-1111-1111-111111111111", role: "Staff");

        using var response = await app.GetTestClient().GetAsync(
            $"{path}?access_token={Uri.EscapeDataString(token)}",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("/hubs/parking")]
    [InlineData("/hubs/parking?access_token=")]
    [InlineData("/api/parking/lots")]
    [InlineData("/api/parking/lots?access_token=not-read-on-api")]
    public async Task Bearer_authorization_header_is_preserved_across_hub_and_api_paths(string pathAndQuery)
    {
        using var keys = new TestJwtKeys();
        await using var app = await CreateSecurityAppAsync(keys.PublicKeyPath);
        var token = CreateToken(keys.PrivateKey, subject: "22222222-2222-2222-2222-222222222222", role: "ParkingLotOwner");

        using var request = new HttpRequestMessage(HttpMethod.Get, pathAndQuery);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var response = await app.GetTestClient().SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var identity = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal("22222222-2222-2222-2222-222222222222", identity.GetProperty("sub").GetString());
        Assert.Equal("ParkingLotOwner", identity.GetProperty("role").GetString());
    }

    [Fact]
    public async Task Hub_path_rejects_expired_or_malformed_query_access_token_with_safe_401()
    {
        using var keys = new TestJwtKeys();
        var logs = new CapturingLoggerProvider();
        await using var app = await CreateSecurityAppAsync(keys.PublicKeyPath, logs);
        var expiredToken = CreateToken(
            keys.PrivateKey,
            subject: "33333333-3333-3333-3333-333333333333",
            role: "Staff",
            expiresAt: DateTimeOffset.UtcNow.AddMinutes(-5));

        foreach (var badToken in new[] { expiredToken, "malformed-hub-token" })
        {
            using var response = await app.GetTestClient().GetAsync(
                $"/hubs/parking?access_token={Uri.EscapeDataString(badToken)}",
                TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
            Assert.DoesNotContain(badToken, body, StringComparison.Ordinal);
            Assert.DoesNotContain("access_token", body, StringComparison.OrdinalIgnoreCase);
        }

        var logText = string.Join(Environment.NewLine, logs.Messages);
        Assert.DoesNotContain(expiredToken, logText, StringComparison.Ordinal);
        Assert.DoesNotContain("malformed-hub-token", logText, StringComparison.Ordinal);
        Assert.DoesNotContain("access_token=", logText, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<WebApplication> CreateSecurityAppAsync(
        string publicKeyPath,
        CapturingLoggerProvider? logs = null)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development" });
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Security:Jwt:Issuer"] = Issuer,
            ["Security:Jwt:Audience"] = Audience,
            ["Security:Jwt:PublicKeyPath"] = publicKeyPath,
            ["Security:Jwt:ClockSkewSeconds"] = "30"
        });
        builder.AddParkingSystemWebFoundation();
        builder.AddParkingSystemSecurity();
        builder.AddParkingSystemTelemetry("web-foundation-hub-security-test");
        if (logs is not null)
            builder.Logging.AddProvider(logs);

        var app = builder.Build();
        app.UseParkingSystemWebFoundation();
        app.UseParkingSystemAuthentication();

        IResult EchoUser(HttpContext context) => Results.Json(new
        {
            sub = context.User.FindFirst("sub")?.Value,
            role = context.User.FindFirst("role")?.Value,
            isAuthenticated = context.User.Identity?.IsAuthenticated ?? false
        });

        app.MapGet("/hubs/parking", EchoUser).RequireAuthorization();
        app.MapGet("/hubs/parking/negotiate", EchoUser).RequireAuthorization();
        app.MapGet("/hubs-other", EchoUser).RequireAuthorization();
        app.MapGet("/hubs", EchoUser).RequireAuthorization();
        app.MapGet("/api/parking/lots", EchoUser).RequireAuthorization();
        app.MapGet("/api/protected", EchoUser).RequireAuthorization();

        await app.StartAsync(TestContext.Current.CancellationToken);
        return app;
    }

    private static string CreateToken(
        RSA signingKey,
        string subject,
        string role,
        DateTimeOffset? expiresAt = null)
    {
        var expiration = expiresAt ?? DateTimeOffset.UtcNow.AddMinutes(5);
        var payload = new Dictionary<string, object>
        {
            ["iss"] = Issuer,
            ["aud"] = Audience,
            ["sub"] = subject,
            ["role"] = role,
            ["nbf"] = (expiresAt?.AddMinutes(-5) ?? DateTimeOffset.UtcNow.AddMinutes(-1)).ToUnixTimeSeconds(),
            ["exp"] = expiration.ToUnixTimeSeconds()
        };

        var headerBase64 = Base64Url(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { alg = "RS256", typ = "JWT" })));
        var payloadBase64 = Base64Url(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload)));
        var signingInput = $"{headerBase64}.{payloadBase64}";
        var signature = signingKey.SignData(Encoding.ASCII.GetBytes(signingInput), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        return $"{signingInput}.{Base64Url(signature)}";
    }

    private static string Base64Url(byte[] value) =>
        Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private sealed class TestJwtKeys : IDisposable
    {
        private readonly string directory = Path.Combine(Path.GetTempPath(), $"parking-hub-jwt-test-{Guid.NewGuid():N}");

        public TestJwtKeys()
        {
            Directory.CreateDirectory(directory);
            PrivateKey = RSA.Create(2048);
            PublicKeyPath = Path.Combine(directory, "public.pem");
            File.WriteAllText(PublicKeyPath, PrivateKey.ExportSubjectPublicKeyInfoPem());
        }

        public RSA PrivateKey { get; }
        public string PublicKeyPath { get; }

        public void Dispose()
        {
            PrivateKey.Dispose();
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }

    private sealed class CapturingLoggerProvider : ILoggerProvider
    {
        private readonly ConcurrentQueue<string> messages = new();
        public IReadOnlyCollection<string> Messages => messages.ToArray();
        public ILogger CreateLogger(string categoryName) => new CapturingLogger(categoryName, messages);
        public void Dispose() { }
    }

    private sealed class CapturingLogger(string categoryName, ConcurrentQueue<string> messages) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => EmptyScope.Instance;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            messages.Enqueue($"[{categoryName}][{logLevel}] {formatter(state, exception)} {exception}");
    }

    private sealed class EmptyScope : IDisposable
    {
        public static EmptyScope Instance { get; } = new();
        public void Dispose() { }
    }
}
