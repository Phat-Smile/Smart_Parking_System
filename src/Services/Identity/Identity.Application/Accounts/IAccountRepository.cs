using ParkingSystem.Identity.Domain.Accounts;

namespace ParkingSystem.Identity.Application.Accounts;

public interface IAccountRepository
{
    Task<bool> EmailExistsAsync(string normalizedEmail, CancellationToken cancellationToken);

    Task<Account?> FindByEmailAsync(string normalizedEmail, CancellationToken cancellationToken);

    Task<Account?> FindByIdAsync(Guid id, CancellationToken cancellationToken);

    void Add(Account account);

    /// <summary>Saves pending changes. Returns false when the email is already registered (unique index).</summary>
    Task<bool> TrySaveChangesAsync(CancellationToken cancellationToken);
}
