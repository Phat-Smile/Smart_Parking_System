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
using ParkingSystem.AspNetCore.Security;
using ParkingSystem.BuildingBlocks.Identity;
using ParkingSystem.Parking.Application;
using ParkingSystem.Parking.Application.ParkingLots;
using ParkingSystem.Parking.Application.ParkingLots.ListStaffLots;
using ParkingSystem.Parking.Application.ParkingSessions;
using ParkingSystem.Parking.Domain.ParkingLots;
using ParkingSystem.Parking.Domain.ParkingSessions;
using ParkingSystem.Parking.WebAPI.Endpoints;
using ParkingSystem.Parking.WebAPI.Security;
using Xunit;

namespace ParkingSystem.Tests.Parking.IntegrationTests;

// Lead task S2 (2026-10-27) at the HTTP layer: 401 without a token, 403 for the wrong role or lot, 404, 400 and 200.
// The repositories are in memory; the PostgreSQL queries are covered in StaffSessionQueriesIntegrationTests.
public sealed class StaffSessionQueriesHttpTests
{
    private const string CallerId = "11111111-1111-1111-1111-111111111111";
    private const string RoleHeader = "X-Test-Role";
    private static readonly Guid AssignedLotId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly Guid OtherLotId = Guid.Parse("10000000-0000-0000-0000-000000000002");
    private static readonly DateTimeOffset EntryTime = new(2026, 10, 27, 8, 0, 0, TimeSpan.Zero);

    // A session of the caller's lot owned by another customer, one owned by the caller, and one of a lot the caller is not assigned to.
    private static readonly ParkingSession LotSession = NewSession(AssignedLotId, Guid.NewGuid(), "qr-lot");
    private static readonly ParkingSession CallerSession = NewSession(OtherLotId, Guid.Parse(CallerId), "qr-mine");
    private static readonly ParkingSession OtherLotSession = NewSession(OtherLotId, Guid.NewGuid(), "qr-other");

    private static readonly string StaffLotsRoute = "/api/parking/staff/lots";
    private static readonly string LotSessionsRoute = $"/api/parking/staff/lots/{AssignedLotId}/sessions";
    private static readonly string AssignmentsRoute = $"/api/parking/admin/lots/{AssignedLotId}/assignments";

