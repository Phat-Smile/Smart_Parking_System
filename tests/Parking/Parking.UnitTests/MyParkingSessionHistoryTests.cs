using ParkingSystem.Parking.Application.ParkingSessions.ListMyParkingSessionHistory;
using ParkingSystem.Parking.Domain.ParkingSessions;
using Xunit;

namespace ParkingSystem.Tests.Parking.UnitTests;

public sealed class MyParkingSessionHistoryTests
{
    private static readonly Guid CustomerId = Guid.NewGuid();
    private static readonly Guid LotId = Guid.NewGuid();
    private static readonly DateTimeOffset EnteredAt = new(2026, 11, 6, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task History_returns_closed_session_without_qr_and_preserves_paging()
    {
        var session = ParkingSession.Open(Guid.NewGuid(), LotId, "51A-123.45", VehicleType.Car,
            EnteredAt, "secret-qr", CustomerId, Guid.NewGuid());
        session.Cancel(Guid.NewGuid(), "Did not enter", EnteredAt.AddMinutes(3));
        var reader = new FakeReader([session], 7);
        var handler = new ListMyParkingSessionHistoryQueryHandler(reader);

        var page = await handler.Handle(new ListMyParkingSessionHistoryQuery(CustomerId, "cancelled", 2, 3),
            TestContext.Current.CancellationToken);

        Assert.Equal((CustomerId, ParkingSessionStatus.Cancelled, 3, 3), reader.LastRequest);
        Assert.Equal(7, page.TotalCount);
        Assert.Equal(2, page.Page);
        var item = Assert.Single(page.Items);
        Assert.Equal("Cancelled", item.Status);
        Assert.Equal(session.CancelledAt, item.CancelledAt);
        Assert.DoesNotContain("QrToken", typeof(MyParkingSessionHistoryItem).GetProperties().Select(property => property.Name));
    }

    [Theory]
    [InlineData("invalid", 1, 20)]
    [InlineData(null, 0, 20)]
    [InlineData(null, 1, 101)]
    public void History_rejects_invalid_filter_or_paging(string? status, int page, int pageSize)
    {
        var validator = new ListMyParkingSessionHistoryQueryValidator();
        Assert.False(validator.Validate(new ListMyParkingSessionHistoryQuery(CustomerId, status, page, pageSize)).IsValid);
    }

    private sealed class FakeReader(IReadOnlyList<ParkingSession> items, int totalCount) : IMyParkingSessionHistoryReader
    {
        public (Guid UserId, ParkingSessionStatus? Status, int Skip, int Take)? LastRequest { get; private set; }

        public Task<(IReadOnlyList<ParkingSession> Items, int TotalCount)> ListAsync(
            Guid userId, ParkingSessionStatus? status, int skip, int take, CancellationToken cancellationToken)
        {
            LastRequest = (userId, status, skip, take);
            return Task.FromResult((items, totalCount));
        }
    }
}
