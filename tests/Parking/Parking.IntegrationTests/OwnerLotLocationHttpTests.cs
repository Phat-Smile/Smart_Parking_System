using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.DataProtection;
using ParkingSystem.AspNetCore;
using ParkingSystem.BuildingBlocks.Identity;
using ParkingSystem.Parking.Application;
using ParkingSystem.Parking.Application.ParkingLots;
using ParkingSystem.Parking.Persistence;
using ParkingSystem.Parking.WebAPI.Endpoints;
using ParkingSystem.Parking.WebAPI.Geocoding;
using ParkingSystem.Parking.WebAPI.OwnerLocation;
using ParkingSystem.Parking.WebAPI.Security;
using ParkingSystem.Testing;
using Xunit;

namespace ParkingSystem.Tests.Parking.IntegrationTests;

// These tests replace authentication ONLY inside the in-memory ASP.NET TestServer.
// Neither the real Parking API nor its JWT configuration is modified.
public sealed class OwnerLotLocationHttpTests
{
    private static readonly Guid OwnerId = Guid.Parse("a1111111-1111-4111-8111-111111111111");
    private const string ConfirmUrl = "/api/parking/owner/locations/confirm";
    private const string ResolveUrl = "/api/parking/owner/locations/resolve";

    [Fact]
    public async Task Both_owner_location_endpoints_reject_anonymous_requests()
    {
        await using var app = await CreateAppAsync();
        using var client = app.GetTestClient();
        var ct = TestContext.Current.CancellationToken;

        using var confirm = await OwnerLocationHttpTestHost.SendAsync(client, HttpMethod.Post, ConfirmUrl,
            null, null, ValidPin(), ct);
        using var resolve = await OwnerLocationHttpTestHost.SendAsync(client, HttpMethod.Get,
            ResolveUrl + "?address=2%20Hai%20Trieu", null, null, null, ct);
        Assert.Equal(HttpStatusCode.Unauthorized, confirm.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, resolve.StatusCode);
    }

    [Theory]
    [InlineData("Customer")]
    [InlineData("SystemAdmin")]
    public async Task Non_owner_roles_are_forbidden(string role)
    {
        await using var app = await CreateAppAsync();
        using var client = app.GetTestClient();
        var ct = TestContext.Current.CancellationToken;

        using var confirm = await OwnerLocationHttpTestHost.SendAsync(client, HttpMethod.Post, ConfirmUrl,
            role, OwnerId, ValidPin(), ct);
        using var resolve = await OwnerLocationHttpTestHost.SendAsync(client, HttpMethod.Get,
            ResolveUrl + "?address=2%20Hai%20Trieu", role, OwnerId, null, ct);
        Assert.Equal(HttpStatusCode.Forbidden, confirm.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, resolve.StatusCode);
    }

    [Fact]
    public async Task Owner_confirms_manual_entrance_without_first_resolving_address()
    {
        await using var app = await CreateAppAsync();
        using var client = app.GetTestClient();
        using var response = await OwnerLocationHttpTestHost.SendAsync(client, HttpMethod.Post, ConfirmUrl,
            "ParkingLotOwner", OwnerId, ValidPin(), TestContext.Current.CancellationToken);

        await OwnerLocationHttpTestHost.AssertStatusAsync(HttpStatusCode.OK, response);
        var token = await response.Content.ReadFromJsonAsync<OwnerLotLocationConfirmation>(
            TestContext.Current.CancellationToken);
        Assert.NotNull(token);
        Assert.False(string.IsNullOrWhiteSpace(token.LocationConfirmationToken));
        Assert.InRange(token.ExpiresAtUtc, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddMinutes(16));
        Assert.True(app.Services.GetRequiredService<OwnerLotLocationConfirmationService>()
            .MatchesRegistration(OwnerId, token.LocationConfirmationToken,
                "2 Hai Trieu, Ho Chi Minh City", 10.771m, 106.704m, requireConfirmation: true));
    }

