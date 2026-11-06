using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using ParkingSystem.Parking.Application.Common.Mapbox;
using ParkingSystem.Parking.Application.ParkingLots;
using ParkingSystem.Parking.Application.ParkingLots.DrivingTime;
using ParkingSystem.Parking.WebAPI.Endpoints;
using Xunit;

namespace ParkingSystem.Tests.Parking.IntegrationTests;

// TestServer: no Docker, PostgreSQL, network or Mapbox token required.
public sealed class ParkingDrivingTimeHttpTests
{
    private static readonly Guid Approved = Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly Guid Pending = Guid.Parse("10000000-0000-0000-0000-000000000002");

    [Fact]
    public async Task Public_detail_returns_provider_eta_and_denies_pending_lot()
    {
        var provider = new StubRouting();
        await using var app = await StartAsync(provider);
        using var ok = await app.GetTestClient().GetAsync(
            $"/api/parking/lots/{Approved}/driving-time?latitude=10.77&longitude=106.70");
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        var result = await ok.Content.ReadFromJsonAsync<ParkingLotDrivingTime>();
        Assert.NotNull(result);
        Assert.Equal(125d, result.DurationSeconds);
        Assert.Equal(1100d, result.DistanceMeters);

        using var hidden = await app.GetTestClient().GetAsync(
            $"/api/parking/lots/{Pending}/driving-time?latitude=10.77&longitude=106.70");
        Assert.Equal(HttpStatusCode.NotFound, hidden.StatusCode);
        Assert.Equal(1, provider.Calls);
    }

    [Fact]
    public async Task Public_list_returns_reachable_and_unreachable_without_fake_etas()
    {
        var provider = new StubRouting { Unreachable = true };
        await using var app = await StartAsync(provider);
        using var response = await app.GetTestClient().GetAsync(
            "/api/parking/lots/driving-times?latitude=10.77&longitude=106.70");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var results = await response.Content.ReadFromJsonAsync<ParkingLotDrivingTime[]>();
        Assert.NotNull(results);
        Assert.Equal(2, results.Length);
        Assert.True(results[0].Reachable);
        Assert.False(results[1].Reachable);
        Assert.Null(results[1].DurationSeconds);
        Assert.Null(results[1].DistanceMeters);
    }

    [Theory]
    [InlineData("/api/parking/lots/driving-times?latitude=100&longitude=106.70")]
    [InlineData("/api/parking/lots/driving-times?latitude=10.77&longitude=106.70&limit=11")]
    [InlineData("/api/parking/lots/driving-times?latitude=10.77&longitude=106.70&radiusKm=51")]
    [InlineData("/api/parking/lots/driving-times?longitude=106.70")]
    public async Task Invalid_query_is_400_without_provider_call(string url)
    {
        var provider = new StubRouting();
        await using var app = await StartAsync(provider);
        using var response = await app.GetTestClient().GetAsync(url);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, provider.Calls);
    }

    [Fact]
    public async Task Provider_error_returns_503_without_exposing_token()
    {
        var provider = new StubRouting { Fails = true };
        await using var app = await StartAsync(provider);
        using var response = await app.GetTestClient().GetAsync(
            $"/api/parking/lots/{Approved}/driving-time?latitude=10.77&longitude=106.70");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("sensitive_access_token", body);
        Assert.DoesNotContain("https://api.mapbox.com", body);
    }

    private static async Task<WebApplication> StartAsync(StubRouting provider)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Production" });
        builder.WebHost.UseTestServer();
        builder.Services.AddSingleton<IParkingLotDetailReader, StubLots>();
        builder.Services.AddSingleton<INearbyParkingLotReader, StubNearby>();
        builder.Services.AddSingleton<IMapboxGeoRouting>(provider);
        builder.Services.AddScoped<ParkingDrivingTimeEstimator>();
        var app = builder.Build();
        app.MapParkingDrivingTimeEndpoints();
        await app.StartAsync();
        return app;
    }

    private sealed class StubLots : IParkingLotDetailReader
    {
        public Task<ParkingLotDetail?> GetApprovedByIdAsync(Guid lotId, CancellationToken ct) =>
            Task.FromResult<ParkingLotDetail?>(lotId == Approved
                ? new ParkingLotDetail(Approved, "Approved", "A", 10.78m, 106.71m, "Outdoor")
                : null);
    }

    private sealed class StubNearby : INearbyParkingLotReader
    {
        public Task<IReadOnlyList<NearbyParkingLotItem>> SearchAsync(
            decimal lat, decimal lon, double radiusKm, int limit, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<NearbyParkingLotItem>>([
                new NearbyParkingLotItem(Approved, "Near", "A", 10.78m, 106.71m, "Outdoor", 1.2),
                new NearbyParkingLotItem(Guid.Parse("10000000-0000-0000-0000-000000000003"),
                    "Further", "B", 10.79m, 106.72m, "Outdoor", 2.4)
            ]);
    }

    private sealed class StubRouting : IMapboxGeoRouting
    {
        public bool Fails { get; set; }
        public bool Unreachable { get; set; }
        public int Calls { get; private set; }
        public Task<MapboxGeocodedPlace?> GeocodeAsync(string address, CancellationToken ct = default) =>
            throw new NotSupportedException();
        public Task<MapboxRouteResult> DirectionsAsync(double a, double b, double c, double d, CancellationToken ct = default)
        {
            Calls++;
            if (Fails) throw new MapboxProviderException("sensitive_access_token");
            return Task.FromResult(new MapboxRouteResult(true, 125, 1100));
        }
        public Task<IReadOnlyList<MapboxRouteResult>> MatrixAsync(
            double a, double b, IReadOnlyList<(double Latitude, double Longitude)> destinations,
            CancellationToken ct = default)
        {
            Calls++;
            if (Fails) throw new MapboxProviderException("sensitive_access_token");
            return Task.FromResult<IReadOnlyList<MapboxRouteResult>>(
                Unreachable
                    ? [new MapboxRouteResult(true, 100, 800), new MapboxRouteResult(false, null, null)]
                    : [new MapboxRouteResult(true, 100, 800), new MapboxRouteResult(true, 200, 2000)]);
        }
    }
}
