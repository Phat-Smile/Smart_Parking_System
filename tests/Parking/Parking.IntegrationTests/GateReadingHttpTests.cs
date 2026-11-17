using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ParkingSystem.AspNetCore;
using ParkingSystem.BuildingBlocks.Identity;
using ParkingSystem.BuildingBlocks.Time;
using ParkingSystem.Parking.Application;
using ParkingSystem.Parking.Application.Common.Auditing;
using ParkingSystem.Parking.Application.Incidents;
using ParkingSystem.Parking.Application.ParkingLots;
using ParkingSystem.Parking.Domain.GateReadings;
using ParkingSystem.Parking.Domain.Incidents;
using ParkingSystem.Parking.WebAPI.Endpoints;
using ParkingSystem.Parking.WebAPI.Security;
using Xunit;

namespace ParkingSystem.Tests.Parking.IntegrationTests;

// US-PARK-001 at the HTTP layer: 401 without a token, 403 for the wrong role or lot, 404, 400 and 201.
// The repositories are in memory; GateReadingIntegrationTests covers PostgreSQL.
public sealed class GateReadingHttpTests
{
    private const string Route = "/api/parking/gate-readings";
    private const string CallerId = "11111111-1111-1111-1111-111111111111";
    private const string RoleHeader = "X-Test-Role";
    private static readonly Guid AssignedLotId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly Guid OtherLotId = Guid.Parse("10000000-0000-0000-0000-000000000002");

    [Fact]
    public async Task Returns_401_without_a_token()
    {
        await using var app = await CreateAppAsync();

        using var response = await SendAsync(app, role: null, Reading(AssignedLotId));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("Customer")]
    [InlineData("SystemAdmin")]
    public async Task Returns_403_for_roles_other_than_staff_or_owner(string role)
    {
        await using var app = await CreateAppAsync();

        using var response = await SendAsync(app, role, Reading(AssignedLotId));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData("Staff")]
    [InlineData("ParkingLotOwner")]
    public async Task Returns_201_with_pending_staff_for_a_confident_valid_web_reading(string role)
    {
        await using var app = await CreateAppAsync();

        using var response = await SendAsync(app, role, Reading(AssignedLotId, confidence: 0.9m));
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        // Q-A4: all web readings require staff confirmation, even with a valid high-confidence plate.
        Assert.Equal(nameof(GateReadingDecision.PendingStaff), body.GetProperty("decision").GetString());
        Assert.Equal("51A-123.45", body.GetProperty("plateNumber").GetString());
        var incidentId = body.GetProperty("incidentId").GetGuid();
        Assert.NotEqual(Guid.Empty, incidentId);

        // Verify that the incident is actually waiting in this lot's staff queue.
        var queue = await GetJsonAsync(app, $"{Route}/pending?lotId={AssignedLotId}", role);
        var item = Assert.Single(queue.EnumerateArray());
        Assert.Equal(incidentId, item.GetProperty("incidentId").GetGuid());
        Assert.Equal("51A-123.45", item.GetProperty("observedPlate").GetString());
        Assert.Equal("Car", item.GetProperty("vehicleType").GetString());
        Assert.Equal(0.9m, item.GetProperty("confidence").GetDecimal());
        Assert.True(item.GetProperty("plateFormatValid").GetBoolean());
    }

    [Fact]
    public async Task Returns_201_with_pending_staff_for_a_low_confidence_reading()
    {
        await using var app = await CreateAppAsync();

        using var response = await SendAsync(app, "Staff", Reading(AssignedLotId, confidence: 0.6m));
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(nameof(GateReadingDecision.PendingStaff), body.GetProperty("decision").GetString());
        Assert.NotEqual(Guid.Empty, body.GetProperty("incidentId").GetGuid());
    }

    [Fact]
    public async Task Returns_403_lot_access_denied_for_a_lot_the_caller_is_not_assigned_to()
    {
        await using var app = await CreateAppAsync();

        using var response = await SendAsync(app, "Staff", Reading(OtherLotId));
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(ParkingLotAccessErrors.AccessDenied.Code, problem.GetProperty("errorCode").GetString());
    }

    [Fact]
    public async Task Returns_404_lot_not_found_for_an_unknown_lot()
    {
        await using var app = await CreateAppAsync();

        using var response = await SendAsync(app, "Staff", Reading(Guid.NewGuid()));
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("parking.lot.not_found", problem.GetProperty("errorCode").GetString());
    }