    [Theory]
    [InlineData(91, 106.704, true)]
    [InlineData(10.771, -181, true)]
    [InlineData(0, 0, true)]
    [InlineData(10.771, 106.704, false)]
    public async Task Invalid_or_unconfirmed_pin_is_rejected_over_http(double lat, double lon, bool confirmed)
    {
        await using var app = await CreateAppAsync();
        using var client = app.GetTestClient();
        using var response = await OwnerLocationHttpTestHost.SendAsync(client, HttpMethod.Post, ConfirmUrl,
            "ParkingLotOwner", OwnerId, ValidPin((decimal)lat, (decimal)lon, confirmed),
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Address_not_found_returns_empty_candidates_and_manual_pin_allowed()
    {
        await using var app = await CreateAppAsync(new StubGeocoder(_ => Task.FromResult<GeocodedDestination?>(null)));
        using var client = app.GetTestClient();
        using var response = await OwnerLocationHttpTestHost.SendAsync(client, HttpMethod.Get,
            ResolveUrl + "?address=Unknown%20road%20name", "ParkingLotOwner", OwnerId, null,
            TestContext.Current.CancellationToken);

        await OwnerLocationHttpTestHost.AssertStatusAsync(HttpStatusCode.OK, response);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal(0, json.RootElement.GetProperty("candidates").GetArrayLength());
        Assert.True(json.RootElement.GetProperty("manualPinAllowed").GetBoolean());
    }

    [Fact]
    public async Task Address_found_is_only_a_suggestion_and_manual_pin_remains_allowed()
    {
        await using var app = await CreateAppAsync(new StubGeocoder(_ =>
            Task.FromResult<GeocodedDestination?>(new("Approximate result", 10.77m, 106.7m))));
        using var client = app.GetTestClient();
        using var response = await OwnerLocationHttpTestHost.SendAsync(client, HttpMethod.Get,
            ResolveUrl + "?address=2%20Hai%20Trieu", "ParkingLotOwner", OwnerId, null,
            TestContext.Current.CancellationToken);

        await OwnerLocationHttpTestHost.AssertStatusAsync(HttpStatusCode.OK, response);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Single(json.RootElement.GetProperty("candidates").EnumerateArray());
        Assert.True(json.RootElement.GetProperty("manualPinAllowed").GetBoolean());
    }

    [Theory]
    [InlineData(true, HttpStatusCode.TooManyRequests)]
    [InlineData(false, HttpStatusCode.ServiceUnavailable)]
    public async Task Geocoding_errors_are_not_misreported_as_address_not_found(bool rateLimited, HttpStatusCode expected)
    {
        await using var app = await CreateAppAsync(new StubGeocoder(_ =>
            rateLimited ? Task.FromException<GeocodedDestination?>(new GeocodingRateLimitedException())
                        : Task.FromException<GeocodedDestination?>(new GeocodingProviderUnavailableException())));
        using var client = app.GetTestClient();
        using var response = await OwnerLocationHttpTestHost.SendAsync(client, HttpMethod.Get,
            ResolveUrl + "?address=2%20Hai%20Trieu", "ParkingLotOwner", OwnerId, null,
            TestContext.Current.CancellationToken);
        Assert.Equal(expected, response.StatusCode);
    }

    private static object ValidPin(decimal latitude = 10.771m, decimal longitude = 106.704m, bool confirmed = true) => new
    {
        address = "2 Hai Trieu, Ho Chi Minh City",
        latitude,
        longitude,
        confirmedOnMap = confirmed
    };

    private static async Task<WebApplication> CreateAppAsync(IAddressGeocoder? geocoder = null)
    {
        var builder = OwnerLocationHttpTestHost.CreateBuilder();
        builder.Services.AddApplicationServices();
        builder.Services.AddSingleton<IAddressGeocoder>(geocoder ??
            new StubGeocoder(_ => Task.FromResult<GeocodedDestination?>(null)));
        var app = builder.Build();
        OwnerLocationHttpTestHost.ConfigurePipeline(app);
        app.MapOwnerLotLocationEndpoints();
        await app.StartAsync(TestContext.Current.CancellationToken);
        return app;
    }

    private sealed class StubGeocoder(Func<string, Task<GeocodedDestination?>> resolve) : IAddressGeocoder
    {
        public Task<GeocodedDestination?> GeocodeAsync(string address, CancellationToken cancellationToken) => resolve(address);
    }
}

// Covers the actual POST /api/parking/lots boundary with PostgreSQL test fixture and strict flag enabled.
public sealed class OwnerLotLocationRegistrationHttpTests(PostgreSqlContainerFixture postgres)
    : IClassFixture<PostgreSqlContainerFixture>
{
    [Fact]
    public async Task Strict_registration_requires_a_matching_confirmation_token_and_preserves_confirmed_pin()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var app = await CreateAppAsync(ct);
        using var client = app.GetTestClient();
        var owner = Guid.NewGuid();
        var otherOwner = Guid.NewGuid();
        var name = "Owner entrance integration " + Guid.NewGuid().ToString("N");
        var payload = new
        {
            name,
            address = "2 Hai Trieu, Ho Chi Minh City",
            latitude = 10.771m,
            longitude = 106.704m,
            type = "Outdoor"
        };

        using var noToken = await OwnerLocationHttpTestHost.SendAsync(client, HttpMethod.Post,
            "/api/parking/lots", "ParkingLotOwner", owner, payload, ct);
        Assert.Equal(HttpStatusCode.BadRequest, noToken.StatusCode);

        using var confirmation = await OwnerLocationHttpTestHost.SendAsync(client, HttpMethod.Post,
            "/api/parking/owner/locations/confirm", "ParkingLotOwner", owner,
            new { payload.address, payload.latitude, payload.longitude, confirmedOnMap = true }, ct);
        await OwnerLocationHttpTestHost.AssertStatusAsync(HttpStatusCode.OK, confirmation);
        var tokenResult = await confirmation.Content.ReadFromJsonAsync<OwnerLotLocationConfirmation>(ct);
        Assert.NotNull(tokenResult);

        var withToken = new
        {
            payload.name, payload.address, payload.latitude, payload.longitude, payload.type,
            locationConfirmationToken = tokenResult.LocationConfirmationToken
        };
        using var anotherOwner = await OwnerLocationHttpTestHost.SendAsync(client, HttpMethod.Post,
            "/api/parking/lots", "ParkingLotOwner", otherOwner, withToken, ct);
        Assert.Equal(HttpStatusCode.BadRequest, anotherOwner.StatusCode);

        using var wrongPin = await OwnerLocationHttpTestHost.SendAsync(client, HttpMethod.Post,
            "/api/parking/lots", "ParkingLotOwner", owner, new
            {
                payload.name, payload.address, latitude = 10.772m, payload.longitude, payload.type,
                locationConfirmationToken = tokenResult.LocationConfirmationToken
            }, ct);
        Assert.Equal(HttpStatusCode.BadRequest, wrongPin.StatusCode);

        using var correct = await OwnerLocationHttpTestHost.SendAsync(client, HttpMethod.Post,
            "/api/parking/lots", "ParkingLotOwner", owner, withToken, ct);
        await OwnerLocationHttpTestHost.AssertStatusAsync(HttpStatusCode.Created, correct);

        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ParkingDbContext>();
        var persisted = await db.ParkingLots.SingleAsync(lot => lot.Name == name && lot.OwnerUserId == owner, ct);
        Assert.Equal(10.771m, persisted.Latitude);
        Assert.Equal(106.704m, persisted.Longitude);
    }

    private async Task<WebApplication> CreateAppAsync(CancellationToken ct)
    {
        var builder = OwnerLocationHttpTestHost.CreateBuilder();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Parking:RequireOwnerLocationConfirmation"] = "true"
        });
        builder.Services.AddApplicationServices();
        builder.Services.AddPersistence(PostgreSqlTestConfiguration.Create(postgres.ConnectionString));
        var app = builder.Build();
        OwnerLocationHttpTestHost.ConfigurePipeline(app);
        app.MapOwnerLotLocationEndpoints();
        app.MapParkingLotEndpoints();
        await app.StartAsync(ct);

