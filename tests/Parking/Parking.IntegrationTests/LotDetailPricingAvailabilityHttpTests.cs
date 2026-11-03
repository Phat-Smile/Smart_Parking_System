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
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ParkingSystem.AspNetCore;
using ParkingSystem.BuildingBlocks.Identity;
using ParkingSystem.BuildingBlocks.Time;
using ParkingSystem.Parking.Application;
using ParkingSystem.Parking.Application.Common.Interfaces;
using ParkingSystem.Parking.Application.ParkingLayout;
using ParkingSystem.Parking.Application.ParkingLayout.ManageLayout;
using ParkingSystem.BuildingBlocks.Results;
using ParkingSystem.Parking.Application.ParkingLots;
using ParkingSystem.Parking.Application.ParkingLots.AdminLots;
using ParkingSystem.Parking.Domain.Entities;
using ParkingSystem.Parking.WebAPI.Endpoints;
using ParkingSystem.Parking.WebAPI.Security;
using Xunit;

namespace ParkingSystem.Tests.Parking.IntegrationTests;

// Demo 1 steps 10 and 13 at the HTTP layer, without Docker: the repositories are in memory.
public sealed class LotDetailPricingAvailabilityHttpTests
{
    private const string RoleHeader = "X-Test-Role";
    private const string UserHeader = "X-Test-User";
    private static readonly Guid ApprovedLot = Guid.Parse("a0000000-0000-0000-0000-000000000001");
    private static readonly Guid PendingLot = Guid.Parse("a0000000-0000-0000-0000-000000000002");
    private static readonly Guid RejectedLot = Guid.Parse("a0000000-0000-0000-0000-000000000003");
    private static readonly Guid Owner = Guid.Parse("b0000000-0000-0000-0000-000000000001");
    private static readonly Guid OtherOwner = Guid.Parse("b0000000-0000-0000-0000-000000000002");

    [Fact]
    public async Task Approved_layout_is_public_and_contains_slot_states_and_geometry()
    {
        await using var app = await CreateAppAsync();

        var body = await GetOkJsonAsync(app, $"/api/parking/lots/{ApprovedLot}/layout", role: null, user: null);

        Assert.Equal(ApprovedLot, body.GetProperty("lotId").GetGuid());
        Assert.Equal(3, body.GetProperty("version").GetInt32());
        Assert.True(body.TryGetProperty("asOfUtc", out _));
        var zones = body.GetProperty("floors")[0].GetProperty("zones");
        var car = zones[0];
        Assert.Equal("Occupied", car.GetProperty("slots")[0].GetProperty("effectiveStatus").GetString());
        Assert.Equal(1, car.GetProperty("slots")[0].GetProperty("position").GetProperty("x").GetInt32());
    }

    [Fact]
    public async Task Pending_and_unknown_lots_do_not_expose_layout_publicly()
    {
        await using var app = await CreateAppAsync();

        using var pending = await SendAsync(app, $"/api/parking/lots/{PendingLot}/layout", null, null);
        using var unknown = await SendAsync(app, $"/api/parking/lots/{Guid.NewGuid()}/layout", null, null);

        await AssertProblemAsync(pending, HttpStatusCode.NotFound, "parking.layout.lot_not_found");
        await AssertProblemAsync(unknown, HttpStatusCode.NotFound, "parking.layout.lot_not_found");
    }

    [Theory]
    [InlineData("Customer")]
    [InlineData("Staff")]
    [InlineData("ParkingLotOwner")]
    [InlineData("Admin")]
    public async Task Approved_layout_is_readable_without_role_restrictions(string role)
    {
        await using var app = await CreateAppAsync();

        var body = await GetOkJsonAsync(app, $"/api/parking/lots/{ApprovedLot}/layout", role, Owner);

        Assert.Equal(ApprovedLot, body.GetProperty("lotId").GetGuid());
    }

