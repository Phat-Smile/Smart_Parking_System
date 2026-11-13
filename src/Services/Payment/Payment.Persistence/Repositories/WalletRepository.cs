using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ParkingSystem.Payment.Application.Common.Interfaces;
using ParkingSystem.Payment.Domain.Entities;
using ParkingSystem.Payment.Domain.Enums;

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

    // Tra cứu bút toán theo mã đặt chỗ, lọc tài khoản chống IDOR
    public async Task<IReadOnlyList<WalletLedgerEntry>> GetReservationLedgerEntriesAsync(
        Guid reservationId,
        Guid? userId = null,
        CancellationToken cancellationToken = default)
    {
        var idStr = reservationId.ToString();
        var idGuidStr = reservationId.ToString("D");
        var idCompactStr = reservationId.ToString("N");

        var query = _dbContext.WalletLedgerEntries
            .AsNoTracking()
            .Where(e => e.ReferenceType == ReferenceType.Reservation
                     && (e.ReferenceId == idStr || e.ReferenceId == idGuidStr || e.ReferenceId == idCompactStr));

        if (userId.HasValue)
        {
            var targetUserId = userId.Value;
            var userWalletIds = _dbContext.Wallets
                .AsNoTracking()
                .Where(w => w.UserId == targetUserId)
                .Select(w => w.Id);

            query = query.Where(e => e.CreatedBy == targetUserId || userWalletIds.Contains(e.WalletId));
        }

        return await query
            .OrderBy(e => e.CreatedAt)
            .ToListAsync(cancellationToken);
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
