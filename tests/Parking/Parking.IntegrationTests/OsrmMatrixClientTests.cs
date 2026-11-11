using System.Net;
using System.Text;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using ParkingSystem.Parking.Application.ParkingLots.SearchCustomerLots;
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