    [Fact]
    public async Task Pending_layout_remains_private_even_to_its_owner_on_the_public_route()
    {
        await using var app = await CreateAppAsync();

        using var response = await SendAsync(app, $"/api/parking/lots/{PendingLot}/layout", "ParkingLotOwner", Owner);

        await AssertProblemAsync(response, HttpStatusCode.NotFound, "parking.layout.lot_not_found");
    }

    [Fact]
    public async Task Rejected_layout_is_not_public()
    {
        await using var app = await CreateAppAsync();

        using var response = await SendAsync(app, $"/api/parking/lots/{RejectedLot}/layout", "ParkingLotOwner", Owner);

        await AssertProblemAsync(response, HttpStatusCode.NotFound, "parking.layout.lot_not_found");
    }

    [Fact]
    public async Task Empty_guid_cannot_disclose_a_layout()
    {
        await using var app = await CreateAppAsync();

        using var response = await SendAsync(app, $"/api/parking/lots/{Guid.Empty}/layout", null, null);

        await AssertProblemAsync(response, HttpStatusCode.NotFound, "parking.layout.lot_not_found");
    }

    [Fact]
    public async Task Layout_response_is_not_cached_on_success_or_not_found()
    {
        await using var app = await CreateAppAsync();

        using var found = await SendAsync(app, $"/api/parking/lots/{ApprovedLot}/layout", null, null);
        using var hidden = await SendAsync(app, $"/api/parking/lots/{PendingLot}/layout", null, null);

        Assert.Equal(HttpStatusCode.OK, found.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, hidden.StatusCode);
        Assert.Equal("no-store", found.Headers.CacheControl?.ToString());
        Assert.Equal("no-store", hidden.Headers.CacheControl?.ToString());
    }

    [Fact]
    public async Task Layout_contains_multiple_floors_and_both_vehicle_groups()
    {
        await using var app = await CreateAppAsync();

        var body = await GetOkJsonAsync(app, $"/api/parking/lots/{ApprovedLot}/layout", null, null);

        var floors = body.GetProperty("floors");
        Assert.Equal(2, floors.GetArrayLength());
        Assert.Equal("Ground", floors[0].GetProperty("name").GetString());
        Assert.Equal("Upper", floors[1].GetProperty("name").GetString());
        var zones = floors[0].GetProperty("zones");
        Assert.Equal("Car", zones[0].GetProperty("vehicleGroup").GetString());
        Assert.Equal("Motorbike", zones[1].GetProperty("vehicleGroup").GetString());
        Assert.Equal(20, zones[1].GetProperty("capacity").GetInt32());
        Assert.Equal(5, zones[1].GetProperty("currentVehicleCount").GetInt32());
        Assert.False(zones[1].GetProperty("hasSlotSensor").GetBoolean());
        Assert.Empty(zones[1].GetProperty("slots").EnumerateArray());
    }

    [Fact]
    public async Task Layout_preserves_bounds_polygon_and_slot_coordinates()
    {
        await using var app = await CreateAppAsync();

        var body = await GetOkJsonAsync(app, $"/api/parking/lots/{ApprovedLot}/layout", null, null);

        var zones = body.GetProperty("floors")[0].GetProperty("zones");
        var carSlots = zones[0].GetProperty("slots");
        Assert.Equal("A-06", carSlots[5].GetProperty("slotCode").GetString());
        Assert.Equal(6, carSlots[5].GetProperty("position").GetProperty("x").GetInt32());
        Assert.Equal(1, carSlots[5].GetProperty("position").GetProperty("y").GetInt32());
        Assert.Equal(5, zones[1].GetProperty("bounds").GetProperty("width").GetInt32());
        Assert.Equal(JsonValueKind.Null, zones[1].GetProperty("polygon").ValueKind);
        var polygon = body.GetProperty("floors")[1].GetProperty("zones")[0].GetProperty("polygon");
        Assert.Equal(3, polygon.GetArrayLength());
        Assert.Equal(4, polygon[1].GetProperty("x").GetInt32());
        Assert.Equal(5, polygon[2].GetProperty("y").GetInt32());
    }

