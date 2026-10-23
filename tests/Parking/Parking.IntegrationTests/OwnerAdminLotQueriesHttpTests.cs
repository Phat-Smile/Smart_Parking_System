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
using ParkingSystem.Parking.Application.ParkingLots.RegisterParkingLot;
using ParkingSystem.Parking.Domain.Entities;
using ParkingSystem.Parking.Persistence;
using ParkingSystem.Parking.WebAPI.Endpoints;
using ParkingSystem.Parking.WebAPI.Security;
using ParkingSystem.Testing;
using Xunit;

namespace ParkingSystem.Tests.Parking.IntegrationTests;

// Review of PR 118 over HTTP (R1 to R3): the real routes, policies, handlers and PostgreSQL.
// Only the sign-in is replaced: two headers stand in for the "sub" and "role" claims of the Identity token.
public sealed class OwnerAdminLotQueriesHttpTests(PostgreSqlContainerFixture postgres)
    : IClassFixture<PostgreSqlContainerFixture>
{
    private const string AccountHeader = "X-Test-Account";
    private const string RoleHeader = "X-Test-Role";
    private const string OwnerRole = "ParkingLotOwner";
    private const string AdminRole = "SystemAdmin";

    // R1: "owner of a lot" is an Owner row in parking_lot_assignments (MOD-PK-05); owner_user_id only records the registrant.
    [Fact]
    public async Task The_owner_list_follows_the_Owner_assignment_rows_not_the_registrant_column()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var app = await CreateAppAsync(ct);
        var client = app.GetTestClient();
        var (ownerA, ownerB, admin) = (Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        var shared = await RegisterLotAsync(client, ownerA, NewName("Shared"), ct);
        var rejected = await RegisterLotAsync(client, ownerA, NewName("Rejected"), ct);
        var ownedByB = await RegisterLotAsync(client, ownerB, NewName("Owned by B"), ct);
        await ReviewAsync(client, admin, rejected, approve: false, "thieu giay to", ct);

        // The registrant sees every lot of hers, Pending and Rejected included, with the reason of the rejection.
        var firstList = await ListOwnerLotsAsync(client, ownerA, ct);
        Assert.Contains(shared, Ids(firstList));
        var rejectedItem = firstList.EnumerateArray().Single(item => item.GetProperty("id").GetGuid() == rejected);
        Assert.Equal("Rejected", rejectedItem.GetProperty("approvalStatus").GetString());
        Assert.Equal("thieu giay to", rejectedItem.GetProperty("rejectionReason").GetString());

        // An admin makes B a co-owner of the shared lot: B can manage it, so B must see it in the owner list.
        await AssignAsync(client, admin, shared, ownerB, "Owner", ct);
        var coOwnerList = await ListOwnerLotsAsync(client, ownerB, ct);
        Assert.Contains(ownedByB, Ids(coOwnerList));
        Assert.True(Ids(coOwnerList).Contains(shared), "A co-owner (Owner row on the lot) does not see the lot in GET /api/parking/owner/lots.");
        Assert.DoesNotContain(rejected, Ids(coOwnerList));

        // The admin removes A's Owner row: A is no longer an owner of the shared lot, so it must leave her list.
        await UnassignAsync(client, admin, shared, ownerA, ct);
        var exOwnerList = await ListOwnerLotsAsync(client, ownerA, ct);
        Assert.Contains(rejected, Ids(exOwnerList));
        Assert.False(Ids(exOwnerList).Contains(shared), "A removed owner still sees the lot (and its review result) in GET /api/parking/owner/lots.");
    }

    // R2: a lot that is not approved is not visible to customers (MF-15); only its owners may read its price.
    [Fact]
    public async Task The_price_of_a_lot_that_is_not_approved_is_visible_only_to_its_owners()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var app = await CreateAppAsync(ct);
        var client = app.GetTestClient();
        var (owner, stranger, admin) = (Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        var pending = await RegisterLotAsync(client, owner, NewName("Pending price"), ct);
        var rejected = await RegisterLotAsync(client, owner, NewName("Rejected price"), ct);
        var approved = await RegisterLotAsync(client, owner, NewName("Approved price"), ct);
        foreach (var lot in new[] { pending, rejected, approved })
            await SetPriceAsync(app, lot, ct);
        await ReviewAsync(client, admin, rejected, approve: false, "khong hop le", ct);
        await ReviewAsync(client, admin, approved, approve: true, null, ct);

        // Approved: public, and the vehicle type is not case sensitive.
        using var publicPrice = await SendAsync(client, HttpMethod.Get, $"/api/parking/lots/{approved}/pricing/car", null, null, null, ct);
        await AssertStatusAsync(HttpStatusCode.OK, publicPrice, ct);
        var price = (await publicPrice.Content.ReadFromJsonAsync<JsonElement>(ct)).GetProperty("result");
        Assert.Equal(15000m, price.GetProperty("price").GetDecimal());
        Assert.Equal(1, price.GetProperty("version").GetInt32());

        foreach (var lot in new[] { pending, rejected })
        {
            var route = $"/api/parking/lots/{lot}/pricing/CAR";
            using var anonymous = await SendAsync(client, HttpMethod.Get, route, null, null, null, ct);
            using var someoneElse = await SendAsync(client, HttpMethod.Get, route, OwnerRole, stranger, null, ct);
            using var theOwner = await SendAsync(client, HttpMethod.Get, route, OwnerRole, owner, null, ct);

            await AssertStatusAsync(HttpStatusCode.NotFound, anonymous, ct);
            await AssertStatusAsync(HttpStatusCode.NotFound, someoneElse, ct);
            await AssertStatusAsync(HttpStatusCode.OK, theOwner, ct);
        }
    }

    // R3: only a status name selects a status. Numbers, unknown values and lists must be a 400, never a quiet wrong filter.
    [Fact]
    public async Task The_admin_list_rejects_a_status_that_is_not_a_status_name()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var app = await CreateAppAsync(ct);
        var client = app.GetTestClient();
        var admin = Guid.NewGuid();

        var wrong = new List<string>();
        foreach (var status in new[] { "99", "1", "-1", "Pending,Rejected", "Approved,", "Pending;Rejected", "Nope" })
        {
            using var response = await SendAsync(client, HttpMethod.Get, $"/api/parking/admin/lots?status={Uri.EscapeDataString(status)}", AdminRole, admin, null, ct);
            if (response.StatusCode != HttpStatusCode.BadRequest)
                wrong.Add($"status={status} -> {(int)response.StatusCode}");
        }

        Assert.True(wrong.Count == 0, "These status values must be rejected with 400 but were accepted: " + string.Join("; ", wrong));
    }

    [Fact]
    public async Task The_admin_list_filters_by_status_name_without_case_sensitivity()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var app = await CreateAppAsync(ct);
        var client = app.GetTestClient();
        var (owner, admin) = (Guid.NewGuid(), Guid.NewGuid());

        var pending = await RegisterLotAsync(client, owner, NewName("Admin pending"), ct);
        var rejected = await RegisterLotAsync(client, owner, NewName("Admin rejected"), ct);
        var approved = await RegisterLotAsync(client, owner, NewName("Admin approved"), ct);
        await ReviewAsync(client, admin, rejected, approve: false, "khong hop le", ct);
        await ReviewAsync(client, admin, approved, approve: true, null, ct);
        var mine = new[] { pending, rejected, approved };

        async Task<Guid[]> ListAsync(string? status)
        {
            var route = status is null ? "/api/parking/admin/lots" : $"/api/parking/admin/lots?status={status}";
            using var response = await SendAsync(client, HttpMethod.Get, route, AdminRole, admin, null, ct);
            await AssertStatusAsync(HttpStatusCode.OK, response, ct);
            return Ids(await response.Content.ReadFromJsonAsync<JsonElement>(ct)).Where(mine.Contains).ToArray();
        }

        Assert.Equal([pending], await ListAsync("Pending"));
        Assert.Equal([pending], await ListAsync("pending"));
        Assert.Equal([rejected], await ListAsync("REJECTED"));
        Assert.Equal([approved], await ListAsync("Approved"));
        Assert.Equal(mine.OrderBy(id => id), (await ListAsync(null)).OrderBy(id => id));
    }

    private static string NewName(string prefix) => $"{prefix} {Guid.NewGuid():N}";

    private static IReadOnlyCollection<Guid> Ids(JsonElement list) =>
        list.EnumerateArray().Select(item => item.GetProperty("id").GetGuid()).ToArray();

    private static async Task<JsonElement> ListOwnerLotsAsync(HttpClient client, Guid owner, CancellationToken ct)
    {
        using var response = await SendAsync(client, HttpMethod.Get, "/api/parking/owner/lots", OwnerRole, owner, null, ct);
        await AssertStatusAsync(HttpStatusCode.OK, response, ct);
        return await response.Content.ReadFromJsonAsync<JsonElement>(ct);
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

    // Written straight to the table: the test is about who may read a price, not about the audited PUT that sets it.
    private static async Task SetPriceAsync(WebApplication app, Guid lotId, CancellationToken ct)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ParkingDbContext>();
        db.PricingPolicies.Add(PricingPolicy.Create(lotId, "CAR", 1, "Block", 30, 15000m, 50000m));
        await db.SaveChangesAsync(ct);
    }

    private static async Task ReviewAsync(HttpClient client, Guid admin, Guid lotId, bool approve, string? reason, CancellationToken ct)
    {
        using var response = await SendAsync(
            client, HttpMethod.Post, $"/api/parking/admin/lots/{lotId}/{(approve ? "approve" : "reject")}", AdminRole, admin,
            approve ? null : new { reason }, ct);
        await AssertStatusAsync(HttpStatusCode.OK, response, ct);
    }

    private static async Task AssignAsync(HttpClient client, Guid admin, Guid lotId, Guid account, string role, CancellationToken ct)
    {
        using var response = await SendAsync(client, HttpMethod.Put, $"/api/parking/admin/lots/{lotId}/assignments/{account}", AdminRole, admin, new { role }, ct);
        await AssertStatusAsync(HttpStatusCode.OK, response, ct);
    }

    private static async Task UnassignAsync(HttpClient client, Guid admin, Guid lotId, Guid account, CancellationToken ct)
    {
        using var response = await SendAsync(client, HttpMethod.Delete, $"/api/parking/admin/lots/{lotId}/assignments/{account}", AdminRole, admin, null, ct);
        Assert.True(response.StatusCode is HttpStatusCode.OK or HttpStatusCode.NoContent, $"Removing the assignment returned {(int)response.StatusCode}.");
    }

    // role and account null means no sign-in at all (an anonymous caller).
    private static async Task<HttpResponseMessage> SendAsync(
        HttpClient client, HttpMethod method, string route, string? role, Guid? account, object? body, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(method, route);
        if (body is not null)
            request.Content = JsonContent.Create(body);
        if (role is not null && account is not null)
        {
            request.Headers.Add(RoleHeader, role);
            request.Headers.Add(AccountHeader, account.Value.ToString());
        }

        return await client.SendAsync(request, ct);
    }

    // Shows the response body when the status is not the expected one, so a failure explains itself.
    private static async Task AssertStatusAsync(HttpStatusCode expected, HttpResponseMessage response, CancellationToken ct)
    {
        if (response.StatusCode == expected)
            return;
        var body = await response.Content.ReadAsStringAsync(ct);
        Assert.Fail($"{response.RequestMessage?.Method} {response.RequestMessage?.RequestUri?.PathAndQuery}: expected {(int)expected} but got {(int)response.StatusCode}: {body}");
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
        app.MapParkingLotEndpoints();
        app.MapParkingLotAssignmentEndpoints();
        app.MapPricingEndpoints();
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
