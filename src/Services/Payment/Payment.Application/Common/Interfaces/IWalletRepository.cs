using ParkingSystem.Payment.Domain.Entities;

namespace ParkingSystem.Payment.Application.Common.Interfaces;

public interface IWalletRepository
{
    Task<Wallet?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken);
    Task<Wallet> GetOrCreateByUserIdAsync(Guid userId, CancellationToken cancellationToken);

    Task AddAsync(Wallet wallet, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