    [Theory]
    [InlineData(0, "Occupied")]
    [InlineData(1, "Available")]
    [InlineData(2, "Maintenance")]
    [InlineData(3, "Unavailable")]
    [InlineData(4, "Assigned")]
    [InlineData(5, "NeedsVerification")]
    public async Task Layout_exposes_each_effective_slot_status(int index, string expected)
    {
        await using var app = await CreateAppAsync();

        var body = await GetOkJsonAsync(app, $"/api/parking/lots/{ApprovedLot}/layout", null, null);

        var slot = body.GetProperty("floors")[0].GetProperty("zones")[0].GetProperty("slots")[index];
        Assert.Equal(expected, slot.GetProperty("effectiveStatus").GetString());
    }

    [Fact]
    public async Task Layout_timestamp_is_a_valid_utc_instant()
    {
        await using var app = await CreateAppAsync();

        var body = await GetOkJsonAsync(app, $"/api/parking/lots/{ApprovedLot}/layout", null, null);

        var instant = body.GetProperty("asOfUtc").GetDateTimeOffset();
        Assert.Equal(TimeSpan.Zero, instant.Offset);
        Assert.True(instant > DateTimeOffset.UnixEpoch);
    }

    [Fact]
    public async Task Anyone_reads_every_price_of_an_approved_lot()
    {
        await using var app = await CreateAppAsync();

        var body = await GetOkJsonAsync(app, $"/api/parking/lots/{ApprovedLot}/pricing", role: null, user: null);

        Assert.Equal(ApprovedLot, body.GetProperty("lotId").GetGuid());
        var items = body.GetProperty("items").EnumerateArray().ToList();
        Assert.Equal(2, items.Count);
        Assert.Equal("CAR", items[0].GetProperty("vehicleType").GetString());
        Assert.Equal(30000m, items[0].GetProperty("price").GetDecimal());
    }

    [Fact]
    public async Task Prices_of_a_pending_lot_are_404_for_others_and_200_for_its_owner()
    {
        await using var app = await CreateAppAsync();

        using var anonymous = await SendAsync(app, $"/api/parking/lots/{PendingLot}/pricing", role: null, user: null);
        using var other = await SendAsync(app, $"/api/parking/lots/{PendingLot}/pricing", "ParkingLotOwner", OtherOwner);
        using var owner = await SendAsync(app, $"/api/parking/lots/{PendingLot}/pricing", "ParkingLotOwner", Owner);

        await AssertProblemAsync(anonymous, HttpStatusCode.NotFound, "parking.lot_not_found");
        await AssertProblemAsync(other, HttpStatusCode.NotFound, "parking.lot_not_found");
        Assert.Equal(HttpStatusCode.OK, owner.StatusCode);
    }

    [Fact]
    public async Task Anyone_reads_the_free_spaces_of_an_approved_lot()
    {
        await using var app = await CreateAppAsync();

        var body = await GetOkJsonAsync(app, $"/api/parking/lots/{ApprovedLot}/availability", role: null, user: null);

        Assert.Equal(7, body.GetProperty("car").GetProperty("available").GetInt32());
        var motorbike = body.GetProperty("motorbike");
        Assert.Equal(40, motorbike.GetProperty("capacity").GetInt32());
        Assert.Equal(30, motorbike.GetProperty("available").GetInt32());
        Assert.Equal("M1", motorbike.GetProperty("zones")[0].GetProperty("zoneCode").GetString());
        Assert.True(body.TryGetProperty("asOfUtc", out _));
    }

    [Fact]
    public async Task Free_spaces_of_a_pending_lot_are_404()
    {
        await using var app = await CreateAppAsync();

        using var response = await SendAsync(app, $"/api/parking/lots/{PendingLot}/availability", "ParkingLotOwner", Owner);

        await AssertProblemAsync(response, HttpStatusCode.NotFound, "parking.lot_not_found");
    }

