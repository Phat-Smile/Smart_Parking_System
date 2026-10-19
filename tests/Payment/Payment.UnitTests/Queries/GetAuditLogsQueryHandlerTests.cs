using ParkingSystem.Payment.Application.Common.Interfaces;
using ParkingSystem.Payment.Application.Features.AuditLogs.Queries.GetAuditLogs;
using Xunit;

namespace ParkingSystem.Tests.Payment.UnitTests.Queries;

public sealed class GetAuditLogsQueryHandlerTests
{
    private sealed class FakeAuditLogQueryRepository(AuditLogsPagedDto returnResult) : IAuditLogQueryRepository
    {
        public AuditLogFilter? LastFilter { get; private set; }

        public Task<AuditLogsPagedDto> GetAuditLogsAsync(AuditLogFilter filter, CancellationToken cancellationToken)
        {
            LastFilter = filter;
            return Task.FromResult(returnResult);
        }
    }

    [Fact]
    public async Task Handle_WhenRepositoryReturnsData_ReturnsSuccessWithPagedDto()
    {
        var sampleItems = new List<AuditLogDto>
        {
            new(
                Id: Guid.NewGuid(),
                OccurredAtUtc: DateTime.UtcNow,
                ActionType: "CashPaymentConfirmed",
                SourceService: "payment",
                ActorId: Guid.NewGuid(),
                ActorEmail: "staff@example.com",
                ActorRole: "Staff",
                ParkingLotId: Guid.NewGuid(),
                TargetType: "ParkingSession",
                TargetId: "session-1",
                Reason: "Cash paid",
                IpAddress: "127.0.0.1",
                Details: null)
        };

        var expectedPaged = new AuditLogsPagedDto(sampleItems, Page: 1, PageSize: 20, TotalItems: 1, TotalPages: 1);
        var fakeRepo = new FakeAuditLogQueryRepository(expectedPaged);
        var handler = new GetAuditLogsQueryHandler(fakeRepo);

        var query = new GetAuditLogsQuery(Page: 1, PageSize: 20, ActionType: "CashPaymentConfirmed");
        var result = await handler.Handle(query, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Single(result.Value.Items);
        Assert.Equal(1, result.Value.TotalItems);
        Assert.Equal(1, result.Value.TotalPages);

        Assert.NotNull(fakeRepo.LastFilter);
        Assert.Equal(1, fakeRepo.LastFilter.Page);
        Assert.Equal(20, fakeRepo.LastFilter.PageSize);
        Assert.Equal("CashPaymentConfirmed", fakeRepo.LastFilter.ActionType);
    }

    [Fact]
    public async Task Handle_WhenRepositoryReturnsEmpty_ReturnsSuccessWithZeroTotal()
    {
        var expectedPaged = new AuditLogsPagedDto([], Page: 1, PageSize: 20, TotalItems: 0, TotalPages: 0);
        var fakeRepo = new FakeAuditLogQueryRepository(expectedPaged);
        var handler = new GetAuditLogsQueryHandler(fakeRepo);

        var query = new GetAuditLogsQuery(Page: 1, PageSize: 20);
        var result = await handler.Handle(query, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Empty(result.Value.Items);
        Assert.Equal(0, result.Value.TotalItems);
        Assert.Equal(0, result.Value.TotalPages);
    }
}
