using ParkingSystem.Identity.Domain.Profiles;

namespace ParkingSystem.Identity.Application.Profiles;

public interface IProfileRepository
{
    void Add(Profile profile);

    Task<Profile?> FindByAccountIdAsync(Guid accountId, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
