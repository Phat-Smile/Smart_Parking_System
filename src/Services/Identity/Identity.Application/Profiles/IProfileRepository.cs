using ParkingSystem.Identity.Domain.Profiles;

namespace ParkingSystem.Identity.Application.Profiles;

public interface IProfileRepository
{
    void Add(Profile profile);

    Task<Profile?> FindByAccountIdAsync(Guid accountId, CancellationToken cancellationToken);

    Task<IReadOnlyDictionary<Guid, string>> FindFullNamesByAccountIdsAsync(IReadOnlyList<Guid> accountIds, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyDictionary<Guid, string>>(new Dictionary<Guid, string>());

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