    [Theory]
    [InlineData("Car", 1.5)]
    [InlineData("Truck", 0.9)]
    public async Task Returns_400_for_an_invalid_reading(string vehicleType, double confidence)
    {
        await using var app = await CreateAppAsync();

        using var response = await SendAsync(app, "Staff", new { lotId = AssignedLotId, plateNumber = "51A-123.45", vehicleType, confidence });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // US-PARK-002.

    [Fact]
    public async Task Staff_see_a_low_confidence_reading_in_the_queue_and_confirm_it()
    {
        await using var app = await CreateAppAsync();
        using var recorded = await SendAsync(app, "Staff", Reading(AssignedLotId, confidence: 0.5m));
        var incidentId = (await recorded.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken)).GetProperty("incidentId").GetGuid();

        var queue = await GetJsonAsync(app, $"{Route}/pending?lotId={AssignedLotId}", "Staff");
        var item = Assert.Single(queue.EnumerateArray());
        Assert.Equal(incidentId, item.GetProperty("incidentId").GetGuid());
        Assert.Equal("51A-123.45", item.GetProperty("observedPlate").GetString());

        using var confirmed = await PostAsync(app, $"{Route}/{incidentId}/confirm", "Staff", new { plateNumber = "51A-123.45", vehicleType = "Car" });
        var body = await confirmed.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, confirmed.StatusCode);
        Assert.Equal("StaffConfirmed", body.GetProperty("decision").GetString());

