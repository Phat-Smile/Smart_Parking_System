using System.Net;
using System.Text;
using ParkingSystem.Parking.Application.Common.Mapbox;
using ParkingSystem.Parking.Infrastructure.Mapbox;
using Xunit;

namespace ParkingSystem.Tests.Parking.UnitTests;

public sealed class MapboxGeoRoutingClientTests
{
    [Fact]
    public async Task Geocode_parses_lon_lat_without_swapping_and_disables_autocomplete()
    {
        Uri? seenUrl = null;
        var client = Build(req =>
        {
            seenUrl = req.RequestUri;
            return Json("""
                {"features":[{"geometry":{"coordinates":[106.700981,10.776530]},
                  "properties":{"full_address":"District 1, Ho Chi Minh City"}}]}
                """);
        });
        var result = await client.GeocodeAsync("District 1, Vietnam", TestContext.Current.CancellationToken);
        Assert.NotNull(result);
        Assert.Equal(10.776530, result.Latitude);
        Assert.Equal(106.700981, result.Longitude);
        Assert.Contains("country=vn", seenUrl!.Query);
        Assert.Contains("autocomplete=false", seenUrl.Query);
    }

    [Fact]
    public async Task Geocode_no_match_returns_null()
    {
        var client = Build(_ => Json("""{"features":[]}"""));
        Assert.Null(await client.GeocodeAsync("No place found", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Directions_preserves_units_and_coordinate_order()
    {
        Uri? seenUrl = null;
        var client = Build(req =>
        {
            seenUrl = req.RequestUri;
            return Json("""{"code":"Ok","routes":[{"duration":120.0,"distance":1050.0}]}""");
        });
        var route = await client.DirectionsAsync(10.77653, 106.700981, 10.7821, 106.6954, TestContext.Current.CancellationToken);
        Assert.True(route.Reachable);
        Assert.Equal(120.0, route.DurationSeconds);
        Assert.Equal(1050.0, route.DistanceMeters);
        Assert.Contains("106.700981,10.776530;106.695400,10.782100", seenUrl!.AbsoluteUri);
    }

    [Fact]
    public async Task Directions_no_route_returns_unreachable()
    {
        var client = Build(_ => Json("""{"code":"NoRoute"}"""));
        var route = await client.DirectionsAsync(10.77, 106.70, 10.78, 106.71, TestContext.Current.CancellationToken);
        Assert.False(route.Reachable);
        Assert.Null(route.DurationSeconds);
    }

    [Fact]
    public async Task Matrix_preserves_destination_order_and_null_unreachable()
    {
        Uri? seenUrl = null;
        var client = Build(req =>
        {
            seenUrl = req.RequestUri;
            return Json("""{"code":"Ok","durations":[[55,null]],"distances":[[250,null]]}""");
        });
        var rows = await client.MatrixAsync(10.77, 106.70,
            [(10.78, 106.71), (10.79, 106.72)], TestContext.Current.CancellationToken);
        Assert.Equal(2, rows.Count);
        Assert.True(rows[0].Reachable);
        Assert.Equal(55.0, rows[0].DurationSeconds);
        Assert.False(rows[1].Reachable);
        Assert.Contains("sources=0", seenUrl!.Query);
        Assert.Contains("destinations=1;2", Uri.UnescapeDataString(seenUrl.Query));
    }

    [Fact]
    public async Task Provider_failure_hides_access_token()
    {
        var client = Build(_ => new HttpResponseMessage(HttpStatusCode.TooManyRequests), "SECRET_TOKEN_FOR_TEST");
        var error = await Assert.ThrowsAsync<MapboxProviderException>(
            () => client.GeocodeAsync("District 1", TestContext.Current.CancellationToken));
        Assert.Equal("rate_limited", error.Code);
        Assert.DoesNotContain("SECRET_TOKEN_FOR_TEST", error.ToString());
    }

    [Fact]
    public async Task Missing_token_fails_only_when_provider_is_called()
    {
        var client = Build(_ => Json("""{"features":[]}"""), "");
        var error = await Assert.ThrowsAsync<MapboxProviderException>(
            () => client.GeocodeAsync("District 1", TestContext.Current.CancellationToken));
        Assert.Equal("token_not_configured", error.Code);
    }

    [Fact]
    public async Task Rejects_invalid_coordinates_without_contacting_provider()
    {
        var called = false;
        var client = Build(_ =>
        {
            called = true;
            return Json("""{"code":"NoRoute"}""");
        });
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            client.DirectionsAsync(91, 106, 10, 106, TestContext.Current.CancellationToken));
        Assert.False(called);
    }

    private static HttpResponseMessage Json(string text) =>
        new(HttpStatusCode.OK) { Content = new StringContent(text, Encoding.UTF8, "application/json") };

    private static MapboxGeoRoutingClient Build(
        Func<HttpRequestMessage, HttpResponseMessage> handle,
        string token = "offline_test_token") =>
        new(new HttpClient(new StubHandler(handle)) { Timeout = TimeSpan.FromSeconds(5) }, token);

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> handle) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(handle(request));
    }
}
