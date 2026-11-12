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

public sealed class ReservationRatioPolicyHttpApiTests(PostgreSqlContainerFixture postgres)
    : IClassFixture<PostgreSqlContainerFixture>
{
    private const string AccountHeader = "X-Test-Account";
    private const string RoleHeader = "X-Test-Role";
    private const string OwnerRole = "ParkingLotOwner";
    private const string DriverRole = "Driver";

    private sealed record RatioPolicyHttpEnvelope(
        RatioPolicyHttpResult? Result,
        bool IsSuccess,
        int StatusCode,
        string? Message);

    private sealed record RatioPolicyHttpResult(
        Guid LotId,
        int ReservationRatioPercent,
        bool IsConfigured,
        DateTime? UpdatedAtUtc);

    [Fact]
    public async Task API_01_to_05_ReservationRatioPolicy_Lifecycle_HappyPath()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var app = await CreateAppAsync(ct);
        var client = app.GetTestClient();
        var ownerId = Guid.NewGuid();
        var lotId = await InsertLotAsync(app.Services, ownerId, "Happy Path Ratio Lot", ct);

        var route = $"/api/parking/lots/{lotId}/reservation-ratio-policy";

        // API-01: GET lot unconfigured -> 200, 50%, isConfigured = false, updatedAtUtc = null
        using var get1 = await SendAsync(client, HttpMethod.Get, route, OwnerRole, ownerId, null, ct);
        Assert.Equal(HttpStatusCode.OK, get1.StatusCode);
        var res1 = await get1.Content.ReadFromJsonAsync<RatioPolicyHttpEnvelope>(ct);
        Assert.NotNull(res1);
        Assert.True(res1.IsSuccess);
        Assert.NotNull(res1.Result);
        Assert.Equal(lotId, res1.Result.LotId);
        Assert.Equal(50, res1.Result.ReservationRatioPercent);
        Assert.False(res1.Result.IsConfigured);
        Assert.Null(res1.Result.UpdatedAtUtc);

        // API-02: PUT lot with 30 -> 200, 30%, isConfigured = true
        using var put2 = await SendAsync(client, HttpMethod.Put, route, OwnerRole, ownerId, new { reservationRatioPercent = 30 }, ct);
        Assert.Equal(HttpStatusCode.OK, put2.StatusCode);
        var res2 = await put2.Content.ReadFromJsonAsync<RatioPolicyHttpEnvelope>(ct);
        Assert.NotNull(res2);
        Assert.True(res2.IsSuccess);
        Assert.Equal(30, res2.Result!.ReservationRatioPercent);
        Assert.True(res2.Result.IsConfigured);
        Assert.NotNull(res2.Result.UpdatedAtUtc);

        // API-03: GET after API-02 -> 200, 30%, isConfigured = true
        using var get3 = await SendAsync(client, HttpMethod.Get, route, OwnerRole, ownerId, null, ct);
        Assert.Equal(HttpStatusCode.OK, get3.StatusCode);
        var res3 = await get3.Content.ReadFromJsonAsync<RatioPolicyHttpEnvelope>(ct);
        Assert.NotNull(res3);
        Assert.True(res3.IsSuccess);
        Assert.Equal(30, res3.Result!.ReservationRatioPercent);
        Assert.True(res3.Result.IsConfigured);
        Assert.NotNull(res3.Result.UpdatedAtUtc);

        // API-04: PUT lot with 0% -> 200, 0, isConfigured = true
        using var put4 = await SendAsync(client, HttpMethod.Put, route, OwnerRole, ownerId, new { reservationRatioPercent = 0 }, ct);
        Assert.Equal(HttpStatusCode.OK, put4.StatusCode);
        var res4 = await put4.Content.ReadFromJsonAsync<RatioPolicyHttpEnvelope>(ct);
        Assert.NotNull(res4);
        Assert.True(res4.IsSuccess);
        Assert.Equal(0, res4.Result!.ReservationRatioPercent);
        Assert.True(res4.Result.IsConfigured);

        // API-05: PUT lot with 100% -> 200, 100, isConfigured = true
        using var put5 = await SendAsync(client, HttpMethod.Put, route, OwnerRole, ownerId, new { reservationRatioPercent = 100 }, ct);
        Assert.Equal(HttpStatusCode.OK, put5.StatusCode);
        var res5 = await put5.Content.ReadFromJsonAsync<RatioPolicyHttpEnvelope>(ct);
        Assert.NotNull(res5);
        Assert.True(res5.IsSuccess);
        Assert.Equal(100, res5.Result!.ReservationRatioPercent);
        Assert.True(res5.Result.IsConfigured);

        // GET after 100% -> 200, 100
        using var getFinal = await SendAsync(client, HttpMethod.Get, route, OwnerRole, ownerId, null, ct);
        Assert.Equal(HttpStatusCode.OK, getFinal.StatusCode);
        var resFinal = await getFinal.Content.ReadFromJsonAsync<RatioPolicyHttpEnvelope>(ct);
        Assert.Equal(100, resFinal!.Result!.ReservationRatioPercent);
    }

    [Fact]
    public async Task API_06_to_09_InvalidValues_Negative_Exceeding100_And_Fractional_Are_Rejected()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var app = await CreateAppAsync(ct);
        var client = app.GetTestClient();
        var ownerId = Guid.NewGuid();
        var lotId = await InsertLotAsync(app.Services, ownerId, "Validation Ratio Lot", ct);
        var route = $"/api/parking/lots/{lotId}/reservation-ratio-policy";

        // Pre-configure to 40
        using var initPut = await SendAsync(client, HttpMethod.Put, route, OwnerRole, ownerId, new { reservationRatioPercent = 40 }, ct);
        Assert.Equal(HttpStatusCode.OK, initPut.StatusCode);

        // API-06: PUT with negative ratio (-1) -> 400
        using var putNegative = await SendAsync(client, HttpMethod.Put, route, OwnerRole, ownerId, new { reservationRatioPercent = -1 }, ct);
        Assert.Equal(HttpStatusCode.BadRequest, putNegative.StatusCode);

        // API-07: PUT with ratio exceeding 100 (101) -> 400
        using var putExceeding = await SendAsync(client, HttpMethod.Put, route, OwnerRole, ownerId, new { reservationRatioPercent = 101 }, ct);
        Assert.Equal(HttpStatusCode.BadRequest, putExceeding.StatusCode);

        // API-08: PUT with fractional number (50.5) -> 400
        using var putFractional = await SendAsync(client, HttpMethod.Put, route, OwnerRole, ownerId, new { reservationRatioPercent = 50.5 }, ct);
        Assert.Equal(HttpStatusCode.BadRequest, putFractional.StatusCode);

        // API-09: PUT with missing field / empty body -> 400
        using var putMissing = await SendAsync(client, HttpMethod.Put, route, OwnerRole, ownerId, new { }, ct);
        Assert.Equal(HttpStatusCode.BadRequest, putMissing.StatusCode);

        // Verify DB unchanged: still 40
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ParkingDbContext>();
        var policy = await db.ReservationRatioPolicies.FirstOrDefaultAsync(p => p.LotId == lotId, ct);
        Assert.NotNull(policy);
        Assert.Equal(40, policy.ReservationRatioPercent);
    }

    [Fact]
    public async Task API_10_NonExistentLot_Returns404()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var app = await CreateAppAsync(ct);
        var client = app.GetTestClient();
        var ownerId = Guid.NewGuid();
        var missingLotId = Guid.NewGuid();
        var route = $"/api/parking/lots/{missingLotId}/reservation-ratio-policy";

        using var getRes = await SendAsync(client, HttpMethod.Get, route, OwnerRole, ownerId, null, ct);
        Assert.Equal(HttpStatusCode.NotFound, getRes.StatusCode);

        using var putRes = await SendAsync(client, HttpMethod.Put, route, OwnerRole, ownerId, new { reservationRatioPercent = 50 }, ct);
        Assert.Equal(HttpStatusCode.NotFound, putRes.StatusCode);

        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ParkingDbContext>();
        var count = await db.ReservationRatioPolicies.CountAsync(p => p.LotId == missingLotId, ct);
        Assert.Equal(0, count);
    }

    [Fact]
    public async Task API_11_MissingToken_GET_And_PUT_Return401Unauthorized()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var app = await CreateAppAsync(ct);
        var client = app.GetTestClient();
        var ownerId = Guid.NewGuid();
        var lotId = await InsertLotAsync(app.Services, ownerId, "Missing Token Ratio Lot", ct);
        var route = $"/api/parking/lots/{lotId}/reservation-ratio-policy";

        using var getNoAuth = await client.GetAsync(route, ct);
        Assert.Equal(HttpStatusCode.Unauthorized, getNoAuth.StatusCode);

        using var putNoAuth = await client.PutAsJsonAsync(route, new { reservationRatioPercent = 50 }, ct);
        Assert.Equal(HttpStatusCode.Unauthorized, putNoAuth.StatusCode);
    }

    [Fact]
    public async Task API_12_WrongRole_GET_And_PUT_Return403Forbidden()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var app = await CreateAppAsync(ct);
        var client = app.GetTestClient();
        var ownerId = Guid.NewGuid();
        var lotId = await InsertLotAsync(app.Services, ownerId, "Wrong Role Ratio Lot", ct);
        var route = $"/api/parking/lots/{lotId}/reservation-ratio-policy";

        using var getWrongRole = await SendAsync(client, HttpMethod.Get, route, DriverRole, ownerId, null, ct);
        Assert.Equal(HttpStatusCode.Forbidden, getWrongRole.StatusCode);

        using var putWrongRole = await SendAsync(client, HttpMethod.Put, route, DriverRole, ownerId, new { reservationRatioPercent = 50 }, ct);
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

        var lotA = await InsertLotAsync(app.Services, ownerA, "Ratio Lot Owner A", ct);
        var lotB = await InsertLotAsync(app.Services, ownerB, "Ratio Lot Owner B", ct);

        var routeA = $"/api/parking/lots/{lotA}/reservation-ratio-policy";
        var routeB = $"/api/parking/lots/{lotB}/reservation-ratio-policy";

        // Owner A configures lot A with 20%; Owner B configures lot B with 70%
        using var putA = await SendAsync(client, HttpMethod.Put, routeA, OwnerRole, ownerA, new { reservationRatioPercent = 20 }, ct);
        Assert.Equal(HttpStatusCode.OK, putA.StatusCode);

        using var putB = await SendAsync(client, HttpMethod.Put, routeB, OwnerRole, ownerB, new { reservationRatioPercent = 70 }, ct);
        Assert.Equal(HttpStatusCode.OK, putB.StatusCode);

        // Cross-owner: Owner A GET lot B -> 403 Forbidden
        using var getCross = await SendAsync(client, HttpMethod.Get, routeB, OwnerRole, ownerA, null, ct);
        Assert.Equal(HttpStatusCode.Forbidden, getCross.StatusCode);

        // Cross-owner: Owner A PUT lot B -> 403 Forbidden
        using var putCross = await SendAsync(client, HttpMethod.Put, routeB, OwnerRole, ownerA, new { reservationRatioPercent = 90 }, ct);
        Assert.Equal(HttpStatusCode.Forbidden, putCross.StatusCode);

        // Verify isolation
        using var getA = await SendAsync(client, HttpMethod.Get, routeA, OwnerRole, ownerA, null, ct);
        var resA = await getA.Content.ReadFromJsonAsync<RatioPolicyHttpEnvelope>(ct);
        Assert.Equal(20, resA!.Result!.ReservationRatioPercent);

        using var getB = await SendAsync(client, HttpMethod.Get, routeB, OwnerRole, ownerB, null, ct);
        var resB = await getB.Content.ReadFromJsonAsync<RatioPolicyHttpEnvelope>(ct);
        Assert.Equal(70, resB!.Result!.ReservationRatioPercent);
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
        app.MapReservationRatioPolicyEndpoints();
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
            VALUES ({lotId}, {name}, '123 Ratio Test Street', 10.77, 106.70, 'Outdoor', 'Approved')
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
