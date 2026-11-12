using Microsoft.EntityFrameworkCore;
using Npgsql;
using ParkingSystem.Identity.Application.Accounts;
using ParkingSystem.Identity.Domain.Accounts;

namespace ParkingSystem.Identity.Persistence.Accounts;

internal sealed class AccountRepository(IdentityDbContext dbContext) : IAccountRepository, IGoogleAccountRepository
{
    public async Task<Account?> FindByGoogleSubjectForUpdateAsync(string googleSubject, CancellationToken cancellationToken)
    {
        return await dbContext.Accounts
            .FromSqlInterpolated($"SELECT * FROM identity.accounts WHERE google_subject = {googleSubject} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);
    }

    public Task<Account?> FindByGoogleSubjectAsync(string googleSubject, CancellationToken cancellationToken) =>
        dbContext.Accounts.SingleOrDefaultAsync(account => account.GoogleSubject == googleSubject, cancellationToken);

    public Task<bool> EmailExistsAsync(string normalizedEmail, CancellationToken cancellationToken) =>
        dbContext.Accounts.AsNoTracking().AnyAsync(account => account.Email == normalizedEmail, cancellationToken);

    public Task<Account?> FindByEmailAsync(string normalizedEmail, CancellationToken cancellationToken) =>
        dbContext.Accounts.SingleOrDefaultAsync(account => account.Email == normalizedEmail, cancellationToken);

    public Task<bool> OwnerTaxCodeExistsAsync(string taxCode, Guid? excludeAccountId, CancellationToken cancellationToken)
    {
        // Same rule as the unique index: only an application in force (Pending or Approved) holds a tax code. A rejected one
        // keeps the code in its record but does not hold it against other accounts.
        var query = dbContext.Accounts.AsNoTracking().Where(account =>
            account.OwnerTaxCode == taxCode
            && (account.OwnerApplicationStatus == OwnerApplicationStatus.Pending
                || account.OwnerApplicationStatus == OwnerApplicationStatus.Approved));
        if (excludeAccountId.HasValue)
        {
            query = query.Where(account => account.Id != excludeAccountId.Value);
        }

        return query.AnyAsync(cancellationToken);
    }

    public Task<Account?> FindByIdAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.Accounts.SingleOrDefaultAsync(account => account.Id == id, cancellationToken);

    public async Task<Account?> FindByIdForUpdateAsync(Guid id, CancellationToken cancellationToken)
    {
        return await dbContext.Accounts
            .FromSqlInterpolated($"SELECT * FROM identity.accounts WHERE id = {id} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);
    }

    public void Add(Account account) => dbContext.Accounts.Add(account);

    public async Task<SaveAccountResult> SaveNewAccountAsync(CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return SaveAccountResult.Success;
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException pg
            && pg.SqlState == PostgresErrorCodes.UniqueViolation
            && pg.ConstraintName is AccountConfiguration.EmailIndexName or AccountConfiguration.PhoneNumberIndexName)
        {
            if (dbContext.Database.CurrentTransaction is not null)
            {
                await dbContext.Database.CurrentTransaction.RollbackAsync(cancellationToken);
            }

            dbContext.ChangeTracker.Clear();
            return pg.ConstraintName == AccountConfiguration.PhoneNumberIndexName
                ? SaveAccountResult.DuplicatePhoneConflict
                : SaveAccountResult.DuplicateEmailConflict;
        }
    }

    public async Task<bool> TrySaveChangesAsync(CancellationToken cancellationToken)
    {
        var result = await SaveNewAccountAsync(cancellationToken);
        return result == SaveAccountResult.Success;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        dbContext.SaveChangesAsync(cancellationToken);

    public async Task<bool> TrySaveOwnerApplicationAsync(CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: AccountConfiguration.OwnerTaxCodeIndexName
        })
        {
            // Another application took the tax code between the check and the save. Nothing of this application is kept
            // (the failed save is rolled back), so the caller answers with a business error. Drop the tracked changes so
            // they cannot be saved by mistake.
            dbContext.ChangeTracker.Clear();
            return false;
        }
    }

    public async Task<Account?> FindByEmailForUpdateAsync(string normalizedEmail, CancellationToken cancellationToken)
    {
        return await dbContext.Accounts
            .FromSqlInterpolated($"SELECT * FROM identity.accounts WHERE email = {normalizedEmail} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<TResult> ExecuteInTransactionAsync<TResult>(Func<Task<TResult>> action, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(action);

        if (dbContext.Database.CurrentTransaction != null)
        {
            return await action();
        }

        var strategy = dbContext.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted, cancellationToken);
            var result = await action();
            await transaction.CommitAsync(cancellationToken);
            return result;
        });
    }
}
