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

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        dbContext.SaveChangesAsync(cancellationToken);
}
