using Microsoft.EntityFrameworkCore;
using ParkingSystem.Identity.Application.Accounts.Admin;
using ParkingSystem.Identity.Application.Accounts.Admin.SearchAccounts;
using ParkingSystem.Identity.Domain.Accounts;

namespace ParkingSystem.Identity.Persistence.Accounts.Admin;

internal sealed class AdminAccountQueries(IdentityDbContext dbContext) : IAdminAccountQueries
{
    public async Task<(IReadOnlyList<AccountSearchItem> Items, int TotalCount)> SearchAsync(
        string? search,
        string? role,
        string? status,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var query = from a in dbContext.Accounts.AsNoTracking()
                    join p in dbContext.Profiles.AsNoTracking() on a.Id equals p.AccountId into profileGroup
                    from p in profileGroup.DefaultIfEmpty()
                    select new { Account = a, Profile = p };

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLowerInvariant();
            query = query.Where(x =>
                x.Account.Email.Contains(term) ||
                (x.Profile != null && x.Profile.FullName.ToLower().Contains(term)) ||
                (x.Account.PhoneNumber != null && x.Account.PhoneNumber.Contains(term)));
        }

        if (role is not null && Enum.TryParse<AccountRole>(role, ignoreCase: true, out var parsedRole))
            query = query.Where(x => x.Account.Role == parsedRole);

        if (status is not null && Enum.TryParse<AccountStatus>(status, ignoreCase: true, out var parsedStatus))
            query = query.Where(x => x.Account.Status == parsedStatus);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(x => x.Account.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new AccountSearchItem(
                x.Account.Id,
                x.Account.Email,
                x.Account.PhoneNumber,
                x.Profile != null ? x.Profile.FullName : null,
                x.Account.Role.ToString(),
                x.Account.Status.ToString(),
                x.Account.CreatedAtUtc))
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }
}