        Assert.Empty((await GetJsonAsync(app, $"{Route}/pending?lotId={AssignedLotId}", "Staff")).EnumerateArray());
    }

    [Fact]
    public async Task Confirming_an_already_confirmed_reading_returns_409_not_pending()
    {
        await using var app = await CreateAppAsync();
        using var recorded = await SendAsync(app, "Staff", Reading(AssignedLotId, confidence: 0.5m));
        var incidentId = (await recorded.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken)).GetProperty("incidentId").GetGuid();
        using var first = await PostAsync(app, $"{Route}/{incidentId}/confirm", "Staff", new { plateNumber = "51A-123.45", vehicleType = "Car" });
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        using var response = await PostAsync(app, $"{Route}/{incidentId}/confirm", "Staff", new { plateNumber = "51A-123.45", vehicleType = "Car" });
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("parking.gate_reading.not_pending", problem.GetProperty("errorCode").GetString());
    }

    [Fact]
    public async Task Confirming_an_unknown_reading_returns_404()
    {
        await using var app = await CreateAppAsync();

        using var response = await PostAsync(app, $"{Route}/{Guid.NewGuid()}/confirm", "Staff", new { plateNumber = "51A-123.45", vehicleType = "Car" });
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("parking.gate_reading.not_found", problem.GetProperty("errorCode").GetString());
    }

    [Fact]
    public async Task Confirming_without_a_plate_returns_400()
    {
        await using var app = await CreateAppAsync();

        using var response = await PostAsync(app, $"{Route}/{Guid.NewGuid()}/confirm", "Staff", new { plateNumber = "", vehicleType = "Car" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Queue_of_an_unassigned_lot_returns_403_and_customers_get_403()
    {
        await using var app = await CreateAppAsync();

        using var otherLot = await GetAsync(app, $"{Route}/pending?lotId={OtherLotId}", "Staff");
        using var customer = await GetAsync(app, $"{Route}/pending?lotId={AssignedLotId}", "Customer");
        using var anonymous = await GetAsync(app, $"{Route}/pending?lotId={AssignedLotId}", role: null);

        Assert.Equal(HttpStatusCode.Forbidden, otherLot.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, customer.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
    }

    private static async Task<HttpResponseMessage> GetAsync(WebApplication app, string route, string? role)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, route);
        if (role is not null)
            request.Headers.Add(RoleHeader, role);
        return await app.GetTestClient().SendAsync(request, TestContext.Current.CancellationToken);
    }

    private static async Task<JsonElement> GetJsonAsync(WebApplication app, string route, string role)
    {
        using var response = await GetAsync(app, route, role);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
    }

    private static async Task<HttpResponseMessage> PostAsync(WebApplication app, string route, string role, object body)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, route) { Content = JsonContent.Create(body) };
        request.Headers.Add(RoleHeader, role);
        return await app.GetTestClient().SendAsync(request, TestContext.Current.CancellationToken);
    }

    private static object Reading(Guid lotId, decimal confidence = 0.95m) =>
        new { lotId, plateNumber = "51A-123.45", vehicleType = "Car", confidence };

    private static async Task<HttpResponseMessage> SendAsync(WebApplication app, string? role, object body)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, Route) { Content = JsonContent.Create(body) };
        if (role is not null)
            request.Headers.Add(RoleHeader, role);
        return await app.GetTestClient().SendAsync(request, TestContext.Current.CancellationToken);
    }

    private static async Task<WebApplication> CreateAppAsync()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Production" });
        builder.Host.UseDefaultServiceProvider(options => options.ValidateOnBuild = false);
        builder.WebHost.UseTestServer();
        builder.AddParkingSystemWebFoundation();
        builder.Services.AddApplicationServices();
        builder.Services.AddParkingAuthorization();
        builder.Services.AddAuthentication(HeaderRoleAuthenticationHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, HeaderRoleAuthenticationHandler>(HeaderRoleAuthenticationHandler.SchemeName, null);
        builder.Services.AddScoped<ICurrentUser, FixedCurrentUser>();
        builder.Services.AddSingleton<IClock, ParkingSystem.BuildingBlocks.Time.SystemClock>();
        builder.Services.AddSingleton<IParkingLotDetailReader, KnownLots>();
        builder.Services.AddSingleton<IParkingLotAccess, AssignedLotOnly>();
        builder.Services.AddSingleton<IIncidentRepository, InMemoryIncidents>();
        builder.Services.AddSingleton<IAuditLog, NoAudit>();

        var app = builder.Build();
        app.UseParkingSystemWebFoundation();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapGateReadingEndpoints();
        await app.StartAsync(TestContext.Current.CancellationToken);
        return app;
    }

    // No header means not signed in; otherwise the role comes from the header and the subject is always CallerId.
    private sealed class HeaderRoleAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public const string SchemeName = "TestHeaderRole";

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.TryGetValue(RoleHeader, out var role))
                return Task.FromResult(AuthenticateResult.NoResult());

            var identity = new ClaimsIdentity(
                [new Claim("sub", CallerId), new Claim(ParkingAuthorization.RoleClaimType, role.ToString())],
                SchemeName);
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName)));
        }
    }

    private sealed class FixedCurrentUser : ICurrentUser
    {
        public string? SubjectId => CallerId;

        public bool IsAuthenticated => true;
    }

    private sealed class KnownLots : IParkingLotDetailReader
    {
        public Task<ParkingLotDetail?> GetApprovedByIdAsync(Guid parkingLotId, CancellationToken cancellationToken) =>
            Task.FromResult(parkingLotId == AssignedLotId || parkingLotId == OtherLotId
                ? new ParkingLotDetail(parkingLotId, "Lot", "1 Test Street", 10.77m, 106.70m, "Outdoor")
                : null);
    }

    private sealed class AssignedLotOnly : IParkingLotAccess
    {
        public Task<bool> CanOperateAsync(Guid accountId, Guid lotId, CancellationToken cancellationToken) =>
            Task.FromResult(lotId == AssignedLotId);
    }

    // Incidents live for one app instance, so a test can record a reading and then confirm it.
    private sealed class InMemoryIncidents : IIncidentRepository
    {
        private readonly List<Incident> stored = [];

        public void Add(Incident incident) => stored.Add(incident);

        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<Incident?> FindByIdAsync(Guid incidentId, CancellationToken cancellationToken) =>
            Task.FromResult(stored.FirstOrDefault(incident => incident.Id == incidentId));

        public Task<IReadOnlyList<Incident>> ListOpenByLotAsync(Guid lotId, IncidentType incidentType, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<Incident>>(stored
                .Where(incident => incident.LotId == lotId && incident.Status == IncidentStatus.Open && incident.IncidentType == incidentType)
                .OrderBy(incident => incident.OccurredAt)
                .ToList());

        public Task<bool> TrySaveChangesAsync(CancellationToken cancellationToken) => Task.FromResult(true);
    }

    private sealed class NoAudit : IAuditLog
    {
        public void Record(AuditEntry entry)
        {
        }
    }
}
