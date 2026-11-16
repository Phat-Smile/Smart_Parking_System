using ParkingSystem.Parking.Application.ParkingLots.GetAuditLogs;
using Xunit;

namespace ParkingSystem.Parking.UnitTests;

public sealed class GetParkingAuditLogsQueryHandlerTests
{
    private sealed class FakeParkingAuditLogQueryRepository : IParkingAuditLogQueryRepository
    {
        public ParkingAuditLogFilter? LastFilter { get; private set; }

        public Task<AuditLogsPagedDto> GetAuditLogsAsync(ParkingAuditLogFilter filter, CancellationToken cancellationToken)
        {
            LastFilter = filter;
            var items = new List<AuditLogDto>
            {
                new(
                    Guid.NewGuid(),
                    DateTime.UtcNow,
                    "session.qr_reissued",
                    "parking",
                    Guid.NewGuid(),
                    null,
                    "Staff",
                    Guid.NewGuid(),
                    "ParkingSession",
                    Guid.NewGuid().ToString(),
                    "Lost ticket reissue",
                    null,
                    null)
            };

            return Task.FromResult(new AuditLogsPagedDto(items, filter.Page, filter.PageSize, 1, 1));
        }
    }

    [Fact]
    public async Task Handle_ValidQuery_ReturnsPagedLogs()
    {
        var repo = new FakeParkingAuditLogQueryRepository();
        var handler = new GetParkingAuditLogsQueryHandler(repo);
        var query = new GetParkingAuditLogsQuery(1, 10, "session.qr_reissued");

        var result = await handler.Handle(query, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(repo.LastFilter);
        Assert.Equal(1, result.Value.TotalItems);
        Assert.Equal("session.qr_reissued", repo.LastFilter.ActionType);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Handle_InvalidPage_ReturnsFailure(int page)
    {
        var repo = new FakeParkingAuditLogQueryRepository();
        var handler = new GetParkingAuditLogsQueryHandler(repo);
        var query = new GetParkingAuditLogsQuery(page, 10);

        var result = await handler.Handle(query, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("parking.audit.invalid_page", result.Error!.Code);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public async Task Handle_InvalidPageSize_ReturnsFailure(int pageSize)
    {
        var repo = new FakeParkingAuditLogQueryRepository();
        var handler = new GetParkingAuditLogsQueryHandler(repo);
        var query = new GetParkingAuditLogsQuery(1, pageSize);

        var result = await handler.Handle(query, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("parking.audit.invalid_page_size", result.Error!.Code);
    }
}
