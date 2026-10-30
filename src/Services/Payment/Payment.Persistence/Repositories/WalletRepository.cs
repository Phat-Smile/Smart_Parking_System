using Microsoft.EntityFrameworkCore;
using ParkingSystem.Payment.Application.Common.Interfaces;
using ParkingSystem.Payment.Domain.Entities;

namespace ParkingSystem.Payment.Persistence.Repositories;

public class WalletRepository : IWalletRepository
{
    private readonly PaymentDbContext _dbContext;

    public WalletRepository(PaymentDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Wallet?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return await _dbContext.Wallets
            .FirstOrDefaultAsync(w => w.Id == id, cancellationToken);
    }

    public async Task<Wallet?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken)
    {
        return await _dbContext.Wallets
            .FirstOrDefaultAsync(w => w.UserId == userId, cancellationToken);
    }

    public async Task<Wallet> GetOrCreateByUserIdAsync(Guid userId, CancellationToken cancellationToken)
    {
        var wallet = await _dbContext.Wallets.FirstOrDefaultAsync(w => w.UserId == userId, cancellationToken);
        if (wallet != null)
        {
            return wallet;
        }

        var newWallet = Wallet.Create(userId);
        await _dbContext.Wallets.AddAsync(newWallet, cancellationToken);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
            return newWallet;
        }
        catch (DbUpdateException)
        {
            // Nếu 2 request tạo ví đồng thời cùng lúc, một request sẽ bị lỗi trùng khóa (Unique Constraint).
            // Ngừng theo dõi bản ghi lỗi trong DbContext và lấy lại bản ghi ví đã được tạo bởi request kia.
            _dbContext.Entry(newWallet).State = EntityState.Detached;
            return (await _dbContext.Wallets.FirstOrDefaultAsync(w => w.UserId == userId, cancellationToken))!;
        }
    }

    public async Task AddAsync(Wallet wallet, CancellationToken cancellationToken)
    {
        await _dbContext.Wallets.AddAsync(wallet, cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