    [Fact]
    public async Task Public_detail_keeps_its_fields_and_adds_prices_and_free_spaces()
    {
        await using var app = await CreateAppAsync();

        var body = await GetOkJsonAsync(app, $"/api/parking/lots/{ApprovedLot}", role: null, user: null);

        Assert.Equal(ApprovedLot, body.GetProperty("id").GetGuid());
        Assert.Equal("Bãi trung tâm", body.GetProperty("name").GetString());
        Assert.Equal("1 Lê Lợi", body.GetProperty("address").GetString());
        Assert.Equal("Outdoor", body.GetProperty("type").GetString());
        Assert.Equal(2, body.GetProperty("pricing").GetArrayLength());
        Assert.Equal(7, body.GetProperty("availability").GetProperty("car").GetProperty("available").GetInt32());
    }

    [Fact]
    public async Task Public_detail_of_a_pending_lot_is_still_404()
    {
        await using var app = await CreateAppAsync();

        using var response = await SendAsync(app, $"/api/parking/lots/{PendingLot}", role: null, user: null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Owner_reads_the_detail_of_their_pending_lot()
    {
        await using var app = await CreateAppAsync();

        var body = await GetOkJsonAsync(app, $"/api/parking/owner/lots/{PendingLot}", "ParkingLotOwner", Owner);

        Assert.Equal(PendingLot, body.GetProperty("id").GetGuid());
        Assert.Equal("Pending", body.GetProperty("approvalStatus").GetString());
        Assert.Equal(1, body.GetProperty("pricing").GetArrayLength());
    }

    [Fact]
    public async Task Owner_detail_is_401_without_token_403_for_other_roles_and_other_owners_404_for_unknown_lot()
    {
        await using var app = await CreateAppAsync();

        using var anonymous = await SendAsync(app, $"/api/parking/owner/lots/{PendingLot}", role: null, user: null);
        using var customer = await SendAsync(app, $"/api/parking/owner/lots/{PendingLot}", "Customer", Owner);
        using var other = await SendAsync(app, $"/api/parking/owner/lots/{PendingLot}", "ParkingLotOwner", OtherOwner);
        using var unknown = await SendAsync(app, $"/api/parking/owner/lots/{Guid.NewGuid()}", "ParkingLotOwner", Owner);

        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, customer.StatusCode);
        await AssertProblemAsync(other, HttpStatusCode.Forbidden, "parking.lot.access_denied");
        await AssertProblemAsync(unknown, HttpStatusCode.NotFound, "parking.lot_not_found");
    }

    private static async Task<JsonElement> GetOkJsonAsync(WebApplication app, string route, string? role, Guid? user)
    {
        using var response = await SendAsync(app, route, role, user);
        var text = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.True(response.StatusCode == HttpStatusCode.OK, $"{route}: {(int)response.StatusCode} {text}");
        return JsonDocument.Parse(text).RootElement.Clone();
    }

    private static async Task AssertProblemAsync(HttpResponseMessage response, HttpStatusCode expected, string errorCode)
    {
        Assert.Equal(expected, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal(errorCode, problem.GetProperty("errorCode").GetString());
    }

    private static async Task<HttpResponseMessage> SendAsync(WebApplication app, string route, string? role, Guid? user)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, route);
        if (role is not null)
            request.Headers.Add(RoleHeader, role);
        if (user is not null)
            request.Headers.Add(UserHeader, user.Value.ToString());
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
        builder.Services.AddAuthentication(HeaderAuthenticationHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, HeaderAuthenticationHandler>(HeaderAuthenticationHandler.SchemeName, null);
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddScoped<ICurrentUser, ClaimsCurrentUser>();
        builder.Services.AddSingleton<IParkingLotDetailReader, InMemoryLots>();
        builder.Services.AddSingleton<IAdminParkingLotDetailReader, InMemoryLots>();
        builder.Services.AddSingleton<ILotOwnershipService, InMemoryOwnership>();
        builder.Services.AddSingleton<IPricingPolicyRepository, InMemoryPricing>();
        builder.Services.AddSingleton<IAvailableSlotReader, InMemoryCarSlots>();
        builder.Services.AddSingleton<IManageParkingLayoutRepository, InMemoryLayouts>();
        builder.Services.AddSingleton<IMotorbikeZoneCounter, InMemoryMotorbikeZones>();
        builder.Services.AddSingleton<IClock, ParkingSystem.BuildingBlocks.Time.SystemClock>();

        var app = builder.Build();
        app.UseParkingSystemWebFoundation();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapParkingLotEndpoints();
        app.MapPublicParkingLayoutEndpoints();
        app.MapPricingEndpoints();
        await app.StartAsync(TestContext.Current.CancellationToken);
        return app;
    }

    // No role header means no token; the user header sets the account id.
    private sealed class HeaderAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public const string SchemeName = "TestHeader";

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.TryGetValue(RoleHeader, out var role))
                return Task.FromResult(AuthenticateResult.NoResult());

