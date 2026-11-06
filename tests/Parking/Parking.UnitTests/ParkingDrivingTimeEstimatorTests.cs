using ParkingSystem.Parking.Application.Common.Mapbox;
using ParkingSystem.Parking.Application.ParkingLots;
using ParkingSystem.Parking.Application.ParkingLots.DrivingTime;
using Xunit;

namespace ParkingSystem.Tests.Parking.UnitTests;

public sealed class ParkingDrivingTimeEstimatorTests
{
    private static readonly Guid ApprovedId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly Guid SecondId = Guid.Parse("10000000-0000-0000-0000-000000000002");

    [Fact]
    public async Task Detail_uses_approved_lot_and_preserves_provider_units()
    {
        var routes = new StubRouting();
        var service = new ParkingDrivingTimeEstimator(new StubDetails(), new StubNearby(), routes);
        var result = await service.GetForLotAsync(ApprovedId, 10.77, 106.70);
        Assert.NotNull(result);
        Assert.True(result.Reachable);
        Assert.Equal(125d, result.DurationSeconds);
        Assert.Equal(1100d, result.DistanceMeters);
        Assert.Equal((10.77, 106.70, 10.78, 106.71), routes.LastDirection);
    }

    [Fact]
    public async Task Hidden_lot_returns_null_without_provider_call()
    {
        var routes = new StubRouting();
        var service = new ParkingDrivingTimeEstimator(new StubDetails(), new StubNearby(), routes);
        Assert.Null(await service.GetForLotAsync(Guid.NewGuid(), 10.77, 106.70));
        Assert.Equal(0, routes.CallCount);
    }

    [Fact]
    public async Task Nearby_maps_matrix_rows_then_sorts_by_actual_driving_time()
    {
        var routes = new StubRouting
        {
            MatrixResults = [new MapboxRouteResult(true, 300, 1500), new MapboxRouteResult(true, 90, 1000)]
        };
        var service = new ParkingDrivingTimeEstimator(new StubDetails(), new StubNearby(), routes);
        var results = await service.GetNearbyAsync(10.77, 106.70);
        Assert.Equal(new[] { SecondId, ApprovedId }, results.Select(r => r.LotId).ToArray());
        Assert.Equal(90d, results[0].DurationSeconds);
        Assert.Equal(300d, results[1].DurationSeconds);
        Assert.Equal(2, routes.LastMatrixCount);
    }

    [Fact]
    public async Task Unreachable_destinations_have_no_fabricated_eta()
    {
        var routes = new StubRouting
        {
            MatrixResults = [new MapboxRouteResult(true, 180, 1600), new MapboxRouteResult(false, null, null)]
        };
        var service = new ParkingDrivingTimeEstimator(new StubDetails(), new StubNearby(), routes);
        var results = await service.GetNearbyAsync(10.77, 106.70);
        Assert.True(results[0].Reachable);
        Assert.False(results[1].Reachable);
        Assert.Null(results[1].DurationSeconds);
        Assert.Null(results[1].DistanceMeters);
    }

    [Fact]
    public async Task Invalid_search_parameters_fail_before_provider_call()
    {
        var routes = new StubRouting();
        var service = new ParkingDrivingTimeEstimator(new StubDetails(), new StubNearby(), routes);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => service.GetNearbyAsync(double.NaN, 106.70));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => service.GetNearbyAsync(10.77, 106.70, limit: 11));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => service.GetNearbyAsync(10.77, 106.70, radiusKm: -1));
        Assert.Equal(0, routes.CallCount);
    }

    private sealed class StubDetails : IParkingLotDetailReader
    {
        public Task<ParkingLotDetail?> GetApprovedByIdAsync(Guid lotId, CancellationToken ct) =>
            Task.FromResult<ParkingLotDetail?>(lotId == ApprovedId
                ? new ParkingLotDetail(ApprovedId, "Approved", "A", 10.78m, 106.71m, "Outdoor")
                : null);
    }

    private sealed class StubNearby : INearbyParkingLotReader
    {
        public Task<IReadOnlyList<NearbyParkingLotItem>> SearchAsync(
            decimal latitude, decimal longitude, double radiusKm, int limit, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<NearbyParkingLotItem>>([
                new NearbyParkingLotItem(ApprovedId, "First", "A", 10.78m, 106.71m, "Outdoor", 1.2),
                new NearbyParkingLotItem(SecondId, "Second", "B", 10.79m, 106.72m, "Outdoor", 2.3)
            ]);
    }

    private sealed class StubRouting : IMapboxGeoRouting
    {
        public int CallCount { get; private set; }
        public int LastMatrixCount { get; private set; }
        public (double, double, double, double) LastDirection { get; private set; }
        public IReadOnlyList<MapboxRouteResult> MatrixResults { get; set; } =
            [new MapboxRouteResult(true, 120, 900), new MapboxRouteResult(true, 240, 2000)];

        public Task<MapboxGeocodedPlace?> GeocodeAsync(string address, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<MapboxRouteResult> DirectionsAsync(double a, double b, double c, double d, CancellationToken ct = default)
        {
            CallCount++;
            LastDirection = (a, b, c, d);
            return Task.FromResult(new MapboxRouteResult(true, 125, 1100));
        }

        public Task<IReadOnlyList<MapboxRouteResult>> MatrixAsync(
            double a, double b, IReadOnlyList<(double Latitude, double Longitude)> destinations,
            CancellationToken ct = default)
        {
            CallCount++;
            LastMatrixCount = destinations.Count;
            return Task.FromResult(MatrixResults);
        }
    }
}
