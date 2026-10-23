using Microsoft.EntityFrameworkCore;
using ParkingSystem.Payment.Application.Common.Interfaces;
using ParkingSystem.Payment.Domain.Entities;

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

    public async Task AddAsync(VnpayTransaction transaction, CancellationToken cancellationToken)
    {
        await _dbContext.VnpayTransactions.AddAsync(transaction, cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
