using RefreshToken = ParkingSystem.Identity.Domain.Accounts.RefreshToken;

namespace ParkingSystem.Identity.Application.Accounts;

public interface IRefreshTokenRepository
{
    void Add(RefreshToken refreshToken);

    Task<RefreshToken?> FindByHashAsync(string tokenHash, CancellationToken cancellationToken);

    Task<RefreshToken?> FindLatestBySessionIdAsync(Guid sessionId, CancellationToken cancellationToken);

    Task RevokeActiveTokensBySessionIdAsync(Guid sessionId, DateTimeOffset nowUtc, string reason, CancellationToken cancellationToken);

    Task RevokeAllForAccountAsync(Guid accountId, DateTimeOffset nowUtc, string reason, CancellationToken cancellationToken);
}
