using ParkingSystem.Payment.Domain.Entities;

namespace ParkingSystem.Payment.Application.Common.Interfaces;

public interface IVnpayTransactionRepository
{
    Task<VnpayTransaction?> GetByWalletIdAndRequestKeyAsync(
        Guid walletId,
        string clientRequestKey,
        CancellationToken cancellationToken);

    Task AddAsync(VnpayTransaction transaction, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
