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
    private static readonly Guid Owner = Guid.Parse("b0000000-0000-0000-0000-000000000001");
    private static readonly Guid OtherOwner = Guid.Parse("b0000000-0000-0000-0000-000000000002");

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
        builder.Services.AddSingleton<IMotorbikeZoneCounter, InMemoryMotorbikeZones>();
        builder.Services.AddSingleton<IClock, ParkingSystem.BuildingBlocks.Time.SystemClock>();

        var app = builder.Build();
        app.UseParkingSystemWebFoundation();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapParkingLotEndpoints();
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
        public Task<PricingPolicy?> GetEffectiveAtAsync(Guid lotId, string vehicleType, DateTimeOffset at, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

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
