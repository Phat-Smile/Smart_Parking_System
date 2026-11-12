using System;
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

public sealed class ReservationAvailabilityPolicyHttpApiTests(PostgreSqlContainerFixture postgres)
    : IClassFixture<PostgreSqlContainerFixture>
{
    private const string AccountHeader = "X-Test-Account";
    private const string RoleHeader = "X-Test-Role";
    private const string OwnerRole = "ParkingLotOwner";
    private const string DriverRole = "Driver";

    private sealed record AvailabilityPolicyHttpEnvelope(
        AvailabilityPolicyHttpResult? Result,
        bool IsSuccess,
        int StatusCode,
        string? Message);

    private sealed record AvailabilityPolicyHttpResult(
        Guid LotId,
        bool ReservationEnabled,
        bool IsConfigured,
        DateTime? UpdatedAtUtc);

    [Fact]
    public async Task API_01_to_05_ReservationAvailabilityPolicy_Lifecycle_HappyPath()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var app = await CreateAppAsync(ct);
        var client = app.GetTestClient();
        var ownerId = Guid.NewGuid();
        var lotId = await InsertLotAsync(app.Services, ownerId, "Happy Path Availability Lot", ct);

        var route = $"/api/parking/lots/{lotId}/reservation-availability";

        // API-01: GET lot unconfigured -> 200, reservationEnabled = true, isConfigured = false, updatedAtUtc = null
        using var get1 = await SendAsync(client, HttpMethod.Get, route, OwnerRole, ownerId, null, ct);
        Assert.Equal(HttpStatusCode.OK, get1.StatusCode);
        var res1 = await get1.Content.ReadFromJsonAsync<AvailabilityPolicyHttpEnvelope>(ct);
        Assert.NotNull(res1);
        Assert.True(res1.IsSuccess);
        Assert.NotNull(res1.Result);
        Assert.Equal(lotId, res1.Result.LotId);
        Assert.True(res1.Result.ReservationEnabled);
        Assert.False(res1.Result.IsConfigured);
        Assert.Null(res1.Result.UpdatedAtUtc);

        // Verify DB still has 0 rows for this lot
        await using (var scope = app.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ParkingDbContext>();
            var count = await db.ReservationAvailabilityPolicies.CountAsync(p => p.LotId == lotId, ct);
            Assert.Equal(0, count);
        }

        // API-02: PUT lot with false -> 200, reservationEnabled = false, isConfigured = true
        using var put2 = await SendAsync(client, HttpMethod.Put, route, OwnerRole, ownerId, new { reservationEnabled = false }, ct);
        Assert.Equal(HttpStatusCode.OK, put2.StatusCode);
        var res2 = await put2.Content.ReadFromJsonAsync<AvailabilityPolicyHttpEnvelope>(ct);
        Assert.NotNull(res2);
        Assert.True(res2.IsSuccess);
        Assert.False(res2.Result!.ReservationEnabled);
        Assert.True(res2.Result.IsConfigured);
        Assert.NotNull(res2.Result.UpdatedAtUtc);

        // API-03: GET lot configured -> 200, reservationEnabled = false, isConfigured = true
        using var get3 = await SendAsync(client, HttpMethod.Get, route, OwnerRole, ownerId, null, ct);
        Assert.Equal(HttpStatusCode.OK, get3.StatusCode);
        var res3 = await get3.Content.ReadFromJsonAsync<AvailabilityPolicyHttpEnvelope>(ct);
        Assert.NotNull(res3);
        Assert.True(res3.IsSuccess);
        Assert.False(res3.Result!.ReservationEnabled);
        Assert.True(res3.Result.IsConfigured);

        // API-04: PUT lot back to true -> 200, reservationEnabled = true
        using var put4 = await SendAsync(client, HttpMethod.Put, route, OwnerRole, ownerId, new { reservationEnabled = true }, ct);
        Assert.Equal(HttpStatusCode.OK, put4.StatusCode);
        var res4 = await put4.Content.ReadFromJsonAsync<AvailabilityPolicyHttpEnvelope>(ct);
        Assert.NotNull(res4);
        Assert.True(res4.IsSuccess);
        Assert.True(res4.Result!.ReservationEnabled);
        Assert.True(res4.Result.IsConfigured);

        // API-05: Verify exactly 1 row in DB
        await using (var scope = app.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ParkingDbContext>();
            var count = await db.ReservationAvailabilityPolicies.CountAsync(p => p.LotId == lotId, ct);
            Assert.Equal(1, count);
        }
    }

    [Fact]
    public async Task API_06_PUT_WithMissingOrNull_Returns400()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var app = await CreateAppAsync(ct);
        var client = app.GetTestClient();
        var ownerId = Guid.NewGuid();
        var lotId = await InsertLotAsync(app.Services, ownerId, "Invalid Payload Lot", ct);
        var route = $"/api/parking/lots/{lotId}/reservation-availability";

        // Missing field
        using var putMissing = await SendAsync(client, HttpMethod.Put, route, OwnerRole, ownerId, new { }, ct);
        Assert.Equal(HttpStatusCode.BadRequest, putMissing.StatusCode);

        // Null value
        using var putNull = await SendAsync(client, HttpMethod.Put, route, OwnerRole, ownerId, new { reservationEnabled = (bool?)null }, ct);
        Assert.Equal(HttpStatusCode.BadRequest, putNull.StatusCode);
    }

    [Fact]
    public async Task API_07_PUT_WithInvalidType_Returns400()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var app = await CreateAppAsync(ct);
        var client = app.GetTestClient();
        var ownerId = Guid.NewGuid();
        var lotId = await InsertLotAsync(app.Services, ownerId, "Invalid Type Lot", ct);
        var route = $"/api/parking/lots/{lotId}/reservation-availability";

        // String instead of bool
        using var putString = await SendAsync(client, HttpMethod.Put, route, OwnerRole, ownerId, new { reservationEnabled = "not-a-bool" }, ct);
        Assert.Equal(HttpStatusCode.BadRequest, putString.StatusCode);

        // Number instead of bool
        using var putNumber = await SendAsync(client, HttpMethod.Put, route, OwnerRole, ownerId, new { reservationEnabled = 123 }, ct);
        Assert.Equal(HttpStatusCode.BadRequest, putNumber.StatusCode);
    }

    [Fact]
    public async Task API_08_NonExistentLot_Returns404()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var app = await CreateAppAsync(ct);
        var client = app.GetTestClient();
        var ownerId = Guid.NewGuid();
        var missingLotId = Guid.NewGuid();
        var route = $"/api/parking/lots/{missingLotId}/reservation-availability";

        using var getRes = await SendAsync(client, HttpMethod.Get, route, OwnerRole, ownerId, null, ct);
        Assert.Equal(HttpStatusCode.NotFound, getRes.StatusCode);

        using var putRes = await SendAsync(client, HttpMethod.Put, route, OwnerRole, ownerId, new { reservationEnabled = false }, ct);
        Assert.Equal(HttpStatusCode.NotFound, putRes.StatusCode);
    }

    [Fact]
    public async Task API_09_MissingToken_Returns401Unauthorized()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var app = await CreateAppAsync(ct);
        var client = app.GetTestClient();
        var ownerId = Guid.NewGuid();
        var lotId = await InsertLotAsync(app.Services, ownerId, "Missing Token Lot", ct);
        var route = $"/api/parking/lots/{lotId}/reservation-availability";

        using var getNoAuth = await client.GetAsync(route, ct);
        Assert.Equal(HttpStatusCode.Unauthorized, getNoAuth.StatusCode);

        using var putNoAuth = await client.PutAsJsonAsync(route, new { reservationEnabled = true }, ct);
        Assert.Equal(HttpStatusCode.Unauthorized, putNoAuth.StatusCode);
    }

    [Fact]
    public async Task API_10_WrongRole_Returns403Forbidden()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var app = await CreateAppAsync(ct);
        var client = app.GetTestClient();
        var ownerId = Guid.NewGuid();
        var lotId = await InsertLotAsync(app.Services, ownerId, "Driver Role Lot", ct);
        var route = $"/api/parking/lots/{lotId}/reservation-availability";

        using var getDriver = await SendAsync(client, HttpMethod.Get, route, DriverRole, ownerId, null, ct);
        Assert.Equal(HttpStatusCode.Forbidden, getDriver.StatusCode);

        using var putDriver = await SendAsync(client, HttpMethod.Put, route, DriverRole, ownerId, new { reservationEnabled = true }, ct);
        Assert.Equal(HttpStatusCode.Forbidden, putDriver.StatusCode);
    }

    [Fact]
    public async Task API_11_DifferentOwner_Returns403Forbidden()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var app = await CreateAppAsync(ct);
        var client = app.GetTestClient();
        var realOwnerId = Guid.NewGuid();
        var otherOwnerId = Guid.NewGuid();
        var lotId = await InsertLotAsync(app.Services, realOwnerId, "Other Owner Lot", ct);
        var route = $"/api/parking/lots/{lotId}/reservation-availability";

        using var getOther = await SendAsync(client, HttpMethod.Get, route, OwnerRole, otherOwnerId, null, ct);
        Assert.Equal(HttpStatusCode.Forbidden, getOther.StatusCode);

        using var putOther = await SendAsync(client, HttpMethod.Put, route, OwnerRole, otherOwnerId, new { reservationEnabled = true }, ct);
        Assert.Equal(HttpStatusCode.Forbidden, putOther.StatusCode);
    }

    [Fact]
    public async Task API_12_IdempotentPUT_ReturnsIdenticalResult()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var app = await CreateAppAsync(ct);
        var client = app.GetTestClient();
        var ownerId = Guid.NewGuid();
        var lotId = await InsertLotAsync(app.Services, ownerId, "Idempotent Lot", ct);
        var route = $"/api/parking/lots/{lotId}/reservation-availability";

        using var put1 = await SendAsync(client, HttpMethod.Put, route, OwnerRole, ownerId, new { reservationEnabled = false }, ct);
        Assert.Equal(HttpStatusCode.OK, put1.StatusCode);
        var res1 = await put1.Content.ReadFromJsonAsync<AvailabilityPolicyHttpEnvelope>(ct);

        using var put2 = await SendAsync(client, HttpMethod.Put, route, OwnerRole, ownerId, new { reservationEnabled = false }, ct);
        Assert.Equal(HttpStatusCode.OK, put2.StatusCode);
        var res2 = await put2.Content.ReadFromJsonAsync<AvailabilityPolicyHttpEnvelope>(ct);

        Assert.Equal(res1!.Result!.ReservationEnabled, res2!.Result!.ReservationEnabled);

        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ParkingDbContext>();
        var count = await db.ReservationAvailabilityPolicies.CountAsync(p => p.LotId == lotId, ct);
        Assert.Equal(1, count);
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

        if (body != null)
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
        app.MapReservationAvailabilityPolicyEndpoints();
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
            VALUES ({lotId}, {name}, '123 Availability Test Street', 10.77, 106.70, 'Outdoor', 'Approved')
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
            if (!Request.Headers.TryGetValue(AccountHeader, out var account) || !Request.Headers.TryGetValue(RoleHeader, out var role))
                return Task.FromResult(AuthenticateResult.NoResult());

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