    [Theory]
    [MemberData(nameof(AllRoutes))]
    public async Task Every_route_returns_401_without_a_token(string route)
    {
        await using var app = await CreateAppAsync();

        using var response = await SendAsync(app, route, role: null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("Customer")]
    [InlineData("SystemAdmin")]
    public async Task Staff_routes_return_403_for_other_roles(string role)
    {
        await using var app = await CreateAppAsync();

        using var lots = await SendAsync(app, StaffLotsRoute, role);
        using var sessions = await SendAsync(app, LotSessionsRoute, role);

        Assert.Equal(HttpStatusCode.Forbidden, lots.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, sessions.StatusCode);
    }

    [Theory]
    [InlineData("Customer")]
    [InlineData("Staff")]
    [InlineData("ParkingLotOwner")]
    public async Task Assignment_list_returns_403_for_anyone_but_a_system_admin(string role)
    {
        await using var app = await CreateAppAsync();

        using var response = await SendAsync(app, AssignmentsRoute, role);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData("Staff")]
    [InlineData("ParkingLotOwner")]
    public async Task Staff_lots_returns_the_callers_lots(string role)
    {
        await using var app = await CreateAppAsync();

        using var response = await SendAsync(app, StaffLotsRoute, role);
        var body = await ReadJsonAsync(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var lot = Assert.Single(body.EnumerateArray());
        Assert.Equal(AssignedLotId, lot.GetProperty("id").GetGuid());
        Assert.Equal("Central", lot.GetProperty("name").GetString());
        Assert.Equal("1 Demo Street", lot.GetProperty("address").GetString());
        Assert.Equal("Outdoor", lot.GetProperty("type").GetString());
    }

    [Fact]
    public async Task Assignment_list_returns_the_lot_members_to_a_system_admin()
    {
        await using var app = await CreateAppAsync();

        using var response = await SendAsync(app, AssignmentsRoute, "SystemAdmin");
        var body = await ReadJsonAsync(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var member = Assert.Single(body.EnumerateArray());
        Assert.Equal(Guid.Parse(CallerId), member.GetProperty("accountId").GetGuid());
        Assert.Equal("Staff", member.GetProperty("role").GetString());
        Assert.Equal(EntryTime, member.GetProperty("assignedAtUtc").GetDateTimeOffset());
    }

    [Fact]
    public async Task Assignment_list_of_an_unknown_lot_returns_404_lot_not_found()
    {
        await using var app = await CreateAppAsync();

        using var response = await SendAsync(app, $"/api/parking/admin/lots/{Guid.NewGuid()}/assignments", "SystemAdmin");

        await AssertProblemAsync(response, HttpStatusCode.NotFound, "parking.lot.not_found");
    }

    [Fact]
    public async Task Session_owner_gets_the_session_with_its_qr()
    {
        await using var app = await CreateAppAsync();

        using var response = await SendAsync(app, $"/api/parking/sessions/{CallerSession.Id}", "Customer");
        var body = await ReadJsonAsync(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(CallerSession.Id, body.GetProperty("sessionId").GetGuid());
        Assert.Equal("qr-mine", body.GetProperty("qrToken").GetString());
        Assert.Equal(JsonValueKind.Null, body.GetProperty("slotCode").ValueKind);
        Assert.Equal(JsonValueKind.Null, body.GetProperty("paidAtUtc").ValueKind);
        Assert.Equal(JsonValueKind.Null, body.GetProperty("graceUntilUtc").ValueKind);
    }

    [Fact]
    public async Task Assigned_staff_get_the_session_without_its_qr()
    {
        await using var app = await CreateAppAsync();

        using var response = await SendAsync(app, $"/api/parking/sessions/{LotSession.Id}", "Staff");
        var body = await ReadJsonAsync(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("51A-123.45", body.GetProperty("plateNumber").GetString());
        Assert.Equal(JsonValueKind.Null, body.GetProperty("qrToken").ValueKind);
    }

    [Theory]
    [InlineData("Customer")]
    [InlineData("Staff")]
    public async Task Session_of_another_customer_in_an_unassigned_lot_returns_403(string role)
    {
        await using var app = await CreateAppAsync();

        using var response = await SendAsync(app, $"/api/parking/sessions/{OtherLotSession.Id}", role);

        await AssertProblemAsync(response, HttpStatusCode.Forbidden, ParkingLotAccessErrors.AccessDenied.Code);
    }

    [Fact]
    public async Task Unknown_session_returns_404_session_not_found()
    {
        await using var app = await CreateAppAsync();

        using var response = await SendAsync(app, $"/api/parking/sessions/{Guid.NewGuid()}", "Staff");

        await AssertProblemAsync(response, HttpStatusCode.NotFound, "parking.session.not_found");
    }

    [Fact]
    public async Task Lot_sessions_use_the_default_page_when_none_is_given()
    {
        await using var app = await CreateAppAsync();

        using var response = await SendAsync(app, LotSessionsRoute + "?status=Active", "Staff");
        var body = await ReadJsonAsync(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1, body.GetProperty("page").GetInt32());
        Assert.Equal(20, body.GetProperty("pageSize").GetInt32());
        Assert.Equal(1, body.GetProperty("totalCount").GetInt32());
        var item = Assert.Single(body.GetProperty("items").EnumerateArray());
        Assert.Equal(LotSession.Id, item.GetProperty("sessionId").GetGuid());
        Assert.Equal("Active", item.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Lot_sessions_of_an_unassigned_lot_return_403()
    {
        await using var app = await CreateAppAsync();

        using var response = await SendAsync(app, $"/api/parking/staff/lots/{OtherLotId}/sessions", "Staff");

        await AssertProblemAsync(response, HttpStatusCode.Forbidden, ParkingLotAccessErrors.AccessDenied.Code);
    }

    [Theory]
    [InlineData("?pageSize=101")]
    [InlineData("?page=0")]
    [InlineData("?status=Parked")]
    public async Task Lot_sessions_with_bad_paging_or_status_return_400(string queryString)
    {
        await using var app = await CreateAppAsync();

        using var response = await SendAsync(app, LotSessionsRoute + queryString, "Staff");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    public static TheoryData<string> AllRoutes() =>
        [StaffLotsRoute, LotSessionsRoute, AssignmentsRoute, $"/api/parking/sessions/{LotSession.Id}"];

    private static ParkingSession NewSession(Guid lotId, Guid userId, string qrToken) =>
        ParkingSession.Open(Guid.NewGuid(), lotId, "51A-123.45", VehicleType.Car, EntryTime, qrToken, userId, Guid.NewGuid());

    private static async Task<HttpResponseMessage> SendAsync(WebApplication app, string route, string? role)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, route);
        if (role is not null)
            request.Headers.Add(RoleHeader, role);
        return await app.GetTestClient().SendAsync(request, TestContext.Current.CancellationToken);
    }

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);

    private static async Task AssertProblemAsync(HttpResponseMessage response, HttpStatusCode status, string errorCode)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Equal(errorCode, (await ReadJsonAsync(response)).GetProperty("errorCode").GetString());
    }

    private static async Task<WebApplication> CreateAppAsync()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Production" });
        builder.Host.UseDefaultServiceProvider(options => options.ValidateOnBuild = false);
        builder.WebHost.UseTestServer();
        builder.AddParkingSystemWebFoundation();
        builder.Services.AddApplicationServices();
        builder.Services.AddParkingAuthorization();
        // Program.cs gets this policy from AddParkingSystemSecurity, which also wires JWT; GET /sessions/{id} needs it.
        builder.Services.AddAuthorization(options => options.AddPolicy(
            ParkingSystemSecurityExtensions.AuthenticatedUserPolicy, policy => policy.RequireAuthenticatedUser()));
        builder.Services.AddAuthentication(HeaderRoleAuthenticationHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, HeaderRoleAuthenticationHandler>(HeaderRoleAuthenticationHandler.SchemeName, null);
        builder.Services.AddScoped<ICurrentUser, FixedCurrentUser>();
        builder.Services.AddSingleton<IParkingLotAccess, AssignedLotOnly>();
        builder.Services.AddSingleton<IParkingLotAssignmentRepository, InMemoryAssignments>();
        builder.Services.AddSingleton<IParkingSessionRepository, InMemorySessions>();

        var app = builder.Build();
        app.UseParkingSystemWebFoundation();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapParkingSessionEndpoints();
        app.MapParkingLotAssignmentEndpoints();
        app.MapStaffLotEndpoints();
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

    private sealed class AssignedLotOnly : IParkingLotAccess
    {
        public Task<bool> CanOperateAsync(Guid accountId, Guid lotId, CancellationToken cancellationToken) =>
            Task.FromResult(lotId == AssignedLotId);
    }

    private sealed class InMemoryAssignments : IParkingLotAssignmentRepository
    {
        public Task<bool> LotExistsAsync(Guid lotId, CancellationToken cancellationToken) =>
            Task.FromResult(lotId == AssignedLotId || lotId == OtherLotId);

        public Task<IReadOnlyList<StaffLot>> ListApprovedLotsOfMemberAsync(Guid accountId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<StaffLot>>([new StaffLot(AssignedLotId, "Central", "1 Demo Street", "Outdoor")]);

        public Task<IReadOnlyList<ParkingLotAssignment>> ListByLotAsync(Guid lotId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ParkingLotAssignment>>(
                [ParkingLotAssignment.Create(lotId, Guid.Parse(CallerId), ParkingLotMemberRole.Staff, Guid.NewGuid(), EntryTime)]);

        public Task<ParkingLotAssignment?> FindAsync(Guid lotId, Guid accountId, CancellationToken cancellationToken) => throw new InvalidOperationException();
        public void Add(ParkingLotAssignment assignment) => throw new InvalidOperationException();
        public void Remove(ParkingLotAssignment assignment) => throw new InvalidOperationException();
        public Task SaveChangesAsync(CancellationToken cancellationToken) => throw new InvalidOperationException();
    }

    private sealed class InMemorySessions : IParkingSessionRepository
    {
        private static readonly ParkingSession[] Sessions = [LotSession, CallerSession, OtherLotSession];

        public Task<ParkingSession?> FindByIdAsync(Guid sessionId, CancellationToken cancellationToken) =>
            Task.FromResult(Sessions.FirstOrDefault(session => session.Id == sessionId));

        public Task<(IReadOnlyList<ParkingSession> Items, int TotalCount)> ListByLotAsync(
            Guid lotId, ParkingSessionStatus? status, int skip, int take, CancellationToken cancellationToken)
        {
            var matching = Sessions.Where(session => session.LotId == lotId && (status is null || session.Status == status)).ToList();
            return Task.FromResult<(IReadOnlyList<ParkingSession>, int)>((matching.Skip(skip).Take(take).ToList(), matching.Count));
        }

        public Task<bool> HasOpenSessionAsync(Guid lotId, string normalizedPlate, CancellationToken cancellationToken) => throw new InvalidOperationException();
        public Task<IReadOnlyList<ParkingSession>> ListOpenByUserAsync(Guid userId, CancellationToken cancellationToken) => throw new InvalidOperationException();
        public Task<ParkingSession?> FindOpenByQrTokenAsync(string qrToken, CancellationToken cancellationToken) => throw new InvalidOperationException();
        public Task<ParkingSession?> FindOpenByPlateAsync(Guid lotId, string normalizedPlate, CancellationToken cancellationToken) => throw new InvalidOperationException();
        public void Add(ParkingSession session) => throw new InvalidOperationException();
        public Task<bool> TrySaveChangesAsync(CancellationToken cancellationToken) => throw new InvalidOperationException();
    }
}
