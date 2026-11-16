using ParkingSystem.Identity.Application.Accounts.Admin.GetAuditLogs;
using ParkingSystem.Identity.Application.Common.Interfaces;
using Xunit;

namespace ParkingSystem.Identity.UnitTests;

public sealed class GetIdentityAuditLogsQueryHandlerTests
{
    private sealed class FakeIdentityAuditLogQueryRepository : IIdentityAuditLogQueryRepository
    {
        public IdentityAuditLogFilter? LastFilter { get; private set; }

        public Task<AuditLogsPagedDto> GetAuditLogsAsync(IdentityAuditLogFilter filter, CancellationToken cancellationToken)
        {
            LastFilter = filter;
            var items = new List<AuditLogDto>
            {
                new(
                    Guid.NewGuid(),
                    DateTime.UtcNow,
                    "owner_application.approved",
                    "identity",
                    Guid.NewGuid(),
                    "admin@example.com",
                    "SystemAdmin",
                    null,
                    "OwnerApplication",
                    Guid.NewGuid().ToString(),
                    "Approved documents",
                    "127.0.0.1",
                    null)
            };

            return Task.FromResult(new AuditLogsPagedDto(items, filter.Page, filter.PageSize, 1, 1));
        }
    }

    [Fact]
    public async Task Handle_ValidQuery_ReturnsPagedLogs()
    {
        var repo = new FakeIdentityAuditLogQueryRepository();
        var handler = new GetIdentityAuditLogsQueryHandler(repo);
        var query = new GetIdentityAuditLogsQuery(1, 20, "owner_application.approved");

        var result = await handler.Handle(query, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(repo.LastFilter);
        Assert.NotNull(result.Value);
        Assert.Equal(1, result.Value.TotalItems);
        Assert.Equal(1, result.Value.Page);
        Assert.Equal(20, result.Value.PageSize);
        Assert.Equal("owner_application.approved", repo.LastFilter.ActionType);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Handle_InvalidPage_ReturnsFailure(int page)
    {
        var repo = new FakeIdentityAuditLogQueryRepository();
        var handler = new GetIdentityAuditLogsQueryHandler(repo);
        var query = new GetIdentityAuditLogsQuery(page, 20);

        var result = await handler.Handle(query, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("identity.audit.invalid_page", result.Error!.Code);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(101)]
    public async Task Handle_InvalidPageSize_ReturnsFailure(int pageSize)
    {
        var repo = new FakeIdentityAuditLogQueryRepository();
        var handler = new GetIdentityAuditLogsQueryHandler(repo);
        var query = new GetIdentityAuditLogsQuery(1, pageSize);

        var result = await handler.Handle(query, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("identity.audit.invalid_page_size", result.Error!.Code);
    }
}
