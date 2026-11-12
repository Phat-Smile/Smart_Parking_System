using Microsoft.EntityFrameworkCore;
using ParkingSystem.Payment.Application.Common.Interfaces;
using ParkingSystem.Payment.Domain.Entities;
using ParkingSystem.Payment.Domain.Enums;

namespace ParkingSystem.Payment.Persistence.Repositories;

public class VnpayTransactionRepository : IVnpayTransactionRepository
{
    private readonly PaymentDbContext _dbContext;

    public VnpayTransactionRepository(PaymentDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<VnpayTransaction?> GetByTxnRefAsync(string txnRef, CancellationToken cancellationToken)
    {
        return await _dbContext.VnpayTransactions
            .FirstOrDefaultAsync(t => t.TxnRef == txnRef, cancellationToken);
    }

    public async Task<VnpayTransaction?> GetByWalletIdAndRequestKeyAsync(
        Guid walletId,
        string clientRequestKey,
        CancellationToken cancellationToken)
    {
        return await _dbContext.VnpayTransactions
            .FirstOrDefaultAsync(
                t => t.WalletId == walletId && t.ClientRequestKey == clientRequestKey,
                cancellationToken);
    }

    // Tim giao dich nap vi theo ma tham chieu va tai khoan so huu
    public async Task<VnpayTransaction?> GetTopupByTxnRefAndUserIdAsync(
        string txnRef,
        Guid userId,
        CancellationToken cancellationToken)
    {
        return await _dbContext.VnpayTransactions
            .AsNoTracking()
            .Join(
                _dbContext.Wallets.AsNoTracking().Where(w => w.UserId == userId),
                txn => txn.WalletId,
                wallet => wallet.Id,
                (txn, wallet) => txn)
            .Where(t => t.TxnRef == txnRef && t.Purpose == VnpayTransactionPurpose.WalletTopUp)
            .FirstOrDefaultAsync(cancellationToken);
    }

    // Lay toi da cac khoan nap dang cho xu ly, xep moi nhat len dau
    public async Task<IReadOnlyList<VnpayTransaction>> GetRecentPendingByWalletIdAsync(
        Guid walletId,
        int limit,
        CancellationToken cancellationToken)
    {
        var nowUtc = DateTimeOffset.UtcNow;
        return await _dbContext.VnpayTransactions
            .AsNoTracking()
            .Where(t => t.WalletId == walletId
                && t.Status == VnpayTransactionStatus.Pending
                && t.ExpiresAt > nowUtc)
            .OrderByDescending(t => t.CreatedAt)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<VnpayTransaction>> GetRecentExpiredTopupsByWalletIdAsync(
        Guid walletId,
        int limit,
        CancellationToken cancellationToken)
    {
        var nowUtc = DateTimeOffset.UtcNow;
        return await _dbContext.VnpayTransactions
            .AsNoTracking()
            .Where(t => t.WalletId == walletId
                && t.Purpose == VnpayTransactionPurpose.WalletTopUp
                && (t.Status == VnpayTransactionStatus.Expired
                    || (t.Status == VnpayTransactionStatus.Pending && t.ExpiresAt <= nowUtc)))
            .OrderByDescending(t => t.ExpiresAt)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(VnpayTransaction transaction, CancellationToken cancellationToken)
    {
        await _dbContext.VnpayTransactions.AddAsync(transaction, cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
