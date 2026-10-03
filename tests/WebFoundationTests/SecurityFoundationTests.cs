using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Grpc.Core;
using Grpc.Health.V1;
using Grpc.Net.Client;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ParkingSystem.AspNetCore;
using ParkingSystem.AspNetCore.Security;
using ParkingSystem.BuildingBlocks.Identity;
using ParkingSystem.Tests.WebFoundation.SecurityProbe;
using Xunit;
using Yarp.ReverseProxy.Configuration;

namespace ParkingSystem.Tests.WebFoundation;

public sealed class SecurityFoundationTests
{
    private const string Issuer = "ParkingSystem.Identity";
    private const string Audience = "ParkingSystem";

    [Fact]
    public async Task Jwt_validation_is_rs256_only_and_returns_safe_401_403_problem_details()
    {
        using var keys = new TestJwtKeys();
        using var otherKeys = new TestJwtKeys();
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
        var app = await CreateSecureApp(keys.PublicKeyPath, securityApp =>
        {
            securityApp.MapAuthenticatedGroup("/protected")
                .MapGet("", (ICurrentUser currentUser) => Results.Json(new
                {
                    currentUser.SubjectId,
                    currentUser.IsAuthenticated
                }));
            securityApp.MapGet("/forbidden", () => Results.Text("forbidden-content"))
                .RequireAuthorization(policy => policy.RequireAuthenticatedUser().RequireClaim("role", "admin"));
        }, logs);

        await using (app)
        {
            var client = app.GetTestClient();
            var invalidTokens = new[]
            {
                CreateToken(keys.PrivateKey, subject: "expired-subject", expiresAt: DateTimeOffset.UtcNow.AddMinutes(-2)),
                CreateToken(keys.PrivateKey, issuer: "wrong-issuer"),
                CreateToken(keys.PrivateKey, audience: "wrong-audience"),
                CreateToken(otherKeys.PrivateKey),
                "malformed-security-token",
                CreateHs256Token(),
                CreateNoAlgorithmToken(),
                CreateToken(keys.PrivateKey, includeExpiration: false)
            };

            using (var missing = await client.GetAsync("/protected", TestContext.Current.CancellationToken))
                await AssertSafeUnauthorized(missing, "missing-token");

            foreach (var token in invalidTokens)
            {
                using var request = BearerRequest("/protected", token);
                using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
                await AssertSafeUnauthorized(response, token);
            }

            var validToken = CreateToken(keys.PrivateKey, subject: "user-subject-01");
            using (var validRequest = BearerRequest("/protected", validToken))
            using (var validResponse = await client.SendAsync(validRequest, TestContext.Current.CancellationToken))
            {
                Assert.Equal(HttpStatusCode.OK, validResponse.StatusCode);
                var identity = await validResponse.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
                Assert.Equal("user-subject-01", identity.GetProperty("subjectId").GetString());
                Assert.True(identity.GetProperty("isAuthenticated").GetBoolean());
            }

            using (var forbiddenRequest = BearerRequest("/forbidden", validToken))
            using (var forbiddenResponse = await client.SendAsync(forbiddenRequest, TestContext.Current.CancellationToken))
            {
                var body = await forbiddenResponse.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
                Assert.Equal(HttpStatusCode.Forbidden, forbiddenResponse.StatusCode);
                Assert.Equal(403, body.GetProperty("status").GetInt32());
                Assert.Equal("Forbidden", body.GetProperty("title").GetString());
                Assert.Equal("auth.forbidden", body.GetProperty("errorCode").GetString());
                Assert.True(body.TryGetProperty("traceId", out _));
                Assert.True(body.TryGetProperty("correlationId", out _));
                Assert.DoesNotContain("forbidden-content", body.ToString(), StringComparison.Ordinal);
            }

            using var unknown = await client.GetAsync("/unmapped", TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
            Assert.DoesNotContain("SecurityToken", string.Join(Environment.NewLine, logs.Messages), StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Authorization: Bearer", string.Join(Environment.NewLine, logs.Messages), StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(validToken, string.Join(Environment.NewLine, logs.Messages), StringComparison.Ordinal);
            Assert.DoesNotContain(keys.PrivateKey.ExportPkcs8PrivateKeyPem(), string.Join(Environment.NewLine, logs.Messages), StringComparison.Ordinal);
            var traceText = string.Join(Environment.NewLine, capturedTraceData);
            Assert.DoesNotContain(validToken, traceText, StringComparison.Ordinal);
            Assert.DoesNotContain("user-subject-01", traceText, StringComparison.Ordinal);
            Assert.DoesNotContain("Authorization", traceText, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task Gateway_validates_and_forwards_authorization_for_downstream_revalidation()
    {
        using var keys = new TestJwtKeys();
        var gatewayLogs = new CapturingLoggerProvider();
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
        await using var destination = await CreateSecureDestination(keys.PublicKeyPath);
        var destinationAddress = destination.Services.GetRequiredService<IServer>()
            .Features.Get<IServerAddressesFeature>()!.Addresses.Single();

        var builder = CreateBuilder(keys.PublicKeyPath, allowedOrigins: "https://frontend.test");
        builder.WebHost.UseTestServer();
        builder.AddParkingSystemWebFoundation();
        builder.AddParkingSystemGatewaySecurity();
        builder.AddParkingSystemTelemetry("gateway-security-test");
        builder.Logging.AddProvider(gatewayLogs);
        builder.Services.AddReverseProxy().LoadFromMemory(
            [new RouteConfig
            {
                RouteId = "secured-test-route",
                ClusterId = "secured-test-cluster",
                Match = new RouteMatch { Path = "/proxy" },
                AuthorizationPolicy = ParkingSystemSecurityExtensions.AuthenticatedUserPolicy
            }],
            [new ClusterConfig
            {
                ClusterId = "secured-test-cluster",
                Destinations = new Dictionary<string, DestinationConfig>
                {
                    ["destination"] = new() { Address = destinationAddress }
                }
            }]);

        var gateway = builder.Build();
        gateway.UseParkingSystemWebFoundation();
        gateway.UseParkingSystemGatewaySecurity();
        gateway.MapReverseProxy();
        await gateway.StartAsync(TestContext.Current.CancellationToken);
        await using (gateway)
        {
            var client = gateway.GetTestClient();
            using (var anonymous = await client.GetAsync("/proxy", TestContext.Current.CancellationToken))
                Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);

            var token = CreateToken(keys.PrivateKey, subject: "gateway-subject");
            using var request = BearerRequest("/proxy", token);
            using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
            var result = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("gateway-subject", result.GetProperty("subjectId").GetString());
            Assert.True(result.GetProperty("authorizationHeaderValidated").GetBoolean());
            Assert.False(response.Headers.Contains("Authorization"));
            var logText = string.Join(Environment.NewLine, gatewayLogs.Messages);
            var traceText = string.Join(Environment.NewLine, capturedTraceData);
            Assert.DoesNotContain(token, logText, StringComparison.Ordinal);
            Assert.DoesNotContain("Authorization: Bearer", logText, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(token, traceText, StringComparison.Ordinal);
            Assert.DoesNotContain("gateway-subject", traceText, StringComparison.Ordinal);
            Assert.DoesNotContain("Authorization", traceText, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task Gateway_cors_is_origin_restricted_preflight_is_anonymous_and_security_headers_are_present()
    {
        using var keys = new TestJwtKeys();
        var builder = CreateBuilder(keys.PublicKeyPath, allowedOrigins: "https://frontend.test");
        builder.WebHost.UseTestServer();
        builder.AddParkingSystemWebFoundation();
        builder.AddParkingSystemGatewaySecurity();
        var app = builder.Build();
        app.UseParkingSystemWebFoundation();
        app.UseParkingSystemGatewaySecurity();
        app.MapAuthenticatedGroup("/secure")
            .MapGet("", () => Results.Ok());
        await app.StartAsync(TestContext.Current.CancellationToken);
        await using (app)
        {
            using var preflight = new HttpRequestMessage(HttpMethod.Options, "/secure");
            preflight.Headers.Add("Origin", "https://frontend.test");
            preflight.Headers.Add("Access-Control-Request-Method", "GET");
            preflight.Headers.Add("Access-Control-Request-Headers", "authorization,x-correlation-id,content-type");
            using var preflightResponse = await app.GetTestClient().SendAsync(preflight, TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.NoContent, preflightResponse.StatusCode);
            Assert.Equal("https://frontend.test", preflightResponse.Headers.GetValues("Access-Control-Allow-Origin").Single());
            Assert.Contains("authorization", preflightResponse.Headers.GetValues("Access-Control-Allow-Headers").Single(), StringComparison.OrdinalIgnoreCase);
            Assert.False(preflightResponse.Headers.Contains("Access-Control-Allow-Credentials"));
            AssertSecurityHeaders(preflightResponse);

            using var allowedRequest = new HttpRequestMessage(HttpMethod.Get, "/secure");
            allowedRequest.Headers.Add("Origin", "https://frontend.test");
            allowedRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(
                "Bearer", CreateToken(keys.PrivateKey, subject: "cors-subject"));
            using var allowedResponse = await app.GetTestClient().SendAsync(allowedRequest, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, allowedResponse.StatusCode);
            Assert.Contains("X-Correlation-ID", allowedResponse.Headers.GetValues("Access-Control-Expose-Headers").Single(), StringComparison.OrdinalIgnoreCase);
            AssertSecurityHeaders(allowedResponse);

            using var blocked = new HttpRequestMessage(HttpMethod.Get, "/secure");
            blocked.Headers.Add("Origin", "https://unlisted.test");
            using var blockedResponse = await app.GetTestClient().SendAsync(blocked, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.Unauthorized, blockedResponse.StatusCode);
            Assert.False(blockedResponse.Headers.Contains("Access-Control-Allow-Origin"));
            AssertSecurityHeaders(blockedResponse);
        }

        var emptyOriginsBuilder = CreateBuilder(keys.PublicKeyPath);
        emptyOriginsBuilder.WebHost.UseTestServer();
        emptyOriginsBuilder.AddParkingSystemWebFoundation();
        emptyOriginsBuilder.AddParkingSystemGatewaySecurity();
        var emptyOriginsApp = emptyOriginsBuilder.Build();
        emptyOriginsApp.UseParkingSystemWebFoundation();
        emptyOriginsApp.UseParkingSystemGatewaySecurity();
        await emptyOriginsApp.StartAsync(TestContext.Current.CancellationToken);
        await using (emptyOriginsApp)
        {
            using var preflight = new HttpRequestMessage(HttpMethod.Options, "/any-path");
            preflight.Headers.Add("Origin", "https://frontend.test");
            preflight.Headers.Add("Access-Control-Request-Method", "POST");
            using var response = await emptyOriginsApp.GetTestClient().SendAsync(preflight, TestContext.Current.CancellationToken);
            Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
        }
    }

    [Fact]
    public async Task Authenticated_grpc_test_service_accepts_bearer_metadata_while_health_stays_anonymous()
    {
        using var keys = new TestJwtKeys();
        var builder = CreateBuilder(keys.PublicKeyPath);
        builder.WebHost.UseTestServer();
        builder.AddParkingSystemWebFoundation();
        builder.AddParkingSystemSecurity();
        builder.AddParkingSystemTelemetry("security-test");
        builder.Services.AddGrpc();
        builder.Services.AddGrpcHealthChecks(options =>
                options.Services.Map("", check => check.Tags.Contains("grpc")))
            .AddCheck("grpc-server", () => Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy(), tags: ["grpc"]);
        var app = builder.Build();
        app.UseParkingSystemWebFoundation();
        app.UseParkingSystemAuthentication();
        app.MapGrpcService<TestSecurityProbeService>();
        app.MapGrpcHealthChecksService();
        await app.StartAsync(TestContext.Current.CancellationToken);
        await using (app)
        {
            using var channel = GrpcChannel.ForAddress("http://localhost", new GrpcChannelOptions
            {
                HttpHandler = app.GetTestServer().CreateHandler()
            });
            var grpcClient = new global::ParkingSystem.Tests.WebFoundation.SecurityProbe.SecurityProbe.SecurityProbeClient(channel);

            var unauthenticated = await Assert.ThrowsAsync<RpcException>(async () =>
                await grpcClient.PingAsync(new ProbeRequest(), cancellationToken: TestContext.Current.CancellationToken));
            Assert.Equal(StatusCode.Unauthenticated, unauthenticated.StatusCode);

            var token = CreateToken(keys.PrivateKey, subject: "grpc-subject");
            var metadata = new Metadata { { "authorization", $"Bearer {token}" } };
            var reply = await grpcClient.PingAsync(new ProbeRequest(), metadata, cancellationToken: TestContext.Current.CancellationToken);
            Assert.Equal("grpc-subject", reply.SubjectId);

            var healthClient = new Health.HealthClient(channel);
            var grpcHealth = await healthClient.CheckAsync(new HealthCheckRequest(), cancellationToken: TestContext.Current.CancellationToken);
            Assert.Equal(HealthCheckResponse.Types.ServingStatus.Serving, grpcHealth.Status);
        }
    }

    [Fact]
    public async Task Missing_or_invalid_public_key_fails_startup_without_logging_key_contents()
    {
        using var keys = new TestJwtKeys();
        var invalidPath = Path.Combine(Path.GetTempPath(), $"invalid-jwt-{Guid.NewGuid():N}.pem");
        var missingPath = Path.Combine(Path.GetTempPath(), $"missing-jwt-{Guid.NewGuid():N}.pem");
        File.WriteAllText(invalidPath, keys.PrivateKey.ExportPkcs8PrivateKeyPem());
        try
        {
            foreach (var keyPath in new[] { missingPath, invalidPath })
            {
                var builder = CreateBuilder(keyPath);
                builder.WebHost.UseTestServer();
                builder.AddParkingSystemWebFoundation();
                builder.AddParkingSystemSecurity();
                var app = builder.Build();

                var exception = await Assert.ThrowsAnyAsync<Exception>(() => app.StartAsync(TestContext.Current.CancellationToken));
                Assert.Contains("JWT public-key configuration is invalid", exception.ToString(), StringComparison.Ordinal);
                Assert.DoesNotContain(keys.PrivateKey.ExportPkcs8PrivateKeyPem(), exception.ToString(), StringComparison.Ordinal);
                await app.DisposeAsync();
            }
        }
        finally
        {
            File.Delete(invalidPath);
        }
    }

    private static async Task AssertSafeUnauthorized(HttpResponseMessage response, string sensitiveValue)
    {
        var bodyText = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        using var body = JsonDocument.Parse(bodyText);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(401, body.RootElement.GetProperty("status").GetInt32());
        Assert.Equal("Unauthorized", body.RootElement.GetProperty("title").GetString());
        Assert.Equal("auth.unauthorized", body.RootElement.GetProperty("errorCode").GetString());
        Assert.True(body.RootElement.TryGetProperty("traceId", out _));
        Assert.True(body.RootElement.TryGetProperty("correlationId", out _));
        Assert.Contains(response.Headers.WwwAuthenticate, header => header.Scheme == "Bearer");
        Assert.DoesNotContain(sensitiveValue, bodyText, StringComparison.Ordinal);
        Assert.DoesNotContain("IDX", bodyText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SecurityToken", bodyText, StringComparison.OrdinalIgnoreCase);
        response.Dispose();
    }

    private static async Task<WebApplication> CreateSecureApp(string publicKeyPath, Action<WebApplication> mapEndpoints, CapturingLoggerProvider? logs = null)
    {
        var builder = CreateBuilder(publicKeyPath);
        builder.WebHost.UseTestServer();
        builder.AddParkingSystemWebFoundation();
        builder.AddParkingSystemSecurity();
        if (logs is not null)
            builder.Logging.AddProvider(logs);
        var app = builder.Build();
        app.UseParkingSystemWebFoundation();
        app.UseParkingSystemAuthentication();
        mapEndpoints(app);
        await app.StartAsync(TestContext.Current.CancellationToken);
        return app;
    }

    private static async Task<WebApplication> CreateSecureDestination(string publicKeyPath)
    {
        var builder = CreateBuilder(publicKeyPath);
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.AddParkingSystemWebFoundation();
        builder.AddParkingSystemSecurity();
        var app = builder.Build();
        app.UseParkingSystemWebFoundation();
        app.UseParkingSystemAuthentication();
        app.MapGet("/proxy", (HttpContext context, ICurrentUser currentUser) => Results.Json(new
        {
            currentUser.SubjectId,
            authorizationHeaderValidated = context.Request.Headers.ContainsKey("Authorization")
        })).RequireAuthenticatedUser();
        await app.StartAsync(TestContext.Current.CancellationToken);
        return app;
    }

    private static WebApplicationBuilder CreateBuilder(string publicKeyPath, string allowedOrigins = "")
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development" });
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Security:Jwt:Issuer"] = Issuer,
            ["Security:Jwt:Audience"] = Audience,
            ["Security:Jwt:PublicKeyPath"] = publicKeyPath,
            ["Security:Jwt:ClockSkewSeconds"] = "30",
            ["Security:Cors:AllowedOrigins"] = allowedOrigins
        });
        return builder;
    }

    private static void AssertSecurityHeaders(HttpResponseMessage response)
    {
        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("no-referrer", response.Headers.GetValues("Referrer-Policy").Single());
        Assert.Equal("DENY", response.Headers.GetValues("X-Frame-Options").Single());
        Assert.False(response.Headers.Contains("Strict-Transport-Security"));
        Assert.False(response.Headers.Contains("Content-Security-Policy"));
    }

    private static HttpRequestMessage BearerRequest(string path, string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        return request;
    }

    private static string CreateToken(RSA signingKey, string issuer = Issuer, string audience = Audience, string subject = "test-subject", DateTimeOffset? expiresAt = null, bool includeExpiration = true)
    {
        var expiration = expiresAt ?? DateTimeOffset.UtcNow.AddMinutes(5);
        var payload = new Dictionary<string, object>
        {
            ["iss"] = issuer,
            ["aud"] = audience,
            ["sub"] = subject,
            ["nbf"] = (expiresAt?.AddMinutes(-5) ?? DateTimeOffset.UtcNow.AddMinutes(-1)).ToUnixTimeSeconds()
        };
        if (includeExpiration)
            payload["exp"] = expiration.ToUnixTimeSeconds();

        return SignToken("RS256", JsonSerializer.Serialize(new { alg = "RS256", typ = "JWT" }), JsonSerializer.Serialize(payload), signingKey);
    }

    private static string CreateHs256Token()
    {
        var payload = JsonSerializer.Serialize(new
        {
            iss = Issuer,
            aud = Audience,
            sub = "hmac-subject",
            exp = DateTimeOffset.UtcNow.AddMinutes(5).ToUnixTimeSeconds()
        });
        var input = $"{Base64Url(Encoding.UTF8.GetBytes("{\"alg\":\"HS256\",\"typ\":\"JWT\"}"))}.{Base64Url(Encoding.UTF8.GetBytes(payload))}";
        var signature = new HMACSHA256(Encoding.UTF8.GetBytes("not-a-production-secret"))
            .ComputeHash(Encoding.ASCII.GetBytes(input));
        return $"{input}.{Base64Url(signature)}";
    }

    private static string CreateNoAlgorithmToken()
    {
        var payload = JsonSerializer.Serialize(new
        {
            iss = Issuer,
            aud = Audience,
            sub = "none-subject",
            exp = DateTimeOffset.UtcNow.AddMinutes(5).ToUnixTimeSeconds()
        });
        return $"{Base64Url(Encoding.UTF8.GetBytes("{\"alg\":\"none\",\"typ\":\"JWT\"}"))}.{Base64Url(Encoding.UTF8.GetBytes(payload))}.";
    }

    private static string SignToken(string algorithm, string headerJson, string payloadJson, RSA key)
    {
        var signingInput = $"{Base64Url(Encoding.UTF8.GetBytes(headerJson))}.{Base64Url(Encoding.UTF8.GetBytes(payloadJson))}";
        var signature = key.SignData(Encoding.ASCII.GetBytes(signingInput), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        return $"{signingInput}.{Base64Url(signature)}";
    }

    private static string Base64Url(byte[] value) =>
        Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private sealed class TestJwtKeys : IDisposable
    {
        private readonly string directory = Path.Combine(Path.GetTempPath(), $"parking-jwt-test-{Guid.NewGuid():N}");

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

    private sealed class TestSecurityProbeService : global::ParkingSystem.Tests.WebFoundation.SecurityProbe.SecurityProbe.SecurityProbeBase
    {
        [Authorize(Policy = ParkingSystemSecurityExtensions.AuthenticatedUserPolicy)]
        public override Task<ProbeReply> Ping(ProbeRequest request, ServerCallContext context) =>
            Task.FromResult(new ProbeReply { SubjectId = context.GetHttpContext().User.FindFirst("sub")?.Value ?? string.Empty });
    }

    private sealed class CapturingLoggerProvider : ILoggerProvider
    {
        private readonly ConcurrentQueue<string> messages = new();
        public IReadOnlyCollection<string> Messages => messages.ToArray();
        public ILogger CreateLogger(string categoryName) => new CapturingLogger(messages);
        public void Dispose() { }
    }

    private sealed class CapturingLogger(ConcurrentQueue<string> messages) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => EmptyScope.Instance;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            messages.Enqueue($"{formatter(state, exception)} {exception}");
    }

    private sealed class EmptyScope : IDisposable
    {
        public static EmptyScope Instance { get; } = new();
        public void Dispose() { }
    }
}
