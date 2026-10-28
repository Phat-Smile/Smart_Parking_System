using ParkingSystem.Identity.Application.Accounts.Admin.SearchAccounts;

namespace ParkingSystem.Identity.Application.Accounts.Admin;

public interface IAdminAccountQueries
{
    Task<(IReadOnlyList<AccountSearchItem> Items, int TotalCount)> SearchAsync(
        string? search,
        string? role,
        string? status,
        int page,
        int pageSize,
        CancellationToken cancellationToken);
}
