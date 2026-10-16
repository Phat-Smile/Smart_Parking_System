using ParkingSystem.Identity.Domain.Accounts;

namespace ParkingSystem.Identity.Application.Accounts;

public interface IAuthSessionRepository
{
    void Add(AuthSession session);

    Task<AuthSession?> FindByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<AuthSession?> FindByIdForUpdateAsync(Guid id, CancellationToken cancellationToken);

    Task RevokeAllForAccountAsync(Guid accountId, DateTimeOffset nowUtc, string reason, CancellationToken cancellationToken);
}
