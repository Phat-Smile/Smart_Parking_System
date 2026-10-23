using ParkingSystem.Parking.Application.ParkingLots;
using ParkingSystem.Parking.Application.ParkingLots.SearchParkingLotsByAddress;
using ParkingSystem.Parking.Domain.ParkingSessions;
using Xunit;

namespace ParkingSystem.Tests.Parking.UnitTests;

public sealed class SearchParkingLotsByAddressTests
{
    [Fact]
    public void Validator_rejects_blank_address_out_of_range_radius_and_limit()
    {
        var validator = new SearchParkingLotsByAddressQueryValidator();
        Assert.False(validator.Validate(new SearchParkingLotsByAddressQuery("  ")).IsValid);
        Assert.False(validator.Validate(new SearchParkingLotsByAddressQuery("a")).IsValid);
        Assert.False(validator.Validate(new SearchParkingLotsByAddressQuery(new string('a', 201))).IsValid);
        Assert.False(validator.Validate(new SearchParkingLotsByAddressQuery("District 1", 0)).IsValid);
        Assert.False(validator.Validate(new SearchParkingLotsByAddressQuery("District 1", 51)).IsValid);
        Assert.False(validator.Validate(new SearchParkingLotsByAddressQuery("District 1", double.NaN)).IsValid);
        Assert.False(validator.Validate(new SearchParkingLotsByAddressQuery("District 1", 5, 0)).IsValid);
        Assert.False(validator.Validate(new SearchParkingLotsByAddressQuery("District 1", 5, 101)).IsValid);
        Assert.True(validator.Validate(new SearchParkingLotsByAddressQuery("District 1")).IsValid);
    }

    [Fact]
    public async Task Handler_returns_approved_lots_within_radius_sorted_by_distance()
    {
        var destination = new GeocodedDestination("District 1, Ho Chi Minh City", 10.776530m, 106.700981m);
        var geocoder = new FakeGeocoder(destination);
        var lots = new FakeCatalogReader([
            Lot("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa", "Far", 10.790000m, 106.710000m),
            Lot("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb", "Closest", 10.776530m, 106.700981m),
            Lot("cccccccc-cccc-cccc-cccc-cccccccccccc", "Too far", 11.800000m, 108.000000m)
        ]);
        var handler = new SearchParkingLotsByAddressQueryHandler(geocoder, lots);

        var result = await handler.Handle(
            new SearchParkingLotsByAddressQuery(" District 1 ", 5, 10),
            TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Equal(destination, result.Destination);
        Assert.Equal("District 1", geocoder.LastAddress);
        Assert.Equal(2, result.ParkingLots.Count);
        Assert.Equal("Closest", result.ParkingLots[0].Name);
        Assert.Equal(0d, result.ParkingLots[0].DistanceKm);
        Assert.Equal("Far", result.ParkingLots[1].Name);
        Assert.True(result.ParkingLots[1].DistanceKm > 0);
        Assert.Equal(1, lots.CallCount);
    }

    [Fact]
    public async Task Handler_returns_null_when_geocoder_finds_no_destination()
    {
        var geocoder = new FakeGeocoder(null);
        var lots = new FakeCatalogReader([]);
        var handler = new SearchParkingLotsByAddressQueryHandler(geocoder, lots);

        var result = await handler.Handle(
            new SearchParkingLotsByAddressQuery("Unknown destination"),
            TestContext.Current.CancellationToken);

        Assert.Null(result);
        Assert.Equal(0, lots.CallCount);
    }

    [Fact]
    public async Task Handler_returns_empty_list_when_destination_exists_but_no_lots_are_nearby()
    {
        var geocoder = new FakeGeocoder(new GeocodedDestination("Destination", 10m, 106m));
        var lots = new FakeCatalogReader([Lot("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa", "Far", 20m, 110m)]);
        var handler = new SearchParkingLotsByAddressQueryHandler(geocoder, lots);

        var result = await handler.Handle(
            new SearchParkingLotsByAddressQuery("Known place"),
            TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Empty(result.ParkingLots);
    }

    private static ParkingLotCatalogItem Lot(string id, string name, decimal lat, decimal lon) =>
        new(Guid.Parse(id), name, "Demo street", lat, lon, "Outdoor");

    private sealed class FakeGeocoder(GeocodedDestination? destination) : IAddressGeocoder
    {
        public string? LastAddress { get; private set; }
        public Task<GeocodedDestination?> GeocodeAsync(string address, CancellationToken cancellationToken)
        {
            LastAddress = address;
            return Task.FromResult(destination);
        }
    }

    private sealed class FakeCatalogReader(IReadOnlyList<ParkingLotCatalogItem> lots) : IParkingLotCatalogReader
    {
        public int CallCount { get; private set; }
        public Task<IReadOnlyList<ParkingLotCatalogItem>> ListApprovedAsync(CancellationToken cancellationToken, VehicleType? vehicleType = null)
        {
            CallCount++;
            return Task.FromResult(lots);
        }
    }
}
