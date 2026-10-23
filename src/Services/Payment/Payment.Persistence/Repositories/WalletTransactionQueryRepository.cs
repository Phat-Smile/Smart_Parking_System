using Microsoft.EntityFrameworkCore;
using ParkingSystem.Payment.Application.Common.Interfaces;

namespace ParkingSystem.Payment.Persistence.Repositories;

public class WalletTransactionQueryRepository : IWalletTransactionQueryRepository
{
    private readonly PaymentDbContext _dbContext;

    public WalletTransactionQueryRepository(PaymentDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<(IReadOnlyList<WalletTransactionRawItem> Items, int TotalCount)> GetTransactionsAsync(
        Guid walletId,
        WalletTransactionFilter filter,
        CancellationToken cancellationToken)
    {
        var query = _dbContext.WalletLedgerEntries
            .AsNoTracking()
            .Where(e => e.WalletId == walletId);

        if (filter.Type.HasValue)
        {
            query = query.Where(e => e.EntryType == filter.Type.Value);
        }

        if (filter.FromUtc.HasValue)
        {
            query = query.Where(e => e.CreatedAt >= filter.FromUtc.Value);
        }

        if (filter.ToUtc.HasValue)
        {
            query = query.Where(e => e.CreatedAt < filter.ToUtc.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        // Chỉ select các cột cần thiết, sắp xếp theo Sequence giảm dần (mới nhất lên đầu)
        var items = await query
            .OrderByDescending(e => e.Sequence)
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .Select(e => new WalletTransactionRawItem(
                e.Id,
                e.CreatedAt,
                e.EntryType,
                e.Amount,
                e.BalanceAfter,
                e.Reason,
                e.ReferenceType,
                e.ReferenceId,
                e.Sequence
            ))
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }
}
