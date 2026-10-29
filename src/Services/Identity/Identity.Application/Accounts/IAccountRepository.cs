using ParkingSystem.Identity.Domain.Accounts;

namespace ParkingSystem.Identity.Application.Accounts;

public enum SaveAccountResult
{
    Success,
    DuplicateEmailConflict,
    DuplicatePhoneConflict
}

public interface IAccountRepository
{
    Task<bool> EmailExistsAsync(string normalizedEmail, CancellationToken cancellationToken);

    Task<Account?> FindByEmailAsync(string normalizedEmail, CancellationToken cancellationToken);

    Task<Account?> FindByEmailForUpdateAsync(string normalizedEmail, CancellationToken cancellationToken);

    Task<Account?> FindByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<Account?> FindByIdForUpdateAsync(Guid id, CancellationToken cancellationToken);

    Task<bool> OwnerTaxCodeExistsAsync(string taxCode, Guid? excludeAccountId, CancellationToken cancellationToken) => Task.FromResult(false);

    /// <summary>
    /// Saves an owner application. Returns false when another account took the same tax code between the existence check
    /// and the save (unique index). The check cannot see an application that is not committed yet, so two applications
    /// racing for one code must end with one success and one business error, not an unhandled exception.
    /// </summary>
    async Task<bool> TrySaveOwnerApplicationAsync(CancellationToken cancellationToken)
    {
        await SaveChangesAsync(cancellationToken);
        return true;
    }

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
