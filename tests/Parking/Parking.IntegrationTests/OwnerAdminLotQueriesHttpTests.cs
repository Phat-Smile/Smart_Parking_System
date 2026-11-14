using System.Net;
using System.Net.Http.Json;
using MediatR;
using ParkingSystem.Parking.Application.ParkingLayout.ManageLayout;
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
using ParkingSystem.Parking.Application.ParkingLots.RegisterParkingLot;
using ParkingSystem.Parking.Domain.Entities;
using ParkingSystem.Parking.Persistence;
using ParkingSystem.Parking.WebAPI.Endpoints;
using ParkingSystem.Parking.WebAPI.OwnerLocation;
using ParkingSystem.Parking.WebAPI.Security;
using ParkingSystem.Testing;
using Xunit;

namespace ParkingSystem.Tests.Parking.IntegrationTests;

// Review of PR 118 over HTTP (R1 to R4): the real routes, policies, handlers and PostgreSQL.
// Only the sign-in is replaced: two headers stand in for the "sub" and "role" claims of the Identity token.
public sealed class OwnerAdminLotQueriesHttpTests(PostgreSqlContainerFixture postgres)
    : IClassFixture<PostgreSqlContainerFixture>
{
    private const string AccountHeader = "X-Test-Account";
    private const string RoleHeader = "X-Test-Role";
    private const string AmrHeader = "X-Test-Amr";
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
        var rejectedItem = Items(firstList).Single(item => item.GetProperty("id").GetGuid() == rejected);
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

        // The list is paged and shared with the other tests of this class: walk every page before looking for these lots.
        async Task<Guid[]> ListAsync(string? status) =>
            (await ListAllAdminLotsAsync(client, admin, status, ct))
                .Select(item => item.GetProperty("id").GetGuid()).Where(mine.Contains).ToArray();

        Assert.Equal([pending], await ListAsync("Pending"));
        Assert.Equal([pending], await ListAsync("pending"));
        Assert.Equal([rejected], await ListAsync("REJECTED"));
        Assert.Equal([approved], await ListAsync("Approved"));
        Assert.Equal(mine.OrderBy(id => id), (await ListAsync(null)).OrderBy(id => id));
    }

    // R4: both lists are pages in the shape of the catalog (API-PK-01, API-PK-03): { items, totalCount, page, pageSize }.
    [Fact]
    public async Task The_owner_list_is_a_page_in_the_catalog_shape_ordered_by_name()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var app = await CreateAppAsync(ct);
        var client = app.GetTestClient();
        var owner = Guid.NewGuid();
        var tag = Guid.NewGuid().ToString("N");

        var expected = new List<Guid>();
        for (var number = 1; number <= 25; number++)
            expected.Add(await RegisterLotAsync(client, owner, $"Paging {number:00} {tag}", ct));

        var first = await ListOwnerLotsAsync(client, owner, ct);
        var second = await ListOwnerLotsAsync(client, owner, ct, "?page=2");

        Assert.Equal(JsonValueKind.Object, first.ValueKind);
        Assert.Equal((25, 1, 20), (first.GetProperty("totalCount").GetInt32(), first.GetProperty("page").GetInt32(), first.GetProperty("pageSize").GetInt32()));
        Assert.Equal((25, 2, 20), (second.GetProperty("totalCount").GetInt32(), second.GetProperty("page").GetInt32(), second.GetProperty("pageSize").GetInt32()));
        Assert.Equal(20, Ids(first).Count);
        Assert.Equal(5, Ids(second).Count);
        // The pages follow each other without a gap or an overlap, ordered by name.
        Assert.Equal(expected, Ids(first).Concat(Ids(second)).ToArray());

        var item = Items(first).First();
        foreach (var field in new[] { "id", "name", "address", "latitude", "longitude", "type", "approvalStatus", "rejectionReason", "hasOpeningHours", "hasPricing" })
            Assert.True(item.TryGetProperty(field, out _), $"An owner list item has no '{field}'.");
    }

    [Fact]
    public async Task The_owner_list_normalizes_paging_values_and_rejects_values_that_are_not_numbers()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var app = await CreateAppAsync(ct);
        var client = app.GetTestClient();
        var owner = Guid.NewGuid();
        for (var number = 1; number <= 7; number++)
            await RegisterLotAsync(client, owner, NewName($"Clamp {number}"), ct);

        async Task<(int Page, int PageSize, int Count, int Total)> ReadAsync(string query)
        {
            var body = await ListOwnerLotsAsync(client, owner, ct, query);
            return (body.GetProperty("page").GetInt32(), body.GetProperty("pageSize").GetInt32(), Ids(body).Count, body.GetProperty("totalCount").GetInt32());
        }

        Assert.Equal((1, 20, 7, 7), await ReadAsync("?page=0&pageSize=0"));
        Assert.Equal((1, 20, 7, 7), await ReadAsync("?page=-3&pageSize=-1"));
        Assert.Equal((1, 100, 7, 7), await ReadAsync("?pageSize=1000"));
        Assert.Equal((1, 3, 3, 7), await ReadAsync("?pageSize=3"));
        Assert.Equal((3, 3, 1, 7), await ReadAsync("?page=3&pageSize=3"));
        // Past the last page: no items, the total is kept, and a huge page number must not overflow the offset.
        Assert.Equal((99, 3, 0, 7), await ReadAsync("?page=99&pageSize=3"));
        Assert.Equal((int.MaxValue, 100, 0, 7), await ReadAsync("?page=2147483647&pageSize=100"));

        foreach (var query in new[] { "?page=abc", "?pageSize=x", "?page=1.5", "?pageSize=99999999999" })
        {
            using var response = await SendAsync(client, HttpMethod.Get, "/api/parking/owner/lots" + query, OwnerRole, owner, null, ct);
            await AssertStatusAsync(HttpStatusCode.BadRequest, response, ct);
        }
    }

    [Fact]
    public async Task The_owner_list_filters_by_status_and_counts_only_the_lots_it_filtered()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var app = await CreateAppAsync(ct);
        var client = app.GetTestClient();
        var (owner, admin) = (Guid.NewGuid(), Guid.NewGuid());

        var pendingA = await RegisterLotAsync(client, owner, NewName("Filter pending A"), ct);
        var pendingB = await RegisterLotAsync(client, owner, NewName("Filter pending B"), ct);
        var approved = await RegisterLotAsync(client, owner, NewName("Filter approved"), ct);
        var rejected = await RegisterLotAsync(client, owner, NewName("Filter rejected"), ct);
        await ReviewAsync(client, admin, approved, approve: true, null, ct);
        await ReviewAsync(client, admin, rejected, approve: false, "khong hop le", ct);

        var all = await ListOwnerLotsAsync(client, owner, ct);
        Assert.Equal(4, all.GetProperty("totalCount").GetInt32());

        var pending = await ListOwnerLotsAsync(client, owner, ct, "?status=Pending");
        Assert.Equal(2, pending.GetProperty("totalCount").GetInt32());
        Assert.Equal(new[] { pendingA, pendingB }.OrderBy(id => id), Ids(pending).OrderBy(id => id));

        var approvedOnly = await ListOwnerLotsAsync(client, owner, ct, "?status=approved");
        Assert.Equal(1, approvedOnly.GetProperty("totalCount").GetInt32());
        Assert.Equal([approved], Ids(approvedOnly));

        var rejectedOnly = await ListOwnerLotsAsync(client, owner, ct, "?status=REJECTED");
        Assert.Equal([rejected], Ids(rejectedOnly));
        Assert.Equal("khong hop le", Items(rejectedOnly).Single().GetProperty("rejectionReason").GetString());

        // The total counts every lot that matches, not only the ones on this page.
        var firstPending = await ListOwnerLotsAsync(client, owner, ct, "?status=Pending&pageSize=1");
        Assert.Equal(2, firstPending.GetProperty("totalCount").GetInt32());
        Assert.Single(Ids(firstPending));

        foreach (var status in new[] { "99", "-1", "Pending,Rejected", "Nope" })
        {
            using var response = await SendAsync(client, HttpMethod.Get, $"/api/parking/owner/lots?status={Uri.EscapeDataString(status)}", OwnerRole, owner, null, ct);
            await AssertStatusAsync(HttpStatusCode.BadRequest, response, ct);
        }
    }

    [Fact]
    public async Task The_owner_list_says_whether_opening_hours_and_prices_are_set()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var app = await CreateAppAsync(ct);
        var client = app.GetTestClient();
        var owner = Guid.NewGuid();

        var bare = await RegisterLotAsync(client, owner, NewName("Bare"), ct);
        var priced = await RegisterLotAsync(client, owner, NewName("Priced"), ct);
        var complete = await RegisterLotAsync(client, owner, NewName("Complete"), ct);
        await SetPriceAsync(app, priced, ct);
        await SetPriceAsync(app, complete, ct);
        await SetOpeningHoursAsync(app, complete, ct);

        var flags = Items(await ListOwnerLotsAsync(client, owner, ct)).ToDictionary(
            item => item.GetProperty("id").GetGuid(),
            item => (item.GetProperty("hasOpeningHours").GetBoolean(), item.GetProperty("hasPricing").GetBoolean()));

        Assert.Equal((false, false), flags[bare]);
        Assert.Equal((false, true), flags[priced]);
        Assert.Equal((true, true), flags[complete]);
    }

    [Fact]
    public async Task The_admin_list_is_a_page_in_the_catalog_shape_and_walks_every_lot_once()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var app = await CreateAppAsync(ct);
        var client = app.GetTestClient();
        var (owner, admin) = (Guid.NewGuid(), Guid.NewGuid());

        var mine = new List<Guid>();
        for (var number = 1; number <= 5; number++)
            mine.Add(await RegisterLotAsync(client, owner, NewName($"Walk {number}"), ct));
        await ReviewAsync(client, admin, mine[0], approve: true, null, ct);
        await ReviewAsync(client, admin, mine[1], approve: false, "khong hop le", ct);

        var seen = new List<JsonElement>();
        var total = 0;
        for (var page = 1; page <= 500; page++)
        {
            var body = await ListAdminLotsAsync(client, admin, $"?page={page}&pageSize=3", ct);
            Assert.Equal((page, 3), (body.GetProperty("page").GetInt32(), body.GetProperty("pageSize").GetInt32()));
            total = body.GetProperty("totalCount").GetInt32();
            var items = Items(body).ToArray();
            Assert.True(items.Length <= 3, $"Page {page} has {items.Length} items for a page size of 3.");
            seen.AddRange(items);
            if (items.Length == 0 || seen.Count >= total)
                break;
        }

        var ids = seen.Select(item => item.GetProperty("id").GetGuid()).ToArray();
        Assert.Equal(total, ids.Length);
        Assert.Equal(ids.Length, ids.Distinct().Count());
        foreach (var lot in mine)
            Assert.Contains(lot, ids);

        // The review queue comes first, as before: no Pending lot after a lot that is not Pending.
        var statuses = seen.Select(item => item.GetProperty("approvalStatus").GetString()).ToList();
        var firstOther = statuses.FindIndex(status => status != "Pending");
        Assert.True(firstOther < 0 || statuses.Skip(firstOther).All(status => status != "Pending"), "A Pending lot follows a lot that is not Pending.");

        foreach (var field in new[] { "id", "ownerUserId", "name", "address", "latitude", "longitude", "type", "approvalStatus", "rejectionReason" })
            Assert.True(seen[0].TryGetProperty(field, out _), $"An admin list item has no '{field}'.");
    }

    [Fact]
    public async Task The_admin_list_normalizes_paging_values_and_rejects_values_that_are_not_numbers()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var app = await CreateAppAsync(ct);
        var client = app.GetTestClient();
        var admin = Guid.NewGuid();

        async Task<(int Page, int PageSize)> ReadAsync(string query)
        {
            var body = await ListAdminLotsAsync(client, admin, query, ct);
            Assert.True(Items(body).Count() <= body.GetProperty("pageSize").GetInt32(), $"{query}: more items than the page size.");
            return (body.GetProperty("page").GetInt32(), body.GetProperty("pageSize").GetInt32());
        }

        Assert.Equal((1, 20), await ReadAsync(""));
        Assert.Equal((1, 20), await ReadAsync("?page=0&pageSize=0"));
        Assert.Equal((1, 100), await ReadAsync("?page=-1&pageSize=1000"));
        Assert.Equal((int.MaxValue, 100), await ReadAsync("?page=2147483647&pageSize=100"));

        foreach (var query in new[] { "?page=abc", "?pageSize=x", "?pageSize=99999999999" })
        {
            using var response = await SendAsync(client, HttpMethod.Get, "/api/parking/admin/lots" + query, AdminRole, admin, null, ct);
            await AssertStatusAsync(HttpStatusCode.BadRequest, response, ct);
        }
    }

    [Fact]
    public async Task Admin_detail_reads_all_approval_states_but_public_detail_remains_approved_only()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var app = await CreateAppAsync(ct);
        var client = app.GetTestClient();
        var (owner, admin) = (Guid.NewGuid(), Guid.NewGuid());

        var pending = await RegisterLotAsync(client, owner, NewName("Detail Pending"), ct);
        var rejected = await RegisterLotAsync(client, owner, NewName("Detail Rejected"), ct);
        var approved = await RegisterLotAsync(client, owner, NewName("Detail Approved"), ct);
        await ReviewAsync(client, admin, rejected, approve: false, "need valid documents", ct);
        await ReviewAsync(client, admin, approved, approve: true, null, ct);

        foreach (var (lotId, expectedStatus) in new[]
                 { (pending, "Pending"), (rejected, "Rejected"), (approved, "Approved") })
        {
            using var response = await SendAsync(client, HttpMethod.Get,
                $"/api/parking/admin/lots/{lotId}", AdminRole, admin, null, ct);
            await AssertStatusAsync(HttpStatusCode.OK, response, ct);
            var detail = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
            Assert.Equal(lotId, detail.GetProperty("id").GetGuid());
            Assert.Equal(owner, detail.GetProperty("ownerUserId").GetGuid());
            Assert.Equal(expectedStatus, detail.GetProperty("approvalStatus").GetString());
            Assert.Equal("1 Demo Street", detail.GetProperty("address").GetString());
            Assert.Equal(expectedStatus == "Rejected" ? "need valid documents" : null,
                detail.GetProperty("rejectionReason").GetString());
        }

        foreach (var hidden in new[] { pending, rejected })
        {
            using var publicResponse = await SendAsync(client, HttpMethod.Get,
                $"/api/parking/lots/{hidden}", null, null, null, ct);
            await AssertStatusAsync(HttpStatusCode.NotFound, publicResponse, ct);
        }

        using var missing = await SendAsync(client, HttpMethod.Get,
            $"/api/parking/admin/lots/{Guid.NewGuid()}", AdminRole, admin, null, ct);
        await AssertStatusAsync(HttpStatusCode.NotFound, missing, ct);
    }

    [Fact]
    public async Task Admin_may_review_pending_layout_and_pricing_without_exposing_them_to_others()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var app = await CreateAppAsync(ct);
        var client = app.GetTestClient();
        var (owner, admin) = (Guid.NewGuid(), Guid.NewGuid());
        var pending = await RegisterLotAsync(client, owner, NewName("Review Layout Price"), ct);
        await SetPriceAsync(app, pending, ct);

        var floor = new LayoutFloorInput(Guid.NewGuid(), "Ground", [
            new LayoutZoneInput(Guid.NewGuid(), "A", "Car", 1, false, [
                new LayoutSlotInput(Guid.NewGuid(), "A01", null, new GridPoint(1, 1))
            ])
        ]);
        await using (var scope = app.Services.CreateAsyncScope())
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            var saved = await sender.Send(new SaveParkingLayoutCommand(pending, owner, 0, [floor]), ct);
            Assert.True(saved.IsSuccess, saved.Error?.Message);
        }

        var detailRoute = $"/api/parking/admin/lots/{pending}";
        var layoutRoute = detailRoute + "/layout";
        var priceRoute = detailRoute + "/pricing/car";
        foreach (var route in new[] { detailRoute, layoutRoute, priceRoute })
        {
            using var anonymous = await SendAsync(client, HttpMethod.Get, route, null, null, null, ct);
            using var wrongRole = await SendAsync(client, HttpMethod.Get, route, OwnerRole, owner, null, ct);
            await AssertStatusAsync(HttpStatusCode.Unauthorized, anonymous, ct);
            await AssertStatusAsync(HttpStatusCode.Forbidden, wrongRole, ct);
        }

        using var layoutResponse = await SendAsync(client, HttpMethod.Get, layoutRoute, AdminRole, admin, null, ct);
        await AssertStatusAsync(HttpStatusCode.OK, layoutResponse, ct);
        var layout = await layoutResponse.Content.ReadFromJsonAsync<JsonElement>(ct);
        Assert.Equal(pending, layout.GetProperty("lotId").GetGuid());
        Assert.Equal(1, layout.GetProperty("version").GetInt32());
        Assert.Equal("A01", layout.GetProperty("floors")[0].GetProperty("zones")[0]
            .GetProperty("slots")[0].GetProperty("slotCode").GetString());

        using var priceResponse = await SendAsync(client, HttpMethod.Get, priceRoute, AdminRole, admin, null, ct);
        await AssertStatusAsync(HttpStatusCode.OK, priceResponse, ct);
        var price = await priceResponse.Content.ReadFromJsonAsync<JsonElement>(ct);
        Assert.Equal(15000m, price.GetProperty("price").GetDecimal());
        Assert.Equal("CAR", price.GetProperty("vehicleType").GetString());

        using var invalidVehicle = await SendAsync(client, HttpMethod.Get,
            detailRoute + "/pricing/TRUCK", AdminRole, admin, null, ct);
        await AssertStatusAsync(HttpStatusCode.BadRequest, invalidVehicle, ct);
        using var noPolicy = await SendAsync(client, HttpMethod.Get,
            detailRoute + "/pricing/MOTORBIKE", AdminRole, admin, null, ct);
        await AssertStatusAsync(HttpStatusCode.NotFound, noPolicy, ct);
        using var noLotLayout = await SendAsync(client, HttpMethod.Get,
            $"/api/parking/admin/lots/{Guid.NewGuid()}/layout", AdminRole, admin, null, ct);
        await AssertStatusAsync(HttpStatusCode.NotFound, noLotLayout, ct);
        using var noLotPrice = await SendAsync(client, HttpMethod.Get,
            $"/api/parking/admin/lots/{Guid.NewGuid()}/pricing/CAR", AdminRole, admin, null, ct);
        await AssertStatusAsync(HttpStatusCode.NotFound, noLotPrice, ct);

        // The public endpoint must still hide this pending lot, even if pricing exists.
        using var publicPrice = await SendAsync(client, HttpMethod.Get,
            $"/api/parking/lots/{pending}/pricing/CAR", null, null, null, ct);
        await AssertStatusAsync(HttpStatusCode.NotFound, publicPrice, ct);
    }

    private static string NewName(string prefix) => $"{prefix} {Guid.NewGuid():N}";

    private static IEnumerable<JsonElement> Items(JsonElement page) => page.GetProperty("items").EnumerateArray();

    private static IReadOnlyCollection<Guid> Ids(JsonElement page) =>
        Items(page).Select(item => item.GetProperty("id").GetGuid()).ToArray();

    private static async Task<JsonElement> ListOwnerLotsAsync(HttpClient client, Guid owner, CancellationToken ct, string query = "")
    {
        using var response = await SendAsync(client, HttpMethod.Get, "/api/parking/owner/lots" + query, OwnerRole, owner, null, ct);
        await AssertStatusAsync(HttpStatusCode.OK, response, ct);
        return await response.Content.ReadFromJsonAsync<JsonElement>(ct);
    }

    [Fact]
    public async Task AdminPolicy_endpoint_rejects_admin_without_mfa_and_accepts_admin_with_mfa()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var app = await CreateAppAsync(ct);
        var client = app.GetTestClient();
        var admin = Guid.NewGuid();
        var owner = Guid.NewGuid();
        var lotId = await RegisterLotAsync(client, owner, NewName("MfaAdminTest"), ct);

        using var noMfaResponse = await SendAsync(
            client, HttpMethod.Get, $"/api/parking/admin/lots/{lotId}/assignments", AdminRole, admin, null, ct, amr: "none");
        Assert.Equal(HttpStatusCode.Forbidden, noMfaResponse.StatusCode);

        using var mfaResponse = await SendAsync(
            client, HttpMethod.Get, $"/api/parking/admin/lots/{lotId}/assignments", AdminRole, admin, null, ct, amr: "mfa");
        Assert.Equal(HttpStatusCode.OK, mfaResponse.StatusCode);
    }

    [Fact]
    public async Task SystemAdminPolicy_endpoint_rejects_admin_without_mfa_and_accepts_admin_with_mfa()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var app = await CreateAppAsync(ct);
        var client = app.GetTestClient();
        var admin = Guid.NewGuid();

        using var noMfaResponse = await SendAsync(
            client, HttpMethod.Get, "/api/parking/admin/lots?page=1&pageSize=10", AdminRole, admin, null, ct, amr: "none");
        Assert.Equal(HttpStatusCode.Forbidden, noMfaResponse.StatusCode);

        using var mfaResponse = await SendAsync(
            client, HttpMethod.Get, "/api/parking/admin/lots?page=1&pageSize=10", AdminRole, admin, null, ct, amr: "mfa");
        Assert.Equal(HttpStatusCode.OK, mfaResponse.StatusCode);
    }

    private static async Task<JsonElement> ListAdminLotsAsync(HttpClient client, Guid admin, string query, CancellationToken ct)
    {
        using var response = await SendAsync(client, HttpMethod.Get, "/api/parking/admin/lots" + query, AdminRole, admin, null, ct);
        await AssertStatusAsync(HttpStatusCode.OK, response, ct);
        return await response.Content.ReadFromJsonAsync<JsonElement>(ct);
    }

    // The admin list is paged and shared by every test of this class: walk all of it, a hundred lots at a time.
    private static async Task<IReadOnlyList<JsonElement>> ListAllAdminLotsAsync(HttpClient client, Guid admin, string? status, CancellationToken ct)
    {
        var all = new List<JsonElement>();
        for (var page = 1; page <= 500; page++)
        {
            var body = await ListAdminLotsAsync(client, admin, $"?page={page}&pageSize=100" + (status is null ? "" : $"&status={status}"), ct);
            var items = Items(body).ToArray();
            all.AddRange(items);
            if (items.Length == 0 || all.Count >= body.GetProperty("totalCount").GetInt32())
                break;
        }

        return all;
    }

    private static async Task SetOpeningHoursAsync(WebApplication app, Guid lotId, CancellationToken ct)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ParkingDbContext>();
        var lot = await db.ParkingLots.SingleAsync(candidate => candidate.Id == lotId, ct);
        lot.SetOpeningHours("""{"days":[{"day":"Monday","open":"06:00","close":"22:00"}]}""");
        await db.SaveChangesAsync(ct);
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
        HttpClient client, HttpMethod method, string route, string? role, Guid? account, object? body, CancellationToken ct, string? amr = null)
    {
        using var request = new HttpRequestMessage(method, route);
        if (body is not null)
            request.Content = JsonContent.Create(body);
        if (role is not null && account is not null)
        {
            request.Headers.Add(RoleHeader, role);
            request.Headers.Add(AccountHeader, account.Value.ToString());
        }
        if (amr is not null)
        {
            request.Headers.Add(AmrHeader, amr);
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

        // Registration endpoint's handler needs this service even when a test only exercises GET routes.
        // Isolate test tokens from real application Data Protection keys.
        builder.Services.AddSingleton(new OwnerLotLocationConfirmationService(
            new EphemeralDataProtectionProvider()));

        var app = builder.Build();
        app.UseParkingSystemWebFoundation();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapParkingLotEndpoints();
        app.MapAdminParkingLotReviewEndpoints();
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

            var claims = new List<Claim>
            {
                new("sub", account.ToString()),
                new(ParkingAuthorization.RoleClaimType, role.ToString())
            };

            if (Request.Headers.TryGetValue(AmrHeader, out var amr))
            {
                if (!string.IsNullOrWhiteSpace(amr) && amr.ToString() != "none")
                {
                    claims.Add(new Claim(ParkingAuthorization.AuthenticationMethodsClaimType, amr.ToString()));
                }
            }
            else if (role.ToString() == AdminRole)
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
