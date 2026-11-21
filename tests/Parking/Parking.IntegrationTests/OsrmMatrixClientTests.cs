using System.Net;
using System.Text;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using ParkingSystem.Parking.Application.ParkingLots.SearchCustomerLots;
using ParkingSystem.Parking.Application.ParkingLots.Recommendations;
using ParkingSystem.Parking.WebAPI.Routing;
using Xunit;

namespace ParkingSystem.Tests.Parking.IntegrationTests;

/// <summary>US-SEARCH-006: mocked OSRM responses, no external requests or database.</summary>
public sealed class OsrmMatrixClientTests
{
    private static readonly Guid FirstId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid SecondId = Guid.Parse("00000000-0000-0000-0000-000000000002");

    private static CustomerLotSearchItem Lot(Guid id, decimal latitude, decimal longitude) =>
        new(id, "Demo", "Demo Street", latitude, longitude, "Outdoor", null,
            20000m, "PerEntry", 5, true, [], []);

    private static OsrmMatrixClient Client(FakeHandler handler) =>
        new(new HttpClient(handler), new MemoryCache(new MemoryCacheOptions()),
            new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
                { ["Osrm:BaseUrl"] = "http://osrm.test" }).Build());

    [Fact]
    public async Task Matrix_maps_duration_to_lot_id_and_caches_for_reordered_candidates()
    {
        var handler = new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"code":"Ok","durations":[[600,1200]]}""", Encoding.UTF8, "application/json")
        });
        var client = Client(handler);
        var lots = new[] { Lot(SecondId, 10.77m, 106.71m), Lot(FirstId, 10.76m, 106.70m) };
        var first = await client.GetMinutesAsync(10.75m, 106.69m, lots, "Car", TestContext.Current.CancellationToken);
        var second = await client.GetMinutesAsync(10.75m, 106.69m, lots.Reverse().ToArray(),
            "Car", TestContext.Current.CancellationToken);

        Assert.Equal(10d, first[FirstId]);
        Assert.Equal(20d, first[SecondId]);
        Assert.Equal(10d, second[FirstId]);
        Assert.Equal(1, handler.Calls);
        Assert.Contains("sources=0", handler.LastRequestUri);
        Assert.Contains("destinations=1;2", handler.LastRequestUri);
    }

    [Fact]
    public async Task Search_And_Recommendation_Reuse_The_Same_Cached_Osrm_Table_Response()
    {
        var handler = new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"code":"Ok","durations":[[660,1020]]}""",
                Encoding.UTF8, "application/json")
        });
        var routing = Client(handler);
        var search = await routing.GetMinutesAsync(10.75m, 106.69m,
            [Lot(FirstId, 10.76m, 106.70m), Lot(SecondId, 10.77m, 106.71m)],
            "Car", TestContext.Current.CancellationToken);
        var recommendation = await routing.GetDrivingMinutesAsync(10.75m, 106.69m,
            [new RecommendationRouteDestination(SecondId, 10.77m, 106.71m),
             new RecommendationRouteDestination(FirstId, 10.76m, 106.70m)],
            "Car", TestContext.Current.CancellationToken);
        Assert.Equal(11d, search[FirstId]);
        Assert.Equal(17d, recommendation[SecondId]);
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task No_route_is_null_and_never_fake_eta()
    {
        var handler = new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"code":"Ok","durations":[[null]]}""", Encoding.UTF8, "application/json")
        });
        var result = await Client(handler).GetMinutesAsync(10.75m, 106.69m,
            [Lot(FirstId, 10.76m, 106.70m)], "Car", TestContext.Current.CancellationToken);
        Assert.True(result.ContainsKey(FirstId));
        Assert.Null(result[FirstId]);
    }

    [Theory]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    public async Task Provider_failure_does_not_crash_search(HttpStatusCode status)
    {
        var handler = new FakeHandler(_ => new HttpResponseMessage(status));
        var result = await Client(handler).GetMinutesAsync(10.75m, 106.69m,
            [Lot(FirstId, 10.76m, 106.70m)], "Car", TestContext.Current.CancellationToken);
        Assert.Empty(result);
    }

    [Fact]
    public async Task Rejects_more_than_ten_destinations_before_http_call()
    {
        var handler = new FakeHandler(_ => throw new InvalidOperationException("Unexpected HTTP call"));
        var lots = Enumerable.Range(0, 11).Select(_ => Lot(Guid.NewGuid(), 10.77m, 106.7m)).ToArray();
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => Client(handler).GetMinutesAsync(
            10.75m, 106.69m, lots, "Car", TestContext.Current.CancellationToken));
        Assert.Equal(0, handler.Calls);
    }

    [Fact]
    public async Task Nearby_but_distinct_GPS_origins_must_not_share_cached_drive_time()
    {
        var handler = new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"code":"Ok","durations":[[600]]}""", Encoding.UTF8, "application/json")
        });
        var cache = new MemoryCache(new MemoryCacheOptions());
        // The same provider result is sufficient: a wrong cache reuse is observable as one call.
        var routing = new OsrmMatrixClient(new HttpClient(handler), cache,
            new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
                { ["Osrm:BaseUrl"] = "http://osrm.test" }).Build());
        var lots = new[] { Lot(FirstId, 10.76m, 106.70m) };
        await routing.GetMinutesAsync(10.75011m, 106.69011m, lots, "Car", TestContext.Current.CancellationToken);
        await routing.GetMinutesAsync(10.75019m, 106.69019m, lots, "Car", TestContext.Current.CancellationToken);
        Assert.Equal(2, handler.Calls);
    }

    [Fact]
    public async Task Identical_six_decimal_coordinates_still_reuse_cache()
    {
        var handler = new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"code":"Ok","durations":[[420]]}""", Encoding.UTF8, "application/json")
        });
        var routing = Client(handler);
        var lots = new[] { Lot(FirstId, 10.76m, 106.70m) };
        await routing.GetMinutesAsync(10.7501111m, 106.6901111m, lots, "Car", TestContext.Current.CancellationToken);
        await routing.GetMinutesAsync(10.7501112m, 106.6901112m, lots, "Car", TestContext.Current.CancellationToken);
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public void Cache_key_is_stable_per_route_and_does_not_contain_raw_GPS()
    {
        var route = new RecommendationRouteDestination(FirstId, 10.761234m, 106.701234m);
        var key = OsrmMatrixClient.BuildCacheKey(10.751234m, 106.691234m, [route], "Car");
        Assert.StartsWith("osrm-table-v2:", key);
        Assert.DoesNotContain("10.751234", key);
        Assert.DoesNotContain("106.691234", key);
        Assert.DoesNotContain("10.761234", key);
        Assert.Equal(key, OsrmMatrixClient.BuildCacheKey(10.751234m, 106.691234m, [route], "Car"));
        Assert.NotEqual(key, OsrmMatrixClient.BuildCacheKey(10.751235m, 106.691234m, [route], "Car"));
    }

    private sealed class FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> response) : HttpMessageHandler
    {
        public int Calls { get; private set; }
        public string LastRequestUri { get; private set; } = "";
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            LastRequestUri = request.RequestUri?.ToString() ?? "";
            return Task.FromResult(response(request));
        }
    }
}
