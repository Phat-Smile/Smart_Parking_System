using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ParkingSystem.AspNetCore;
using ParkingSystem.BuildingBlocks.Identity;
using ParkingSystem.Parking.Application;
using ParkingSystem.Parking.Application.ParkingLayout.ManageLayout;
using ParkingSystem.Parking.Application.ParkingLots.OpeningHours;
using ParkingSystem.Parking.Application.ParkingLots.RegisterParkingLot;
using ParkingSystem.Parking.Persistence;
using ParkingSystem.Parking.WebAPI.Endpoints;
using ParkingSystem.Parking.WebAPI.OwnerLocation;
using ParkingSystem.Parking.WebAPI.Security;
using ParkingSystem.Testing;
using Xunit;

namespace ParkingSystem.Tests.Parking.IntegrationTests;

// MOD-PK-04 and MOD-PK-05 over HTTP: the real routes, policies, handlers and PostgreSQL.
// Only the sign-in is replaced: two headers stand in for the "sub" and "role" claims of the Identity token.
public sealed class LotOwnerConventionHttpTests(PostgreSqlContainerFixture postgres)
    : IClassFixture<PostgreSqlContainerFixture>
{
    private const string AccountHeader = "X-Test-Account";
    private const string RoleHeader = "X-Test-Role";
    private const string OwnerRole = "ParkingLotOwner";
    private const string AdminRole = "SystemAdmin";

    private static readonly WeeklyOpeningHours EveryDayEightToTwenty = new(
        Enumerable.Range(0, 7).Select(day => new DailyOpeningHours(day, false, false, "08:00", "20:00")).ToArray());

    [Fact]
    public async Task A_freshly_registered_owner_can_set_opening_hours_and_the_layout_without_any_admin_step()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var app = await CreateAppAsync(ct);
        var client = app.GetTestClient();
        var owner = Guid.NewGuid();

        var lotId = await RegisterLotAsync(client, owner, "Http Fresh Owner Lot", ct);
        var hoursRoute = $"/api/parking/owner/lots/{lotId}/opening-hours";
        var layoutRoute = $"/api/parking/owner/lots/{lotId}/layout";

        using var hoursBefore = await SendAsync(client, HttpMethod.Get, hoursRoute, OwnerRole, owner, null, ct);
        await AssertStatusAsync(HttpStatusCode.OK, hoursBefore, ct);
        Assert.False((await hoursBefore.Content.ReadFromJsonAsync<OpeningHoursView>(ct))!.IsConfigured);

        using var hoursSaved = await SendAsync(client, HttpMethod.Put, hoursRoute, OwnerRole, owner, EveryDayEightToTwenty, ct);
        await AssertStatusAsync(HttpStatusCode.OK, hoursSaved, ct);
        using var hoursAfter = await SendAsync(client, HttpMethod.Get, hoursRoute, OwnerRole, owner, null, ct);
        await AssertStatusAsync(HttpStatusCode.OK, hoursAfter, ct);
        var view = (await hoursAfter.Content.ReadFromJsonAsync<OpeningHoursView>(ct))!;
        Assert.True(view.IsConfigured);
        Assert.Equal(7, view.Schedule!.Days.Count);

        using var layoutBefore = await SendAsync(client, HttpMethod.Get, layoutRoute, OwnerRole, owner, null, ct);
        await AssertStatusAsync(HttpStatusCode.OK, layoutBefore, ct);
        var version = (await layoutBefore.Content.ReadFromJsonAsync<ParkingLotLayout>(ct))!.Version;

        using var layoutSaved = await SendAsync(client, HttpMethod.Put, layoutRoute, OwnerRole, owner, new { expectedVersion = version, floors = OneFloorWithTwoSlots() }, ct);
        await AssertStatusAsync(HttpStatusCode.OK, layoutSaved, ct);
        using var layoutAfter = await SendAsync(client, HttpMethod.Get, layoutRoute, OwnerRole, owner, null, ct);
        await AssertStatusAsync(HttpStatusCode.OK, layoutAfter, ct);
        var layout = (await layoutAfter.Content.ReadFromJsonAsync<ParkingLotLayout>(ct))!;
        Assert.Equal(2, layout.Floors.Single().Zones.Single().Slots.Count);
    }

    [Fact]
    public async Task Only_owner_assignments_open_the_owner_endpoints_of_a_lot()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var app = await CreateAppAsync(ct);
        var client = app.GetTestClient();
        var registrant = Guid.NewGuid();
        var stranger = Guid.NewGuid();
        var staffOnTheLot = Guid.NewGuid();
        var admin = Guid.NewGuid();

        var lotId = await RegisterLotAsync(client, registrant, "Http Owner Rows Lot", ct);
        await AssignAsync(client, admin, lotId, staffOnTheLot, "Staff", ct);

        // Both callers carry the ParkingLotOwner role in their token, so the policy lets them in; the lot decides.
        foreach (var caller in new[] { stranger, staffOnTheLot })
            await AssertOwnerEndpointsDeniedAsync(client, caller, lotId, ct);

        // The registrant and an owner assigned by an admin are owners through the very same kind of row.
        await AssignAsync(client, admin, lotId, stranger, "Owner", ct);
        foreach (var caller in new[] { registrant, stranger })
        {
            using var hours = await SendAsync(client, HttpMethod.Put, $"/api/parking/owner/lots/{lotId}/opening-hours", OwnerRole, caller, EveryDayEightToTwenty, ct);
            await AssertStatusAsync(HttpStatusCode.OK, hours, ct);
            using var layout = await SendAsync(client, HttpMethod.Get, $"/api/parking/owner/lots/{lotId}/layout", OwnerRole, caller, null, ct);
            await AssertStatusAsync(HttpStatusCode.OK, layout, ct);
        }

        await AssertOwnerEndpointsDeniedAsync(client, staffOnTheLot, lotId, ct);
    }

    private static async Task AssertOwnerEndpointsDeniedAsync(HttpClient client, Guid caller, Guid lotId, CancellationToken ct)
    {
        var hoursRoute = $"/api/parking/owner/lots/{lotId}/opening-hours";
        var layoutRoute = $"/api/parking/owner/lots/{lotId}/layout";

        using var readHours = await SendAsync(client, HttpMethod.Get, hoursRoute, OwnerRole, caller, null, ct);
        using var saveHours = await SendAsync(client, HttpMethod.Put, hoursRoute, OwnerRole, caller, EveryDayEightToTwenty, ct);
        await AssertStatusAsync(HttpStatusCode.NotFound, readHours, ct);
        await AssertStatusAsync(HttpStatusCode.NotFound, saveHours, ct);

        using var readLayout = await SendAsync(client, HttpMethod.Get, layoutRoute, OwnerRole, caller, null, ct);
        using var saveLayout = await SendAsync(client, HttpMethod.Put, layoutRoute, OwnerRole, caller, new { expectedVersion = 0, floors = OneFloorWithTwoSlots() }, ct);
        foreach (var response in new[] { readLayout, saveLayout })
        {
            await AssertStatusAsync(HttpStatusCode.Forbidden, response, ct);
            var problem = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
            Assert.Equal(ParkingLayoutErrors.OwnerRequired.Code, problem.GetProperty("errorCode").GetString());
        }
    }

    private static async Task<Guid> RegisterLotAsync(HttpClient client, Guid owner, string name, CancellationToken ct)
    {
        using var response = await SendAsync(client, HttpMethod.Post, "/api/parking/lots", OwnerRole, owner, new
        {
            name,
            address = "1 Demo Street",
            latitude = 10.77m,
            longitude = 106.70m,
            type = "Outdoor"
        }, ct);
        await AssertStatusAsync(HttpStatusCode.Created, response, ct);
        return (await response.Content.ReadFromJsonAsync<RegisteredParkingLot>(ct))!.Id;
    }

    private static async Task AssignAsync(HttpClient client, Guid admin, Guid lotId, Guid account, string role, CancellationToken ct)
    {
        using var response = await SendAsync(client, HttpMethod.Put, $"/api/parking/admin/lots/{lotId}/assignments/{account}", AdminRole, admin, new { role }, ct);
        await AssertStatusAsync(HttpStatusCode.OK, response, ct);
    }

    private static LayoutFloorInput[] OneFloorWithTwoSlots() =>
    [
        new LayoutFloorInput(Guid.NewGuid(), "Ground", [
            new LayoutZoneInput(Guid.NewGuid(), "A", "Car", 2, false, [
                new LayoutSlotInput(Guid.NewGuid(), "A-1", null, new GridPoint(0, 0)),
                new LayoutSlotInput(Guid.NewGuid(), "A-2", null, new GridPoint(1, 0))
            ])
        ])
    ];

    private static async Task<HttpResponseMessage> SendAsync(
        HttpClient client, HttpMethod method, string route, string role, Guid account, object? body, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(method, route);
        if (body is not null)
            request.Content = JsonContent.Create(body);
        request.Headers.Add(RoleHeader, role);
        request.Headers.Add(AccountHeader, account.ToString());
        return await client.SendAsync(request, ct);
    }

    // Shows the response body when the status is not the expected one, so a failure explains itself.
    private static async Task AssertStatusAsync(HttpStatusCode expected, HttpResponseMessage response, CancellationToken ct)
    {
        if (response.StatusCode == expected)
            return;
        var body = await response.Content.ReadAsStringAsync(ct);
        Assert.Fail($"{response.RequestMessage?.Method} {response.RequestMessage?.RequestUri?.AbsolutePath}: expected {(int)expected} but got {(int)response.StatusCode}. {body}");
    }

    private async Task<WebApplication> CreateAppAsync(CancellationToken ct)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Production" });
        builder.Host.UseDefaultServiceProvider(options => options.ValidateOnBuild = false);
        builder.WebHost.UseTestServer();
        builder.AddParkingSystemWebFoundation();
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddApplicationServices();
        builder.Services.AddPersistence(PostgreSqlTestConfiguration.Create(postgres.ConnectionString));
        builder.Services.AddParkingAuthorization();
        builder.Services.AddAuthentication(HeaderAuthenticationHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, HeaderAuthenticationHandler>(HeaderAuthenticationHandler.SchemeName, null);
        builder.Services.AddScoped<ICurrentUser, HeaderCurrentUser>();

        // Registration endpoint's handler needs this service even when a test only exercises GET routes.
        // Isolate test tokens from real application Data Protection keys.
        builder.Services.AddSingleton(new OwnerLotLocationConfirmationService(
            new EphemeralDataProtectionProvider()));

        var app = builder.Build();
        app.UseParkingSystemWebFoundation();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapParkingLotEndpoints();
        app.MapParkingLotOpeningHoursEndpoints();
        app.MapParkingLotAssignmentEndpoints();
        app.MapParkingLayoutOwnerEndpoints();
        await app.StartAsync(ct);

        await using var scope = app.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<ParkingDbContext>().Database.MigrateAsync(ct);
        return app;
    }

    private sealed class HeaderAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public const string SchemeName = "TestHeaderAccount";

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.TryGetValue(AccountHeader, out var account) || !Request.Headers.TryGetValue(RoleHeader, out var role))
                return Task.FromResult(AuthenticateResult.NoResult());

            var claims = new List<Claim>
            {
                new("sub", account.ToString()),
                new(ParkingAuthorization.RoleClaimType, role.ToString())
            };

            if (role.ToString() == ParkingAuthorization.SystemAdminRole)
            {
                claims.Add(new Claim(ParkingAuthorization.AuthenticationMethodsClaimType, ParkingAuthorization.MfaMethod));
            }

            var identity = new ClaimsIdentity(claims, SchemeName);
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName)));
        }
    }

    private sealed class HeaderCurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
    {
        public string? SubjectId => httpContextAccessor.HttpContext?.User.FindFirst("sub")?.Value;

        public bool IsAuthenticated => httpContextAccessor.HttpContext?.User.Identity?.IsAuthenticated ?? false;
    }
}
