using ParkingSystem.Parking.Application.ParkingLots;
using ParkingSystem.Parking.Application.ParkingLots.GetParkingLotDetail;
using Xunit;

namespace ParkingSystem.Tests.Parking.UnitTests;

public sealed class GetParkingLotDetailTests
{
    [Fact]
    public async Task Handler_returns_the_parking_lot_detail_from_the_reader()
    {
        var parkingLotId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var expected = new ParkingLotDetail(
            parkingLotId,
            "Central Parking",
            "1 Demo Street",
            10.776530m,
            106.700981m,
            "MultiStorey");
        var reader = new FakeParkingLotDetailReader(expected);
        var handler = new GetParkingLotDetailQueryHandler(reader);

        var result = await handler.Handle(
            new GetParkingLotDetailQuery(parkingLotId),
            TestContext.Current.CancellationToken);

        Assert.Same(expected, result);
        Assert.Equal(parkingLotId, reader.LastParkingLotId);
        Assert.Equal(1, reader.CallCount);
    }

    [Fact]
    public async Task Handler_returns_null_when_the_reader_cannot_expose_the_parking_lot()
    {
        var parkingLotId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var reader = new FakeParkingLotDetailReader(null);
        var handler = new GetParkingLotDetailQueryHandler(reader);

        var result = await handler.Handle(
            new GetParkingLotDetailQuery(parkingLotId),
            TestContext.Current.CancellationToken);

        Assert.Null(result);
        Assert.Equal(parkingLotId, reader.LastParkingLotId);
    }

    [Fact]
    public async Task Validator_rejects_an_empty_parking_lot_id()
    {
        var validator = new GetParkingLotDetailQueryValidator();

        var validation = await validator.ValidateAsync(
            new GetParkingLotDetailQuery(Guid.Empty),
            TestContext.Current.CancellationToken);

        Assert.False(validation.IsValid);
        Assert.Contains(validation.Errors, error => error.PropertyName == nameof(GetParkingLotDetailQuery.ParkingLotId));
    }

    private sealed class FakeParkingLotDetailReader(ParkingLotDetail? result) : IParkingLotDetailReader
    {
        public int CallCount { get; private set; }
        public Guid? LastParkingLotId { get; private set; }

        public Task<ParkingLotDetail?> GetApprovedByIdAsync(Guid parkingLotId, CancellationToken cancellationToken)
        {
            CallCount++;
            LastParkingLotId = parkingLotId;
            return Task.FromResult(result);
        }
    }
}
