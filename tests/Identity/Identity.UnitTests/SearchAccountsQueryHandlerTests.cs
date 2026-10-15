using ParkingSystem.Identity.Application.Admin;
using ParkingSystem.Identity.Application.Admin.SearchAccounts;
using Xunit;

namespace ParkingSystem.Tests.Identity.UnitTests;

public sealed class SearchAccountsQueryHandlerTests
{
    [Fact]
    public async Task Returns_invalid_page_size_error_when_pageSize_exceeds_100()
    {
        var handler = new SearchAccountsQueryHandler(new FakeAdminAccountQueries([]));
        var result = await handler.Handle(new SearchAccountsQuery(null, null, null, 1, 101), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("identity.invalid_page_size", result.Error!.Code);
    }

    [Fact]
    public async Task Returns_invalid_role_error_when_role_string_is_not_a_valid_enum_value()
    {
        var handler = new SearchAccountsQueryHandler(new FakeAdminAccountQueries([]));
        var result = await handler.Handle(new SearchAccountsQuery(null, "SuperAdmin", null, 1, 20), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("identity.invalid_role", result.Error!.Code);
    }

    [Fact]
    public async Task Returns_invalid_status_error_when_status_string_is_not_a_valid_enum_value()
    {
        var handler = new SearchAccountsQueryHandler(new FakeAdminAccountQueries([]));
        var result = await handler.Handle(new SearchAccountsQuery(null, null, "Banned", 1, 20), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("identity.invalid_status", result.Error!.Code);
    }

    [Fact]
    public async Task Returns_results_for_valid_query()
    {
        var item = new AccountSearchItem(Guid.NewGuid(), "a@test.com", null, null, "Customer", "EmailVerified",
            new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero));
        var handler = new SearchAccountsQueryHandler(new FakeAdminAccountQueries([item]));

        var result = await handler.Handle(new SearchAccountsQuery("a@test.com", null, null, 1, 20), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value!.TotalCount);
        Assert.Single(result.Value.Items);
    }

    [Fact]
    public async Task Uses_default_page_size_when_pageSize_is_zero()
    {
        int capturedPageSize = 0;
        var fake = new CapturingFakeQueries(captured => capturedPageSize = captured);
        var handler = new SearchAccountsQueryHandler(fake);

        await handler.Handle(new SearchAccountsQuery(null, null, null, 1, 0), CancellationToken.None);

        Assert.Equal(20, capturedPageSize);
    }

    [Theory]
    [InlineData("Customer")]
    [InlineData("customer")]
    [InlineData("CUSTOMER")]
    [InlineData("Staff")]
    [InlineData("ParkingLotOwner")]
    [InlineData("SystemAdmin")]
    public async Task Accepts_valid_role_values_case_insensitively(string role)
    {
        var handler = new SearchAccountsQueryHandler(new FakeAdminAccountQueries([]));
        var result = await handler.Handle(new SearchAccountsQuery(null, role, null, 1, 20), CancellationToken.None);
        Assert.True(result.IsSuccess);
    }

    [Theory]
    [InlineData("PendingEmailVerification")]
    [InlineData("EmailVerified")]
    [InlineData("Suspended")]
    [InlineData("Disabled")]
    public async Task Accepts_valid_status_values_case_insensitively(string status)
    {
        var handler = new SearchAccountsQueryHandler(new FakeAdminAccountQueries([]));
        var result = await handler.Handle(new SearchAccountsQuery(null, null, status, 1, 20), CancellationToken.None);
        Assert.True(result.IsSuccess);
    }

    private sealed class FakeAdminAccountQueries(IReadOnlyList<AccountSearchItem> items) : IAdminAccountQueries
    {
        public Task<(IReadOnlyList<AccountSearchItem> Items, int TotalCount)> SearchAsync(
            string? search, string? role, string? status, int page, int pageSize, CancellationToken cancellationToken) =>
            Task.FromResult<(IReadOnlyList<AccountSearchItem>, int)>((items, items.Count));
    }

    private sealed class CapturingFakeQueries(Action<int> onSearch) : IAdminAccountQueries
    {
        public Task<(IReadOnlyList<AccountSearchItem> Items, int TotalCount)> SearchAsync(
            string? search, string? role, string? status, int page, int pageSize, CancellationToken cancellationToken)
        {
            onSearch(pageSize);
            return Task.FromResult<(IReadOnlyList<AccountSearchItem>, int)>(([], 0));
        }
    }
}
