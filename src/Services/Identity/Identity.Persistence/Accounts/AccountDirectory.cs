using Microsoft.EntityFrameworkCore;
using ParkingSystem.Identity.Application.Accounts.AdminAccounts;

namespace ParkingSystem.Identity.Persistence.Accounts;

internal sealed class AccountDirectory(IdentityDbContext dbContext) : IAccountDirectory
{
    public async Task<AccountPage> SearchAsync(AccountSearchFilter filter, DateTimeOffset nowUtc, CancellationToken cancellationToken)
    {
        // Một truy vấn có left join Profile để lấy họ tên, không tra từng tài khoản (tránh N+1).
        var query =
            from account in dbContext.Accounts.AsNoTracking()
            join profile in dbContext.Profiles.AsNoTracking() on account.Id equals profile.AccountId into profiles
            from profile in profiles.DefaultIfEmpty()
            select new { account, FullName = profile == null ? null : profile.FullName };

        if (filter.Search is { } search)
        {
            // Email đã lưu dạng chữ thường; ký tự % và _ của người dùng được escape để không thành ký tự đại diện.
            var pattern = "%" + search.ToLowerInvariant().Replace(@"\", @"\\").Replace("%", @"\%").Replace("_", @"\_") + "%";
            query = query.Where(row => EF.Functions.Like(row.account.Email, pattern, @"\")
                || (row.FullName != null && EF.Functions.ILike(row.FullName, pattern, @"\")));
        }

        if (filter.Role is { } role)
            query = query.Where(row => row.account.Role == role);

        if (filter.Status is { } status)
            query = query.Where(row => row.account.Status == status);

        var totalCount = await query.CountAsync(cancellationToken);
        var rows = await query
            .OrderByDescending(row => row.account.CreatedAtUtc)
            .ThenBy(row => row.account.Id)
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .Select(row => new
            {
                row.account.Id,
                row.account.Email,
                row.FullName,
                row.account.Role,
                row.account.Status,
                row.account.LockoutEndUtc,
                row.account.CreatedAtUtc
            })
            .ToListAsync(cancellationToken);

        var items = rows
            .Select(row => new AccountSummary(
                row.Id,
                row.Email,
                row.FullName,
                row.Role.ToString(),
                row.Status.ToString(),
                row.LockoutEndUtc > nowUtc,
                row.LockoutEndUtc,
                row.CreatedAtUtc))
            .ToList();
        return new AccountPage(items, filter.Page, filter.PageSize, totalCount);
    }
}
