using MediatR;
using ParkingSystem.BuildingBlocks.Results;

namespace ParkingSystem.Identity.Application.Accounts.Admin.SearchAccounts;

public sealed record SearchAccountsQuery(
    string? Search,
    string? Role,
    string? Status,
    int Page,
    int PageSize) : IRequest<Result<SearchAccountsResult>>;

public sealed record AccountSearchItem(
    Guid Id,
    string Email,
    string? PhoneNumber,
    string? FullName,
    string Role,
    string Status,
    DateTimeOffset CreatedAtUtc);

public sealed record SearchAccountsResult(
    IReadOnlyList<AccountSearchItem> Items,
    int TotalCount,
    int Page,
    int PageSize);

public static class SearchAccountsErrors
{
    public static readonly Error InvalidPageSize =
        new("identity.invalid_page_size", "PageSize phải từ 1 đến 100.");

    public static readonly Error InvalidRole =
        new("identity.invalid_role", "Giá trị role không hợp lệ.");

    public static readonly Error InvalidStatus =
        new("identity.invalid_status", "Giá trị status không hợp lệ.");
}
