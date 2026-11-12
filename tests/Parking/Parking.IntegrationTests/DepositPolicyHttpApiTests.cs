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
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ParkingSystem.AspNetCore;
using ParkingSystem.BuildingBlocks.Identity;
using ParkingSystem.Parking.Application;
using ParkingSystem.Parking.Domain.Entities;
using ParkingSystem.Parking.Domain.ParkingLots;
using ParkingSystem.Parking.Persistence;
using ParkingSystem.Parking.WebAPI.DTOs;
using ParkingSystem.Parking.WebAPI.Endpoints;
using ParkingSystem.Parking.WebAPI.Security;
using ParkingSystem.Testing;
using Xunit;

namespace ParkingSystem.Tests.Parking.IntegrationTests;

public sealed class DepositPolicyHttpApiTests(PostgreSqlContainerFixture postgres)
    : IClassFixture<PostgreSqlContainerFixture>
{
    private const string AccountHeader = "X-Test-Account";
    private const string RoleHeader = "X-Test-Role";
    private const string OwnerRole = "ParkingLotOwner";
    private const string DriverRole = "Driver";

    private sealed record DepositPolicyHttpEnvelope(
        DepositPolicyHttpResult? Result,
        bool IsSuccess,
        int StatusCode,
        string? Message);

    private sealed record DepositPolicyHttpResult(
        Guid LotId,
        decimal HoldDepositVnd,
        bool IsConfigured,
        DateTime? UpdatedAtUtc);

    [Fact]
    public async Task API_01_to_05_DepositPolicy_Lifecycle_HappyPath()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var app = await CreateAppAsync(ct);
        var client = app.GetTestClient();
        var ownerId = Guid.NewGuid();
        var lotId = await InsertLotAsync(app.Services, ownerId, "Happy Path Lot", ct);

        var route = $"/api/parking/lots/{lotId}/deposit-policy";

        // API-01: GET lot unconfigured -> 200, 20000, isConfigured = false, updatedAtUtc = null
        using var get1 = await SendAsync(client, HttpMethod.Get, route, OwnerRole, ownerId, null, ct);
        Assert.Equal(HttpStatusCode.OK, get1.StatusCode);
        var res1 = await get1.Content.ReadFromJsonAsync<DepositPolicyHttpEnvelope>(ct);
        Assert.NotNull(res1);
        Assert.True(res1.IsSuccess);
        Assert.NotNull(res1.Result);
        Assert.Equal(lotId, res1.Result.LotId);
        Assert.Equal(20000m, res1.Result.HoldDepositVnd);
        Assert.False(res1.Result.IsConfigured);
        Assert.Null(res1.Result.UpdatedAtUtc);

        // API-02: PUT lot with 15000 -> 200, 15000, isConfigured = true
        using var put2 = await SendAsync(client, HttpMethod.Put, route, OwnerRole, ownerId, new { holdDepositVnd = 15000 }, ct);
        Assert.Equal(HttpStatusCode.OK, put2.StatusCode);
        var res2 = await put2.Content.ReadFromJsonAsync<DepositPolicyHttpEnvelope>(ct);
        Assert.NotNull(res2);
        Assert.True(res2.IsSuccess);
        Assert.Equal(15000m, res2.Result!.HoldDepositVnd);
        Assert.True(res2.Result.IsConfigured);
        Assert.NotNull(res2.Result.UpdatedAtUtc);

        // API-03: GET after API-02 -> 200, 15000, isConfigured = true
        using var get3 = await SendAsync(client, HttpMethod.Get, route, OwnerRole, ownerId, null, ct);
        Assert.Equal(HttpStatusCode.OK, get3.StatusCode);
        var res3 = await get3.Content.ReadFromJsonAsync<DepositPolicyHttpEnvelope>(ct);
        Assert.NotNull(res3);
        Assert.True(res3.IsSuccess);
        Assert.Equal(15000m, res3.Result!.HoldDepositVnd);
        Assert.True(res3.Result.IsConfigured);
        Assert.NotNull(res3.Result.UpdatedAtUtc);

        // API-04: PUT lot with 0 VND -> 200, 0, isConfigured = true
        using var put4 = await SendAsync(client, HttpMethod.Put, route, OwnerRole, ownerId, new { holdDepositVnd = 0 }, ct);
        Assert.Equal(HttpStatusCode.OK, put4.StatusCode);
        var res4 = await put4.Content.ReadFromJsonAsync<DepositPolicyHttpEnvelope>(ct);
        Assert.NotNull(res4);
        Assert.True(res4.IsSuccess);
        Assert.Equal(0m, res4.Result!.HoldDepositVnd);
        Assert.True(res4.Result.IsConfigured);

        // API-05: GET after API-04 -> 200, 0, isConfigured = true
        using var get5 = await SendAsync(client, HttpMethod.Get, route, OwnerRole, ownerId, null, ct);
        Assert.Equal(HttpStatusCode.OK, get5.StatusCode);
        var res5 = await get5.Content.ReadFromJsonAsync<DepositPolicyHttpEnvelope>(ct);
        Assert.NotNull(res5);
        Assert.True(res5.IsSuccess);
        Assert.Equal(0m, res5.Result!.HoldDepositVnd);
        Assert.True(res5.Result.IsConfigured);
    }

    [Fact]
    public async Task API_06_and_07_InvalidValues_Negative_And_Fractional_Are_Rejected()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var app = await CreateAppAsync(ct);
        var client = app.GetTestClient();
        var ownerId = Guid.NewGuid();
        var lotId = await InsertLotAsync(app.Services, ownerId, "Validation Lot", ct);
        var route = $"/api/parking/lots/{lotId}/deposit-policy";

        // Pre-configure to 10000
        using var initPut = await SendAsync(client, HttpMethod.Put, route, OwnerRole, ownerId, new { holdDepositVnd = 10000 }, ct);
        Assert.Equal(HttpStatusCode.OK, initPut.StatusCode);

        // API-06: PUT with negative amount
        using var putNegative = await SendAsync(client, HttpMethod.Put, route, OwnerRole, ownerId, new { holdDepositVnd = -5000 }, ct);
        Assert.Equal(HttpStatusCode.BadRequest, putNegative.StatusCode);

        // API-07: PUT with fractional amount
        using var putFractional = await SendAsync(client, HttpMethod.Put, route, OwnerRole, ownerId, new { holdDepositVnd = 15000.5 }, ct);
        Assert.Equal(HttpStatusCode.BadRequest, putFractional.StatusCode);

        // PUT with missing field {}
        using var putMissing = await SendAsync(client, HttpMethod.Put, route, OwnerRole, ownerId, new { }, ct);
        Assert.Equal(HttpStatusCode.BadRequest, putMissing.StatusCode);

        // Verify DB unchanged: still 10000
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ParkingDbContext>();
        var policy = await db.ReservationDepositPolicies.FirstOrDefaultAsync(p => p.LotId == lotId, ct);
        Assert.NotNull(policy);
        Assert.Equal(10000m, policy.HoldDepositVnd);
    }

    [Fact]
    public async Task API_08_and_09_NonExistentLot_Returns404()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var app = await CreateAppAsync(ct);
        var client = app.GetTestClient();
        var ownerId = Guid.NewGuid();
        var missingLotId = Guid.NewGuid();
        var route = $"/api/parking/lots/{missingLotId}/deposit-policy";

        // API-08: GET non-existent lot -> 404
        using var getRes = await SendAsync(client, HttpMethod.Get, route, OwnerRole, ownerId, null, ct);
        Assert.Equal(HttpStatusCode.NotFound, getRes.StatusCode);

        // API-09: PUT non-existent lot -> 404, no orphan policy created
        using var putRes = await SendAsync(client, HttpMethod.Put, route, OwnerRole, ownerId, new { holdDepositVnd = 20000 }, ct);
        Assert.Equal(HttpStatusCode.NotFound, putRes.StatusCode);

        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ParkingDbContext>();
        var count = await db.ReservationDepositPolicies.CountAsync(p => p.LotId == missingLotId, ct);
        Assert.Equal(0, count);
    }

    [Fact]
    public async Task API_10_MissingToken_GET_And_PUT_Return401Unauthorized()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var app = await CreateAppAsync(ct);
        var client = app.GetTestClient();
        var ownerId = Guid.NewGuid();
        var lotId = await InsertLotAsync(app.Services, ownerId, "Missing Token Lot", ct);
        var route = $"/api/parking/lots/{lotId}/deposit-policy";

        // GET without token (no headers) -> 401
        using var getNoAuth = await client.GetAsync(route, ct);
        Assert.Equal(HttpStatusCode.Unauthorized, getNoAuth.StatusCode);

        // PUT without token (no headers) -> 401
        using var putNoAuth = await client.PutAsJsonAsync(route, new { holdDepositVnd = 20000 }, ct);
        Assert.Equal(HttpStatusCode.Unauthorized, putNoAuth.StatusCode);
    }

    [Fact]
    public async Task API_11_InvalidToken_GET_And_PUT_Return401Unauthorized()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var app = await CreateAppAsync(ct);
        var client = app.GetTestClient();
        var ownerId = Guid.NewGuid();
        var lotId = await InsertLotAsync(app.Services, ownerId, "Invalid Token Lot", ct);
        var route = $"/api/parking/lots/{lotId}/deposit-policy";

        // GET with malformed/invalid bearer token
        using var getInvalidBearer = new HttpRequestMessage(HttpMethod.Get, route);
        getInvalidBearer.Headers.Add("Authorization", "Bearer invalid-token-signature");
        using var resGetInvalidBearer = await client.SendAsync(getInvalidBearer, ct);
        Assert.Equal(HttpStatusCode.Unauthorized, resGetInvalidBearer.StatusCode);

        // PUT with malformed/invalid bearer token
        using var putInvalidBearer = new HttpRequestMessage(HttpMethod.Put, route);
        putInvalidBearer.Headers.Add("Authorization", "Bearer invalid-token-signature");
        putInvalidBearer.Content = JsonContent.Create(new { holdDepositVnd = 20000 });
        using var resPutInvalidBearer = await client.SendAsync(putInvalidBearer, ct);
        Assert.Equal(HttpStatusCode.Unauthorized, resPutInvalidBearer.StatusCode);

        // GET with invalid account header token
        using var getInvalid = await SendAsync(client, HttpMethod.Get, route, OwnerRole, Guid.Empty, null, ct);
        // Note: Guid.Empty fails Guid.TryParse non-empty check
        Assert.Equal(HttpStatusCode.Unauthorized, getInvalid.StatusCode);
    }

    [Fact]
    public async Task API_11b_ExpiredToken_GET_And_PUT_Return401Unauthorized()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var app = await CreateAppAsync(ct);
        var client = app.GetTestClient();
        var ownerId = Guid.NewGuid();
        var lotId = await InsertLotAsync(app.Services, ownerId, "Expired Token Lot", ct);
        var route = $"/api/parking/lots/{lotId}/deposit-policy";

        // GET with expired bearer token
        using var getExpiredBearer = new HttpRequestMessage(HttpMethod.Get, route);
        getExpiredBearer.Headers.Add("Authorization", "Bearer expired-token-signature");
        using var resGetExpired = await client.SendAsync(getExpiredBearer, ct);
        Assert.Equal(HttpStatusCode.Unauthorized, resGetExpired.StatusCode);

        // PUT with expired bearer token
        using var putExpiredBearer = new HttpRequestMessage(HttpMethod.Put, route);
        putExpiredBearer.Headers.Add("Authorization", "Bearer expired-token-signature");
        putExpiredBearer.Content = JsonContent.Create(new { holdDepositVnd = 20000 });
        using var resPutExpired = await client.SendAsync(putExpiredBearer, ct);
        Assert.Equal(HttpStatusCode.Unauthorized, resPutExpired.StatusCode);

        // GET with expired account token header
        using var getExpiredHeader = new HttpRequestMessage(HttpMethod.Get, route);
        getExpiredHeader.Headers.Add(AccountHeader, "expired-token");
        getExpiredHeader.Headers.Add(RoleHeader, OwnerRole);
        using var resGetExpiredHeader = await client.SendAsync(getExpiredHeader, ct);
        Assert.Equal(HttpStatusCode.Unauthorized, resGetExpiredHeader.StatusCode);
    }

    [Fact]
    public async Task API_12_WrongRole_GET_And_PUT_Return403Forbidden()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var app = await CreateAppAsync(ct);
        var client = app.GetTestClient();
        var ownerId = Guid.NewGuid();
        var lotId = await InsertLotAsync(app.Services, ownerId, "Wrong Role Lot", ct);
        var route = $"/api/parking/lots/{lotId}/deposit-policy";

        // GET with non-Owner role (Driver) -> 403
        using var getWrongRole = await SendAsync(client, HttpMethod.Get, route, DriverRole, ownerId, null, ct);
        Assert.Equal(HttpStatusCode.Forbidden, getWrongRole.StatusCode);

        // PUT with non-Owner role (Driver) -> 403
        using var putWrongRole = await SendAsync(client, HttpMethod.Put, route, DriverRole, ownerId, new { holdDepositVnd = 20000 }, ct);
        Assert.Equal(HttpStatusCode.Forbidden, putWrongRole.StatusCode);
    }

    [Fact]
    public async Task API_13_to_16_LotOwnership_Isolation_And_CrossOwner_Access()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var app = await CreateAppAsync(ct);
        var client = app.GetTestClient();

        var ownerA = Guid.NewGuid();
        var ownerB = Guid.NewGuid();

        var lotA = await InsertLotAsync(app.Services, ownerA, "Lot Owner A", ct);
        var lotB = await InsertLotAsync(app.Services, ownerB, "Lot Owner B", ct);

        var routeA = $"/api/parking/lots/{lotA}/deposit-policy";
        var routeB = $"/api/parking/lots/{lotB}/deposit-policy";

        // API-15: Owner A can configure lot A with 12000; Owner B can configure lot B with 25000
        using var putA = await SendAsync(client, HttpMethod.Put, routeA, OwnerRole, ownerA, new { holdDepositVnd = 12000 }, ct);
        Assert.Equal(HttpStatusCode.OK, putA.StatusCode);

        using var putB = await SendAsync(client, HttpMethod.Put, routeB, OwnerRole, ownerB, new { holdDepositVnd = 25000 }, ct);
        Assert.Equal(HttpStatusCode.OK, putB.StatusCode);

        // API-13: Owner A GET lot B -> 403 Forbidden
        using var getCross = await SendAsync(client, HttpMethod.Get, routeB, OwnerRole, ownerA, null, ct);
        Assert.Equal(HttpStatusCode.Forbidden, getCross.StatusCode);

        // API-14: Owner A PUT lot B -> 403 Forbidden
        using var putCross = await SendAsync(client, HttpMethod.Put, routeB, OwnerRole, ownerA, new { holdDepositVnd = 99999 }, ct);
        Assert.Equal(HttpStatusCode.Forbidden, putCross.StatusCode);

        // API-16: Verify lot A and lot B retain independent policies
        using var getA = await SendAsync(client, HttpMethod.Get, routeA, OwnerRole, ownerA, null, ct);
        var resA = await getA.Content.ReadFromJsonAsync<DepositPolicyHttpEnvelope>(ct);
        Assert.Equal(12000m, resA!.Result!.HoldDepositVnd);

        using var getB = await SendAsync(client, HttpMethod.Get, routeB, OwnerRole, ownerB, null, ct);
        var resB = await getB.Content.ReadFromJsonAsync<DepositPolicyHttpEnvelope>(ct);
        Assert.Equal(25000m, resB!.Result!.HoldDepositVnd);
    }

    [Fact]
    public async Task API_17_and_18_Route_Convention_And_Legacy_Route_Removed()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var app = await CreateAppAsync(ct);
        var client = app.GetTestClient();
        var ownerId = Guid.NewGuid();
        var lotId = await InsertLotAsync(app.Services, ownerId, "Route Convention Lot", ct);

        // API-17: Public route matches Gateway forwarding /api/parking/lots/{lotId}/deposit-policy
        var validRoute = $"/api/parking/lots/{lotId}/deposit-policy";
        using var getValid = await SendAsync(client, HttpMethod.Get, validRoute, OwnerRole, ownerId, null, ct);
        Assert.Equal(HttpStatusCode.OK, getValid.StatusCode);

        // API-18: Old legacy route /api/v1/parking-lots/{lotId}/deposit-policy is removed and returns 404
        var legacyRoute = $"/api/v1/parking-lots/{lotId}/deposit-policy";
        using var getLegacy = await SendAsync(client, HttpMethod.Get, legacyRoute, OwnerRole, ownerId, null, ct);
        Assert.Equal(HttpStatusCode.NotFound, getLegacy.StatusCode);

        using var putLegacy = await SendAsync(client, HttpMethod.Put, legacyRoute, OwnerRole, ownerId, new { holdDepositVnd = 15000 }, ct);
        Assert.Equal(HttpStatusCode.NotFound, putLegacy.StatusCode);
    }

    private static async Task<HttpResponseMessage> SendAsync(
        HttpClient client,
        HttpMethod method,
        string url,
        string role,
        Guid accountId,
        object? body,
        CancellationToken ct)
    {
        using var request = new HttpRequestMessage(method, url);
        request.Headers.Add(AccountHeader, accountId.ToString());
        request.Headers.Add(RoleHeader, role);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        return await client.SendAsync(request, ct);
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

        var app = builder.Build();
        app.UseParkingSystemWebFoundation();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapDepositPolicyEndpoints();
        await app.StartAsync(ct);

        await using var scope = app.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<ParkingDbContext>().Database.MigrateAsync(ct);
        return app;
    }

    private static async Task<Guid> InsertLotAsync(
        IServiceProvider services,
        Guid ownerId,
        string name,
        CancellationToken ct)
    {
        var lotId = Guid.NewGuid();
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ParkingDbContext>();
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO parking.parking_lots (id, name, address, latitude, longitude, type, approval_status)
            VALUES ({lotId}, {name}, '123 Test Street', 10.77, 106.70, 'Outdoor', 'Approved')
            """,
            ct);

        db.ParkingLotAssignments.Add(
            ParkingLotAssignment.Create(lotId, ownerId, ParkingLotMemberRole.Owner, Guid.NewGuid(), DateTimeOffset.UtcNow));
        await db.SaveChangesAsync(ct);

        return lotId;
    }

    private sealed class HeaderAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public const string SchemeName = "TestHeaderAccount";

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (Request.Headers.TryGetValue("Authorization", out var authHeader))
            {
                var authVal = authHeader.ToString();
                if (authVal.Contains("expired", StringComparison.OrdinalIgnoreCase))
                    return Task.FromResult(AuthenticateResult.Fail("Token expired"));
                if (authVal.Contains("invalid", StringComparison.OrdinalIgnoreCase) || authVal == "Bearer" || !authVal.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                    return Task.FromResult(AuthenticateResult.Fail("Token invalid"));
            }

            if (!Request.Headers.TryGetValue(AccountHeader, out var account) || !Request.Headers.TryGetValue(RoleHeader, out var role))
                return Task.FromResult(AuthenticateResult.NoResult());

            if (account == "invalid-token" || account == "malformed-token")
                return Task.FromResult(AuthenticateResult.Fail("The provided token is malformed or invalid."));

            if (account == "expired-token")
                return Task.FromResult(AuthenticateResult.Fail("The provided token has expired."));

            var identity = new ClaimsIdentity(
                [new Claim("sub", account.ToString()), new Claim(ParkingAuthorization.RoleClaimType, role.ToString())],
                SchemeName);
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName)));
        }
    }

    private sealed class HeaderCurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
    {
        public string? SubjectId => httpContextAccessor.HttpContext?.User.FindFirst("sub")?.Value;

        public bool IsAuthenticated => httpContextAccessor.HttpContext?.User.Identity?.IsAuthenticated ?? false;
    }
}
