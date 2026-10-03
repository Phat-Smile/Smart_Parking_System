using Microsoft.EntityFrameworkCore;
using Npgsql;
using ParkingSystem.Identity.Application.Accounts;
using ParkingSystem.Identity.Domain.Accounts;

namespace ParkingSystem.Identity.Persistence.Accounts;

internal sealed class AccountRepository(IdentityDbContext dbContext) : IAccountRepository
{
    public Task<bool> EmailExistsAsync(string normalizedEmail, CancellationToken cancellationToken) =>
        dbContext.Accounts.AsNoTracking().AnyAsync(account => account.Email == normalizedEmail, cancellationToken);

    public void Add(Account account) => dbContext.Accounts.Add(account);

    public async Task<bool> TrySaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: AccountConfiguration.EmailIndexName
        })
        {
            dbContext.ChangeTracker.Clear();
            return false;
        }
    }
}
