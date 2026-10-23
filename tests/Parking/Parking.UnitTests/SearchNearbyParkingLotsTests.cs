using ParkingSystem.Parking.Application.ParkingLots;
using ParkingSystem.Parking.Application.ParkingLots.SearchNearbyParkingLots;
using Xunit;

namespace ParkingSystem.Tests.Parking.UnitTests;

public sealed class SearchNearbyParkingLotsTests
{
    [Fact]
    public void Validator_rejects_invalid_coordinates_radius_and_limit()
    {
        var validator = new SearchNearbyParkingLotsQueryValidator();
        Assert.False(validator.Validate(new SearchNearbyParkingLotsQuery(90.1m, 106m)).IsValid);
        Assert.False(validator.Validate(new SearchNearbyParkingLotsQuery(10m, 180.1m)).IsValid);
        Assert.False(validator.Validate(new SearchNearbyParkingLotsQuery(10m, 106m, 0)).IsValid);
        Assert.False(validator.Validate(new SearchNearbyParkingLotsQuery(10m, 106m, 51)).IsValid);
        Assert.False(validator.Validate(new SearchNearbyParkingLotsQuery(10m, 106m, 5, 0)).IsValid);
        Assert.False(validator.Validate(new SearchNearbyParkingLotsQuery(10m, 106m, 5, 101)).IsValid);
        Assert.True(validator.Validate(new SearchNearbyParkingLotsQuery(10.7m, 106.7m)).IsValid);
    }

    [Fact]
    public async Task Handler_forwards_valid_search_parameters()
    {
        var reader = new FakeNearbyReader();
        var handler = new SearchNearbyParkingLotsQueryHandler(reader);
        var request = new SearchNearbyParkingLotsQuery(10.77m, 106.70m, 7.5, 12);

        var result = await handler.Handle(request, TestContext.Current.CancellationToken);

        Assert.Empty(result);
        Assert.Equal(10.77m, reader.Latitude);
        Assert.Equal(106.70m, reader.Longitude);
        Assert.Equal(7.5, reader.RadiusKm);
        Assert.Equal(12, reader.Limit);
    }

    private sealed class FakeNearbyReader : INearbyParkingLotReader
    {
        public decimal Latitude { get; private set; }
        public decimal Longitude { get; private set; }
        public double RadiusKm { get; private set; }
        public int Limit { get; private set; }

        public Task<IReadOnlyList<NearbyParkingLotItem>> SearchAsync(
            decimal latitude, decimal longitude, double radiusKm, int limit,
            CancellationToken cancellationToken)
        {
            Latitude = latitude;
            Longitude = longitude;
            RadiusKm = radiusKm;
            Limit = limit;
            return Task.FromResult<IReadOnlyList<NearbyParkingLotItem>>([]);
        }
    }
}
