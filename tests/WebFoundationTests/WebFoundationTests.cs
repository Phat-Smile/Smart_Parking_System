using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Collections.Concurrent;
using System.Diagnostics;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using Microsoft.Extensions.Options;
using ParkingSystem.AspNetCore;
using ParkingSystem.BuildingBlocks.Correlation;
using ParkingSystem.BuildingBlocks.Results;
using Xunit;
using Yarp.ReverseProxy.Configuration;

namespace ParkingSystem.Tests.WebFoundation;

public sealed class WebFoundationTests
{
    [Fact]
    public void Console_logging_defaults_to_json_without_scopes()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development" });
        builder.AddParkingSystemWebFoundation();
        using var app = builder.Build();

        Assert.Equal(ConsoleFormatterNames.Json, app.Services.GetRequiredService<IOptions<ConsoleLoggerOptions>>().Value.FormatterName);
        Assert.False(app.Services.GetRequiredService<IOptionsMonitor<JsonConsoleFormatterOptions>>()
            .Get(ConsoleFormatterNames.Json).IncludeScopes);
    }

    [Fact]
    public void Launch_profiles_can_switch_console_logging_to_simple_without_scopes()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development" });
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            [ParkingSystemWebFoundationExtensions.ConsoleFormatterNameKey] = "simple"
        });
        builder.AddParkingSystemWebFoundation();
        using var app = builder.Build();

        Assert.Equal(ConsoleFormatterNames.Simple, app.Services.GetRequiredService<IOptions<ConsoleLoggerOptions>>().Value.FormatterName);
        Assert.False(app.Services.GetRequiredService<IOptionsMonitor<SimpleConsoleFormatterOptions>>()
            .Get(ConsoleFormatterNames.Simple).IncludeScopes);
    }

    [Fact]
    public async Task Valid_correlation_id_is_preserved_in_response_and_problem_details()
    {
        var app = await CreateApp(app => app.MapGet("/error", ThrowUnexpected));
        await using (app)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "/error");
            request.Headers.Add("X-Correlation-ID", "A2B4D30E-6990-4261-BD7F-9F0A8D5B0E16");

            using var response = await app.GetTestClient().SendAsync(request, TestContext.Current.CancellationToken);
            var problem = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
            Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
            Assert.Equal("A2B4D30E-6990-4261-BD7F-9F0A8D5B0E16", response.Headers.GetValues("X-Correlation-ID").Single());
            Assert.Equal("Đã xảy ra lỗi hệ thống.", problem.GetProperty("title").GetString());
            Assert.Equal("A2B4D30E-6990-4261-BD7F-9F0A8D5B0E16", problem.GetProperty("correlationId").GetString(), ignoreCase: true);
            Assert.Equal("internal.unexpected", problem.GetProperty("errorCode").GetString());
            Assert.False(problem.TryGetProperty("stackTrace", out _));
            Assert.DoesNotContain("private-password-value", problem.ToString(), StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task Business_problem_has_the_same_request_extensions_as_foundation_errors()
    {
        var app = await CreateApp(app => app.MapGet("/conflict", (HttpContext context) =>
            BusinessProblem.Create(context, StatusCodes.Status409Conflict, new Error("sample.conflict", "Sample conflict."))));
        await using (app)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "/conflict");
            request.Headers.Add("X-Correlation-ID", "6F1B2C3D-4E5F-4A6B-8C7D-9E0F1A2B3C4D");

            using var response = await app.GetTestClient().SendAsync(request, TestContext.Current.CancellationToken);
            var problem = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
            Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
            Assert.Equal("Sample conflict.", problem.GetProperty("title").GetString());
            Assert.Equal("/conflict", problem.GetProperty("instance").GetString());
            Assert.Equal("sample.conflict", problem.GetProperty("errorCode").GetString());
            Assert.Equal("6F1B2C3D-4E5F-4A6B-8C7D-9E0F1A2B3C4D", problem.GetProperty("correlationId").GetString(), ignoreCase: true);
            Assert.False(string.IsNullOrEmpty(problem.GetProperty("traceId").GetString()));
        }
    }

    [Theory]
    [InlineData("/api/identity/accounts/me/media", "identity.media_file_required", "Media file is required.")]
    [InlineData("/api/identity/accounts/me/media", "identity.invalid_customer_media", "Invalid customer media.")]
    [InlineData("/api/notification/me", "notification.invalid_paging", "Invalid paging parameters.")]
    [InlineData("/api/parking/lots/arrival-availability", "parking.arrival_availability.invalid_parameters", "Invalid lotId, arrivalAt or vehicleType.")]
    [InlineData("/api/identity/accounts/me/vehicles/11111111-1111-1111-1111-111111111111", "identity.bicycle_photo_required", "Bicycle photo is required.")]
    public async Task Standardized_bad_request_endpoints_conform_to_rfc7807_problem_details(string path, string errorCode, string message)
    {
        var app = await CreateApp(app => app.MapGet(path, (HttpContext context) =>
            BusinessProblem.Create(context, StatusCodes.Status400BadRequest, new Error(errorCode, message))));
        await using (app)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, path);
            request.Headers.Add("X-Correlation-ID", "6F1B2C3D-4E5F-4A6B-8C7D-9E0F1A2B3C4D");

            using var response = await app.GetTestClient().SendAsync(request, TestContext.Current.CancellationToken);
            var problem = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
            Assert.Equal("about:blank", problem.GetProperty("type").GetString());
            Assert.Equal(400, problem.GetProperty("status").GetInt32());
            Assert.Equal(message, problem.GetProperty("title").GetString());
            Assert.Equal(path, problem.GetProperty("instance").GetString());
            Assert.Equal(errorCode, problem.GetProperty("errorCode").GetString());
            Assert.Equal("6F1B2C3D-4E5F-4A6B-8C7D-9E0F1A2B3C4D", problem.GetProperty("correlationId").GetString(), ignoreCase: true);
            Assert.False(string.IsNullOrEmpty(problem.GetProperty("traceId").GetString()));
        }
    }

    [Fact]
    public async Task Missing_malformed_oversized_and_multi_value_ids_are_replaced_with_valid_guids()
    {
        var app = await CreateApp(app => app.MapGet("/id", (HttpContext context) => context.Request.Headers["X-Correlation-ID"].ToString()));
        await using (app)
        {
            var client = app.GetTestClient();
            foreach (var request in new[]
            {
                new HttpRequestMessage(HttpMethod.Get, "/id"),
                WithHeader("not-a-guid"),
                WithHeader(new string('a', 129)),
                WithMultipleHeaders("2D7AFBC7-6256-4A1C-A718-2E9509650427", "C3E4CD41-01B0-487A-8DCB-7D785F08972E")
            })
            {
                using (request)
                using (var response = await client.SendAsync(request, TestContext.Current.CancellationToken))
                {
                    var id = response.Headers.GetValues("X-Correlation-ID").Single();
                    Assert.True(Guid.TryParse(id, out var parsed));
                    Assert.NotEqual(Guid.Empty, parsed);
                    Assert.Equal(id, (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).Trim('"'));
                }
            }
        }
    }

    [Fact]
    public async Task Validation_errors_are_safe_field_errors_without_attempted_values()
    {
        var app = await CreateApp(app => app.MapGet("/invalid", ThrowValidation));
        await using (app)
        {
            using var response = await app.GetTestClient().GetAsync("/invalid", TestContext.Current.CancellationToken);
            var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
            Assert.Contains("request.validation_failed", body, StringComparison.Ordinal);
            Assert.True(body.Contains("password", StringComparison.OrdinalIgnoreCase), $"Validation response: {body}");
            Assert.DoesNotContain("private-password-value", body, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task Validation_problem_has_a_fixed_title_and_one_message_per_field()
    {
        var app = await CreateApp(application => application.MapGet("/invalid-fields", ThrowFieldValidation));
        await using (app)
        {
            using var response = await app.GetTestClient().GetAsync("/invalid-fields", TestContext.Current.CancellationToken);
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
            var root = json.RootElement;

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal("Dữ liệu không hợp lệ.", root.GetProperty("title").GetString());
            Assert.Equal("Email không đúng định dạng hợp lệ. Mật khẩu phải có ít nhất 8 ký tự.", root.GetProperty("detail").GetString());
            // A one-letter input ("a") must not hide a message that happens to contain that letter.
            Assert.Equal("Email không đúng định dạng hợp lệ.", root.GetProperty("errors").GetProperty("Email").EnumerateArray().Single().GetString());
            Assert.Equal("Mật khẩu phải có ít nhất 8 ký tự.", root.GetProperty("errors").GetProperty("Password").EnumerateArray().Single().GetString());
        }
    }

    [Fact]
    public async Task Concurrent_requests_keep_correlation_ids_isolated()
    {
        var app = await CreateApp(application => application.MapGet("/id", (HttpContext context) => context.Request.Headers["X-Correlation-ID"].ToString()));
        await using (app)
        {
            var client = app.GetTestClient();
            var firstId = "A26B0A6B-A0A9-4668-8D58-F314A7A99A90";
            var secondId = "58BF2B4D-74DD-4C7B-80E8-E62530A05D17";
            var responses = await Task.WhenAll(SendWithId(client, firstId), SendWithId(client, secondId));

            Assert.Equal(firstId, responses[0]);
            Assert.Equal(secondId, responses[1]);
        }
    }

    [Fact]
    public async Task Existing_correlation_context_is_available_from_request_scoped_di()
    {
        var app = await CreateApp(application => application.MapGet("/context", (CorrelationContext context) => context.CorrelationId));
        await using (app)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "/context");
            request.Headers.Add("X-Correlation-ID", "2CF64A6F-5A31-43C7-9A2A-88F160DA6B2B");
            using var response = await app.GetTestClient().SendAsync(request, TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(Guid.Parse("2CF64A6F-5A31-43C7-9A2A-88F160DA6B2B"),
                await response.Content.ReadFromJsonAsync<Guid>(TestContext.Current.CancellationToken));
        }
    }

    [Fact]
    public async Task Request_logging_uses_route_templates_without_identifiers_queries_or_authorization()
    {
        const string routeValue = "SENSITIVE_ROUTE_VALUE_12345";
        const string queryValue = "SENSITIVE_QUERY_VALUE_12345";
        const string bearerValue = "SENSITIVE_AUTH_VALUE_12345";
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development" });
        builder.WebHost.UseTestServer();
        builder.AddParkingSystemWebFoundation();
        var capture = new CapturingLoggerProvider();
        builder.Logging.AddProvider(capture);
        var app = builder.Build();
        Assert.False(app.Services.GetRequiredService<IOptionsMonitor<JsonConsoleFormatterOptions>>()
            .Get(ConsoleFormatterNames.Json).IncludeScopes);
        app.UseParkingSystemWebFoundation();
        app.MapGet("/privacy/{identifier}", (string identifier) => Results.Ok());
        await app.StartAsync(TestContext.Current.CancellationToken);

        await using (app)
        {
            using var matched = new HttpRequestMessage(HttpMethod.Get, $"/privacy/{routeValue}?query={queryValue}");
            matched.Headers.TryAddWithoutValidation("Authorization", $"Bearer {bearerValue}");
            using var matchedResponse = await app.GetTestClient().SendAsync(matched, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, matchedResponse.StatusCode);

            using var unmatchedResponse = await app.GetTestClient().GetAsync(
                $"/missing/{routeValue}?query={queryValue}", TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.NotFound, unmatchedResponse.StatusCode);

            var requestLogs = capture.Messages.Where(message =>
                message.Contains("HTTP request completed.", StringComparison.Ordinal)).ToArray();
            Assert.Equal(2, requestLogs.Length);
            Assert.Contains(requestLogs, message => message.Contains("Path=/privacy/{identifier}", StringComparison.Ordinal));
            Assert.Contains(requestLogs, message => message.Contains("Path=<unmatched>", StringComparison.Ordinal));
            Assert.All(requestLogs, message =>
            {
                Assert.DoesNotContain(routeValue, message, StringComparison.Ordinal);
                Assert.DoesNotContain(queryValue, message, StringComparison.Ordinal);
                Assert.DoesNotContain(bearerValue, message, StringComparison.Ordinal);
                Assert.Contains("Method=GET", message, StringComparison.Ordinal);
                Assert.Contains("StatusCode=", message, StringComparison.Ordinal);
                Assert.Contains("CorrelationId=", message, StringComparison.Ordinal);
                Assert.Contains("TraceId=", message, StringComparison.Ordinal);
                Assert.Contains("SpanId=", message, StringComparison.Ordinal);
                Assert.Contains("TraceIdentifier=", message, StringComparison.Ordinal);
            });
        }
    }

    [Fact]
    public async Task Internal_exception_diagnostic_redacts_configured_secrets()
    {
        const string configuredSecret = "private-password-value";
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development" });
        builder.WebHost.UseTestServer();
        builder.Configuration["Credentials:DatabasePassword"] = configuredSecret;
        builder.AddParkingSystemWebFoundation();
        var capture = new CapturingLoggerProvider();
        builder.Logging.AddProvider(capture);
        var app = builder.Build();
        app.UseParkingSystemWebFoundation();
        app.MapGet("/error", ThrowUnexpected);
        await app.StartAsync(TestContext.Current.CancellationToken);
        await using (app)
        {
            using var response = await app.GetTestClient().GetAsync("/error", TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
            Assert.DoesNotContain(capture.Messages, message => message.Contains(configuredSecret, StringComparison.Ordinal));
            Assert.Contains(capture.Messages, message => message.Contains("ExceptionDetails=", StringComparison.Ordinal));
        }
    }

    [Fact]
    public async Task Yarp_forwards_the_valid_correlation_header_to_the_destination()
    {
        await using var destination = await StartDestination();
        var destinationAddress = destination.Services.GetRequiredService<IServer>()
            .Features.Get<IServerAddressesFeature>()!.Addresses.Single();

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development" });
        builder.WebHost.UseTestServer();
        builder.AddParkingSystemWebFoundation();
        builder.AddParkingSystemTelemetry("test-gateway");
        builder.Services.AddReverseProxy().LoadFromMemory(
            [new RouteConfig
            {
                RouteId = "test-route",
                ClusterId = "test-cluster",
                Match = new RouteMatch { Path = "/proxy" }
            }, new RouteConfig
            {
                RouteId = "trace-route",
                ClusterId = "test-cluster",
                Match = new RouteMatch { Path = "/proxy/trace" }
            }, new RouteConfig
            {
                RouteId = "problem-route",
                ClusterId = "test-cluster",
                Match = new RouteMatch { Path = "/proxy/problem" }
            }, new RouteConfig
            {
                RouteId = "failure-route",
                ClusterId = "failed-cluster",
                Match = new RouteMatch { Path = "/proxy/failure" }
            }],
            [new ClusterConfig
            {
                ClusterId = "test-cluster",
                Destinations = new Dictionary<string, DestinationConfig>
                {
                    ["destination"] = new() { Address = destinationAddress }
                }
            }, new ClusterConfig
            {
                ClusterId = "failed-cluster",
                Destinations = new Dictionary<string, DestinationConfig>
                {
                    ["unavailable"] = new() { Address = "http://127.0.0.1:1/" }
                }
            }]);
        var proxy = builder.Build();
        proxy.UseParkingSystemWebFoundation();
        proxy.Use(async (context, next) =>
        {
            var activity = Activity.Current;
            context.Response.OnStarting(() =>
            {
                if (activity is not null)
                {
                    context.Response.Headers["X-Test-Gateway-TraceId"] = activity.TraceId.ToString();
                    context.Response.Headers["X-Test-Gateway-SpanId"] = activity.SpanId.ToString();
                    context.Response.Headers["X-Test-Gateway-ParentSpanId"] = activity.ParentSpanId.ToString();
                }
                return Task.CompletedTask;
            });
            await next(context);
        });
        proxy.MapReverseProxy();
        await proxy.StartAsync(TestContext.Current.CancellationToken);
        await using (proxy)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "/proxy");
            request.Headers.Add("X-Correlation-ID", "6B0D3575-9F7F-4366-9C00-5F606E694331");
            using var response = await proxy.GetTestClient().SendAsync(request, TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("6B0D3575-9F7F-4366-9C00-5F606E694331", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
            Assert.Equal("6B0D3575-9F7F-4366-9C00-5F606E694331", response.Headers.GetValues("X-Correlation-ID").Single());

            const string externalTraceId = "4bf92f3577b34da6a3ce929d0e0e4736";
            const string externalSpanId = "00f067aa0ba902b7";
            const string correlationId = "A2B4D30E-6990-4261-BD7F-9F0A8D5B0E16";
            var outgoingSpans = new ConcurrentBag<(string SpanId, string ParentSpanId)>();
            using var activityListener = new ActivityListener
            {
                ShouldListenTo = source => source.Name == "System.Net.Http",
                Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
                SampleUsingParentId = (ref ActivityCreationOptions<string> _) => ActivitySamplingResult.AllData,
                ActivityStopped = activity =>
                {
                    if (activity.TraceId.ToString() == externalTraceId)
                        outgoingSpans.Add((activity.SpanId.ToString(), activity.ParentSpanId.ToString()));
                }
            };
            ActivitySource.AddActivityListener(activityListener);
            using var traceRequest = new HttpRequestMessage(HttpMethod.Get, "/proxy/trace");
            traceRequest.Headers.TryAddWithoutValidation("traceparent", $"00-{externalTraceId}-{externalSpanId}-01");
            traceRequest.Headers.Add("X-Correlation-ID", correlationId);
            using var traceResponse = await proxy.GetTestClient().SendAsync(traceRequest, TestContext.Current.CancellationToken);
            var downstreamTrace = await traceResponse.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.OK, traceResponse.StatusCode);
            Assert.Equal(externalTraceId, traceResponse.Headers.GetValues("X-Test-Gateway-TraceId").Single());
            var gatewaySpanId = traceResponse.Headers.GetValues("X-Test-Gateway-SpanId").Single();
            Assert.Equal(externalSpanId, traceResponse.Headers.GetValues("X-Test-Gateway-ParentSpanId").Single());
            Assert.Equal(externalTraceId, downstreamTrace.GetProperty("traceId").GetString());
            var yarpClientSpan = Assert.Single(outgoingSpans);
            Assert.Equal(gatewaySpanId, yarpClientSpan.ParentSpanId);
            Assert.Equal(yarpClientSpan.SpanId, downstreamTrace.GetProperty("parentSpanId").GetString());
            Assert.NotEqual(externalTraceId, Guid.Parse(correlationId).ToString("N"));
            Assert.Equal(correlationId, downstreamTrace.GetProperty("correlationId").GetString(), ignoreCase: true);

            using var downstreamProblem = await proxy.GetTestClient().GetAsync("/proxy/problem", TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.Conflict, downstreamProblem.StatusCode);
            Assert.Equal("application/problem+json", downstreamProblem.Content.Headers.ContentType?.MediaType);
            Assert.Equal("{\"title\":\"downstream-safe-detail\"}", await downstreamProblem.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

            using var proxyFailure = await proxy.GetTestClient().GetAsync("/proxy/failure", TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.BadGateway, proxyFailure.StatusCode);
            Assert.Equal("application/problem+json", proxyFailure.Content.Headers.ContentType?.MediaType);
            var failureProblem = await proxyFailure.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
            Assert.Equal("internal.unexpected", failureProblem.GetProperty("errorCode").GetString());
            Assert.False(failureProblem.TryGetProperty("exception", out _));
        }
    }

    private static async Task<WebApplication> CreateApp(Action<WebApplication> configureRoutes)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development" });
        builder.WebHost.UseTestServer();
        builder.AddParkingSystemWebFoundation();
        var app = builder.Build();
        app.UseParkingSystemWebFoundation();
        configureRoutes(app);
        await app.StartAsync(TestContext.Current.CancellationToken);
        return app;
    }

    private static async Task<WebApplication> StartDestination()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        var app = builder.Build();
        app.MapGet("/proxy", (HttpContext context) => Results.Text(context.Request.Headers["X-Correlation-ID"].ToString()));
        app.MapGet("/proxy/trace", (HttpContext context) => Results.Json(new
        {
            traceId = Activity.Current?.TraceId.ToString(),
            spanId = Activity.Current?.SpanId.ToString(),
            parentSpanId = Activity.Current?.ParentSpanId.ToString(),
            correlationId = context.Request.Headers["X-Correlation-ID"].ToString()
        }));
        app.MapGet("/proxy/problem", async (HttpContext context) =>
        {
            context.Response.StatusCode = StatusCodes.Status409Conflict;
            context.Response.ContentType = "application/problem+json";
            await context.Response.WriteAsync("{\"title\":\"downstream-safe-detail\"}", context.RequestAborted);
        });
        await app.StartAsync(TestContext.Current.CancellationToken);
        return app;
    }

    private static HttpRequestMessage WithHeader(string value)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/id");
        request.Headers.Add("X-Correlation-ID", value);
        return request;
    }

    private static HttpRequestMessage WithMultipleHeaders(string first, string second)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/id");
        request.Headers.TryAddWithoutValidation("X-Correlation-ID", new[] { first, second });
        return request;
    }

    private static async Task<string> SendWithId(HttpClient client, string id)
    {
        using var request = WithHeader(id);
        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        return response.Headers.GetValues("X-Correlation-ID").Single();
    }

    private static Task<IResult> ThrowUnexpected() => throw new InvalidOperationException("private-password-value");

    private static Task<IResult> ThrowFieldValidation() => throw new ValidationException(
    [
        new ValidationFailure("Email", "Email không đúng định dạng hợp lệ.") { AttemptedValue = "a" },
        new ValidationFailure("Password", "Mật khẩu phải có ít nhất 8 ký tự.") { AttemptedValue = "8" },
        new ValidationFailure("Password", "Mật khẩu phải chứa ít nhất một chữ cái viết hoa.") { AttemptedValue = "8" }
    ]);

    private static Task<IResult> ThrowValidation() => throw new ValidationException(
    [new ValidationFailure("Password", "private-password-value") { AttemptedValue = "private-password-value" }]);

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
            messages.Enqueue(formatter(state, exception));
    }

    private sealed class EmptyScope : IDisposable
    {
        public static EmptyScope Instance { get; } = new();
        public void Dispose() { }
    }
}
