using ParkingSystem.Parking.Application.ParkingLots;
using ParkingSystem.Parking.Application.ParkingLots.ListParkingLots;
using ParkingSystem.Parking.Domain.ParkingSessions;
using Xunit;

namespace ParkingSystem.Tests.Parking.UnitTests;

public sealed class ListParkingLotsTests
{
    [Fact]
    public async Task Handler_returns_the_approved_catalog_from_the_reader()
    {
        IReadOnlyList<ParkingLotCatalogItem> expected =
        [
            new(
                Guid.Parse("11111111-1111-1111-1111-111111111111"),
                "Central Parking",
                "1 Demo Street",
                10.776530m,
                106.700981m,
                "MultiStorey")
        ];
        var reader = new FakeParkingLotCatalogReader(expected);
        var handler = new ListParkingLotsQueryHandler(reader);

        var result = await handler.Handle(
            new ListParkingLotsQuery(),
            TestContext.Current.CancellationToken);

        Assert.Same(expected, result);
        Assert.Null(reader.LastVehicleType);
        Assert.Equal(1, reader.CallCount);
    }

    [Theory]
    [InlineData(VehicleType.Car)]
    [InlineData(VehicleType.Motorbike)]
    public async Task Handler_forwards_the_requested_vehicle_filter_to_the_reader(VehicleType vehicleType)
    {
        IReadOnlyList<ParkingLotCatalogItem> expected = [];
        var reader = new FakeParkingLotCatalogReader(expected);
        var handler = new ListParkingLotsQueryHandler(reader);

        var result = await handler.Handle(
            new ListParkingLotsQuery(vehicleType),
            TestContext.Current.CancellationToken);

        Assert.Same(expected, result);
        Assert.Equal(vehicleType, reader.LastVehicleType);
        Assert.Equal(1, reader.CallCount);
    }

    private sealed class FakeParkingLotCatalogReader(IReadOnlyList<ParkingLotCatalogItem> result)
        : IParkingLotCatalogReader
    {
        public int CallCount { get; private set; }
        public VehicleType? LastVehicleType { get; private set; }

        public Task<IReadOnlyList<ParkingLotCatalogItem>> ListApprovedAsync(
            CancellationToken cancellationToken,
            VehicleType? vehicleType = null)
        {
            CallCount++;
            LastVehicleType = vehicleType;
            return Task.FromResult(result);
        }
    }
}
