using ParkingSystem.Parking.Application.Common.Paging;
using ParkingSystem.Parking.Application.ParkingLots.AdminLots;
using ParkingSystem.Parking.Application.ParkingLots.OwnerLots;
using ParkingSystem.Parking.Domain.ParkingLots;
using Xunit;

namespace ParkingSystem.Tests.Parking.UnitTests;

public sealed class OwnerAndAdminParkingLotQueryTests
{
    [Fact]
    public async Task GetOwnerParkingLotsQueryHandler_returns_the_page_of_the_reader_for_the_owner_status_and_page()
    {
        var ownerId = Guid.NewGuid();
        var expected = new PagedResult<OwnerParkingLotItem>(
            [
                new(Guid.NewGuid(), "Owner Lot 1", "123 Nguyen Hue", 10.77m, 106.70m, "Commercial", "Pending", null, false, false),
                new(Guid.NewGuid(), "Owner Lot 2", "456 Le Loi", 10.78m, 106.71m, "Residential", "Approved", null, true, true)
            ],
            TotalCount: 42,
            Page: 2,
            PageSize: 50);

        var fakeReader = new FakeOwnerParkingLotReader(expected);
        var handler = new GetOwnerParkingLotsQueryHandler(fakeReader);

        var result = await handler.Handle(
            new GetOwnerParkingLotsQuery(ownerId, ParkingLotApprovalStatus.Pending, 2, 50),
            TestContext.Current.CancellationToken);

        Assert.Same(expected, result);
        Assert.Equal(ownerId, fakeReader.LastOwnerId);
        Assert.Equal(ParkingLotApprovalStatus.Pending, fakeReader.LastStatus);
        Assert.Equal(2, fakeReader.LastPage);
        Assert.Equal(50, fakeReader.LastPageSize);
        Assert.Equal(1, fakeReader.CallCount);
    }

    [Theory]
    [InlineData(null, null, 1, 20)]
    [InlineData(0, 0, 1, 20)]
    [InlineData(-4, -1, 1, 20)]
    [InlineData(3, 1000, 3, 100)]
    public async Task GetOwnerParkingLotsQueryHandler_gives_the_reader_a_normalized_page(int? page, int? pageSize, int expectedPage, int expectedPageSize)
    {
        var fakeReader = new FakeOwnerParkingLotReader(new PagedResult<OwnerParkingLotItem>([], 0, expectedPage, expectedPageSize));
        var handler = new GetOwnerParkingLotsQueryHandler(fakeReader);

        await handler.Handle(
            new GetOwnerParkingLotsQuery(Guid.NewGuid(), null, page, pageSize),
            TestContext.Current.CancellationToken);

        Assert.Null(fakeReader.LastStatus);
        Assert.Equal(expectedPage, fakeReader.LastPage);
        Assert.Equal(expectedPageSize, fakeReader.LastPageSize);
    }

    [Fact]
    public async Task GetAdminParkingLotsQueryHandler_returns_the_page_of_the_reader_filtered_or_unfiltered()
    {
        var expected = new PagedResult<AdminParkingLotItem>(
            [
                new(Guid.NewGuid(), Guid.NewGuid(), "Pending Lot", "789 Pasteur", 10.79m, 106.69m, "Commercial", "Pending", null)
            ],
            TotalCount: 1,
            Page: 1,
            PageSize: 20);

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

    [Theory]
    [InlineData(null, null, 1, 20)]
    [InlineData(0, 0, 1, 20)]
    [InlineData(-4, -1, 1, 20)]
    [InlineData(7, 101, 7, 100)]
    public async Task GetAdminParkingLotsQueryHandler_gives_the_reader_a_normalized_page(int? page, int? pageSize, int expectedPage, int expectedPageSize)
    {
        var fakeReader = new FakeAdminParkingLotReader(new PagedResult<AdminParkingLotItem>([], 0, expectedPage, expectedPageSize));
        var handler = new GetAdminParkingLotsQueryHandler(fakeReader);

        await handler.Handle(
            new GetAdminParkingLotsQuery(null, page, pageSize),
            TestContext.Current.CancellationToken);

        Assert.Equal(expectedPage, fakeReader.LastPage);
        Assert.Equal(expectedPageSize, fakeReader.LastPageSize);
    }

    private sealed class FakeOwnerParkingLotReader(PagedResult<OwnerParkingLotItem> result) : IOwnerParkingLotReader
    {
        public int CallCount { get; private set; }
        public Guid? LastOwnerId { get; private set; }
        public ParkingLotApprovalStatus? LastStatus { get; private set; }
        public int LastPage { get; private set; }
        public int LastPageSize { get; private set; }

        public Task<PagedResult<OwnerParkingLotItem>> ListByOwnerAsync(
            Guid ownerUserId,
            ParkingLotApprovalStatus? status,
            int page,
            int pageSize,
            CancellationToken cancellationToken)
        {
            CallCount++;
            LastOwnerId = ownerUserId;
            LastStatus = status;
            LastPage = page;
            LastPageSize = pageSize;
            return Task.FromResult(result);
        }
    }

    private sealed class FakeAdminParkingLotReader(PagedResult<AdminParkingLotItem> result) : IAdminParkingLotReader
    {
        public int CallCount { get; private set; }
        public ParkingLotApprovalStatus? LastStatus { get; private set; }
        public int LastPage { get; private set; }
        public int LastPageSize { get; private set; }

        public Task<PagedResult<AdminParkingLotItem>> ListForAdminAsync(
            ParkingLotApprovalStatus? status,
            int page,
            int pageSize,
            CancellationToken cancellationToken)
        {
            CallCount++;
            LastStatus = status;
            LastPage = page;
            LastPageSize = pageSize;
            return Task.FromResult(result);
        }
    }
}
