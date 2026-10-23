using ParkingSystem.Payment.Domain.Entities;

namespace ParkingSystem.Payment.Application.Common.Interfaces;

public interface IVnpayTransactionRepository
{
    Task<VnpayTransaction?> GetByTxnRefAsync(string txnRef, CancellationToken cancellationToken);

    /// <summary>
    /// // Tìm đơn hàng theo mã giao dịch
    /// </summary>
    Task<VnpayTransaction?> GetByWalletIdAndRequestKeyAsync(
        Guid walletId, string clientRequestKey, CancellationToken cancellationToken);

    Task AddAsync(VnpayTransaction transaction, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);

    // Tim giao dich nap vi theo ma tham chieu va tai khoan so huu
    Task<VnpayTransaction?> GetTopupByTxnRefAndUserIdAsync(string txnRef, Guid userId, CancellationToken cancellationToken);
}