            var user = Request.Headers.TryGetValue(UserHeader, out var id) ? id.ToString() : Guid.NewGuid().ToString();
            var identity = new ClaimsIdentity(
                [new Claim("sub", user), new Claim(ParkingAuthorization.RoleClaimType, role.ToString())],
                SchemeName);
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName)));
        }
    }

    private sealed class ClaimsCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
    {
        public string? SubjectId => accessor.HttpContext?.User.FindFirst("sub")?.Value;

        public bool IsAuthenticated => accessor.HttpContext?.User.Identity?.IsAuthenticated ?? false;
    }

    private sealed class InMemoryLots : IParkingLotDetailReader, IAdminParkingLotDetailReader
    {
        public Task<ParkingLotDetail?> GetApprovedByIdAsync(Guid parkingLotId, CancellationToken cancellationToken) =>
            Task.FromResult(parkingLotId == ApprovedLot
                ? new ParkingLotDetail(ApprovedLot, "Bãi trung tâm", "1 Lê Lợi", 10.77m, 106.70m, "Outdoor")
                : null);

        public Task<AdminParkingLotDetail?> GetByIdAsync(Guid parkingLotId, CancellationToken cancellationToken) =>
            Task.FromResult(
                parkingLotId == PendingLot
                    ? new AdminParkingLotDetail(PendingLot, Owner, "Bãi chờ duyệt", "2 Lê Lợi", 10.78m, 106.71m, "Outdoor", "Pending", null, null)
                    : parkingLotId == RejectedLot
                        ? new AdminParkingLotDetail(RejectedLot, Owner, "Bãi bị từ chối", "3 Lê Lợi", 10.79m, 106.72m, "Outdoor", "Rejected", null, null)
                    : parkingLotId == ApprovedLot
                        ? new AdminParkingLotDetail(ApprovedLot, Owner, "Bãi trung tâm", "1 Lê Lợi", 10.77m, 106.70m, "Outdoor", "Approved", null, null)
                        : null);
    }

    private sealed class InMemoryOwnership : ILotOwnershipService
    {
        public Task<bool> LotExistsAsync(Guid lotId) => Task.FromResult(lotId == ApprovedLot || lotId == PendingLot);

        public Task<bool> IsLotOwnerAsync(Guid lotId, Guid callerId) =>
            Task.FromResult((lotId == ApprovedLot || lotId == PendingLot) && callerId == Owner);

        public Task<bool> IsLotApprovedAsync(Guid lotId) => Task.FromResult(lotId == ApprovedLot);
    }

    private sealed class InMemoryPricing : IPricingPolicyRepository
    {
        private static readonly DateTime Effective = new(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);

        private static readonly PricingPolicy[] Policies =
        [
            PricingPolicy.Create(Guid.NewGuid(), ApprovedLot, "CAR", 1, "Block", 60, 30000m, 0m, Effective),
            PricingPolicy.Create(Guid.NewGuid(), ApprovedLot, "MOTORBIKE", 1, "Block", 60, 5000m, 0m, Effective),
            PricingPolicy.Create(Guid.NewGuid(), PendingLot, "CAR", 1, "Block", 60, 20000m, 0m, Effective),
        ];

        public Task<PricingPolicy?> GetLatestAsync(Guid lotId, string vehicleType, CancellationToken cancellationToken) =>
            Task.FromResult(Policies.FirstOrDefault(policy => policy.LotId == lotId && policy.VehicleType == vehicleType));

        public Task AddAsync(PricingPolicy policy, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<bool> SaveChangesAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class InMemoryLayouts : IManageParkingLayoutRepository
    {
        public Task<bool> LotExistsAsync(Guid lotId, CancellationToken ct) =>
            Task.FromResult(lotId == ApprovedLot || lotId == PendingLot);

        public Task<bool> IsOwnerAsync(Guid lotId, Guid actorId, CancellationToken ct) =>
            Task.FromResult(false);

        public Task<ParkingLotLayout> ReadAsync(Guid lotId, CancellationToken ct) =>
            Task.FromResult(new ParkingLotLayout(lotId, 3,
            [
                new LayoutFloor(Guid.NewGuid(), "Ground",
                [
                    new LayoutZone(Guid.NewGuid(), "CAR", "Car", 6, 3, true,
                    [
                        new LayoutSlot(Guid.NewGuid(), "A-01", null, "Occupied", new GridPoint(1, 1)),
                        new LayoutSlot(Guid.NewGuid(), "A-02", null, "Available", new GridPoint(2, 1)),
                        new LayoutSlot(Guid.NewGuid(), "A-03", "Maintenance", "Maintenance", new GridPoint(3, 1)),
                        new LayoutSlot(Guid.NewGuid(), "A-04", "Unavailable", "Unavailable", new GridPoint(4, 1)),
                        new LayoutSlot(Guid.NewGuid(), "A-05", null, "Assigned", new GridPoint(5, 1)),
                        new LayoutSlot(Guid.NewGuid(), "A-06", null, "NeedsVerification", new GridPoint(6, 1))
                    ], null, null),
                    new LayoutZone(Guid.NewGuid(), "BIKES", "Motorbike", 20, 5, false, [],
                        new GridBounds(0, 0, 5, 5), null)
                ]),
                new LayoutFloor(Guid.NewGuid(), "Upper",
                [
                    new LayoutZone(Guid.NewGuid(), "OVERFLOW", "Motorbike", 15, 2, false, [], null,
                    [new GridPoint(0, 0), new GridPoint(4, 0), new GridPoint(0, 5)])
                ])
            ]));

        public Task<Error?> SaveAsync(Guid lotId, Guid actorId, int expectedVersion,
            IReadOnlyList<LayoutFloorInput> floors, CancellationToken ct) => throw new NotSupportedException();
    }

    private sealed class InMemoryCarSlots : IAvailableSlotReader
    {
        public Task<int> CountAvailableCarSlotsAsync(Guid lotId, CancellationToken cancellationToken) => Task.FromResult(7);
    }

    private sealed class InMemoryMotorbikeZones : IMotorbikeZoneCounter
    {
        public Task<MotorbikeSpaceTaken?> TakeSpaceAsync(Guid lotId, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task ReleaseSpaceAsync(Guid zoneId, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<MotorbikeAvailability?> GetAvailabilityAsync(Guid lotId, CancellationToken cancellationToken) =>
            Task.FromResult<MotorbikeAvailability?>(new MotorbikeAvailability(
                [new MotorbikeZoneAvailability(Guid.Parse("c0000000-0000-0000-0000-000000000001"), "M1", "Tầng 1", 40, 10)]));
    }
}
