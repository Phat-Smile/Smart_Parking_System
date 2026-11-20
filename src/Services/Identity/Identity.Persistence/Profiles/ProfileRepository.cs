using Microsoft.EntityFrameworkCore;
using ParkingSystem.Identity.Application.Profiles;
using ParkingSystem.Identity.Domain.Profiles;

namespace ParkingSystem.Identity.Persistence.Profiles;

internal sealed class ProfileRepository(IdentityDbContext dbContext) : IProfileRepository
{
    public void Add(Profile profile)
    {
        dbContext.Profiles.Add(profile);
    }

    public Task<Profile?> FindByAccountIdAsync(Guid accountId, CancellationToken cancellationToken)
    {
        return dbContext.Profiles.FirstOrDefaultAsync(p => p.AccountId == accountId, cancellationToken);
    }

    public async Task<IReadOnlyDictionary<Guid, string>> FindFullNamesByAccountIdsAsync(IReadOnlyList<Guid> accountIds, CancellationToken cancellationToken)
    {
        if (accountIds.Count == 0) return new Dictionary<Guid, string>();
        return await dbContext.Profiles.AsNoTracking()
            .Where(p => accountIds.Contains(p.AccountId))
            .ToDictionaryAsync(p => p.AccountId, p => p.FullName, cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        dbContext.SaveChangesAsync(cancellationToken);
}
