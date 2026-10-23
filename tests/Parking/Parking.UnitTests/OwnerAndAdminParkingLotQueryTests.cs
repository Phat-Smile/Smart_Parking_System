using ParkingSystem.Parking.Application.ParkingLots.AdminLots;
using ParkingSystem.Parking.Application.ParkingLots.OwnerLots;
using ParkingSystem.Parking.Domain.ParkingLots;
using Xunit;

namespace ParkingSystem.Tests.Parking.UnitTests;

public sealed class OwnerAndAdminParkingLotQueryTests
{
    [Fact]
    public async Task GetOwnerParkingLotsQueryHandler_returns_lots_from_reader()
    {
        var ownerId = Guid.NewGuid();
        IReadOnlyList<OwnerParkingLotItem> expected =
        [
            new(
                Guid.NewGuid(),
                "Owner Lot 1",
                "123 Nguyen Hue",
                10.77m,
                106.70m,
                "Commercial",
                "Pending",
                null),
            new(
                Guid.NewGuid(),
                "Owner Lot 2",
                "456 Le Loi",
                10.78m,
                106.71m,
                "Residential",
                "Approved",
                null)
        ];

        var fakeReader = new FakeOwnerParkingLotReader(expected);
        var handler = new GetOwnerParkingLotsQueryHandler(fakeReader);

        var result = await handler.Handle(
            new GetOwnerParkingLotsQuery(ownerId),
            TestContext.Current.CancellationToken);

        Assert.Same(expected, result);
        Assert.Equal(ownerId, fakeReader.LastOwnerId);
        Assert.Equal(1, fakeReader.CallCount);
    }

    [Fact]
    public async Task GetAdminParkingLotsQueryHandler_returns_lots_filtered_or_unfiltered()
    {
        IReadOnlyList<AdminParkingLotItem> expected =
        [
            new(
                Guid.NewGuid(),
                Guid.NewGuid(),
                "Pending Lot",
                "789 Pasteur",
                10.79m,
                106.69m,
                "Commercial",
                "Pending",
                null)
        ];

        var fakeReader = new FakeAdminParkingLotReader(expected);
        var handler = new GetAdminParkingLotsQueryHandler(fakeReader);

        // Case 1: Filter by Pending
        var resultPending = await handler.Handle(
            new GetAdminParkingLotsQuery(ParkingLotApprovalStatus.Pending),
            TestContext.Current.CancellationToken);

        Assert.Same(expected, resultPending);
        Assert.Equal(ParkingLotApprovalStatus.Pending, fakeReader.LastStatus);
        Assert.Equal(1, fakeReader.CallCount);

        // Case 2: No filter
        var resultAll = await handler.Handle(
            new GetAdminParkingLotsQuery(null),
            TestContext.Current.CancellationToken);

        Assert.Same(expected, resultAll);
        Assert.Null(fakeReader.LastStatus);
        Assert.Equal(2, fakeReader.CallCount);
    }

    private sealed class FakeOwnerParkingLotReader(IReadOnlyList<OwnerParkingLotItem> result) : IOwnerParkingLotReader
    {
        public int CallCount { get; private set; }
        public Guid? LastOwnerId { get; private set; }

        public Task<IReadOnlyList<OwnerParkingLotItem>> ListByOwnerAsync(Guid ownerUserId, CancellationToken cancellationToken)
        {
            CallCount++;
            LastOwnerId = ownerUserId;
            return Task.FromResult(result);
        }
    }

    private sealed class FakeAdminParkingLotReader(IReadOnlyList<AdminParkingLotItem> result) : IAdminParkingLotReader
    {
        public int CallCount { get; private set; }
        public ParkingLotApprovalStatus? LastStatus { get; private set; }

        public Task<IReadOnlyList<AdminParkingLotItem>> ListForAdminAsync(ParkingLotApprovalStatus? status, CancellationToken cancellationToken)
        {
            CallCount++;
            LastStatus = status;
            return Task.FromResult(result);
        }
    }
}
