using ParkingSystem.Identity.Domain.Accounts;

namespace ParkingSystem.Identity.Application.Accounts;

public enum SaveAccountResult
{
    Success,
    DuplicateEmailConflict
}

public interface IAccountRepository
{
    Task<bool> EmailExistsAsync(string normalizedEmail, CancellationToken cancellationToken);

    Task<Account?> FindByEmailAsync(string normalizedEmail, CancellationToken cancellationToken);

    Task<Account?> FindByEmailForUpdateAsync(string normalizedEmail, CancellationToken cancellationToken);

    Task<Account?> FindByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<Account?> FindByIdForUpdateAsync(Guid id, CancellationToken cancellationToken);

    void Add(Account account);

    /// <summary>Saves a new account with email uniqueness check. Returns DuplicateEmailConflict when the email index violates unique constraint.</summary>
    Task<SaveAccountResult> SaveNewAccountAsync(CancellationToken cancellationToken);

    /// <summary>Saves pending changes. Returns false when the email is already registered (unique index).</summary>
    Task<bool> TrySaveChangesAsync(CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Executes the specified business action within an explicit transaction boundary.
    /// Used to coordinate row-level locks and atomic state mutations.
    /// </summary>
    Task<TResult> ExecuteInTransactionAsync<TResult>(Func<Task<TResult>> action, CancellationToken cancellationToken);
}
