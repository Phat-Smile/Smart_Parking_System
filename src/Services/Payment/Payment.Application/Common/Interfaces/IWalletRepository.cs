using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ParkingSystem.Payment.Domain.Entities;

namespace ParkingSystem.Payment.Application.Common.Interfaces;

public interface IWalletRepository
{
    Task<Wallet?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<Wallet?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken);
    Task<Wallet> GetOrCreateByUserIdAsync(Guid userId, CancellationToken cancellationToken);

    // Tra cứu danh sách bút toán theo mã đặt chỗ và tài khoản
    Task<IReadOnlyList<WalletLedgerEntry>> GetReservationLedgerEntriesAsync(
        Guid reservationId,
        Guid? userId = null,
        CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<WalletLedgerEntry>>(Array.Empty<WalletLedgerEntry>());

    Task AddAsync(Wallet wallet, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