        await using var scope = app.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<ParkingDbContext>().Database.MigrateAsync(ct);
        return app;
    }
}

internal static class OwnerLocationHttpTestHost
{
    private const string AccountHeader = "X-Test-Account";
    private const string RoleHeader = "X-Test-Role";

    public static WebApplicationBuilder CreateBuilder()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Production" });
        builder.Host.UseDefaultServiceProvider(options => options.ValidateOnBuild = false);
        builder.WebHost.UseTestServer();
        builder.AddParkingSystemWebFoundation();
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddParkingAuthorization();
        builder.Services.AddDataProtection();
        builder.Services.AddSingleton<OwnerLotLocationConfirmationService>();
        builder.Services.AddAuthentication(HeaderAuthenticationHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, HeaderAuthenticationHandler>(HeaderAuthenticationHandler.SchemeName, null);
        builder.Services.AddScoped<ICurrentUser, HeaderCurrentUser>();
        return builder;
    }

    public static void ConfigurePipeline(WebApplication app)
    {
        app.UseParkingSystemWebFoundation();
        app.UseAuthentication();
        app.UseAuthorization();
    }

    public static async Task<HttpResponseMessage> SendAsync(HttpClient client, HttpMethod method, string path,
        string? role, Guid? userId, object? body, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(method, path);
        if (body is not null)
            request.Content = JsonContent.Create(body);
        if (role is not null && userId is not null)
        {
            request.Headers.Add(RoleHeader, role);
            request.Headers.Add(AccountHeader, userId.Value.ToString());
        }
        return await client.SendAsync(request, ct);
    }

    public static async Task AssertStatusAsync(HttpStatusCode expected, HttpResponseMessage response)
    {
        if (response.StatusCode == expected)
            return;
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Fail($"{response.RequestMessage?.Method} {response.RequestMessage?.RequestUri?.PathAndQuery}: expected {(int)expected}, got {(int)response.StatusCode}: {body}");
    }

    private sealed class HeaderAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public const string SchemeName = "TestOwnerLocationHeader";

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.TryGetValue(AccountHeader, out var account)
                || !Request.Headers.TryGetValue(RoleHeader, out var role))
                return Task.FromResult(AuthenticateResult.NoResult());

            var identity = new ClaimsIdentity(
                [new Claim("sub", account.ToString()), new Claim(ParkingAuthorization.RoleClaimType, role.ToString())],
                SchemeName);
            return Task.FromResult(AuthenticateResult.Success(
                new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName)));
        }
    }

    private sealed class HeaderCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
    {
        public string? SubjectId => accessor.HttpContext?.User.FindFirst("sub")?.Value;
        public bool IsAuthenticated => accessor.HttpContext?.User.Identity?.IsAuthenticated ?? false;
    }
}
