using ParkingSystem.BuildingBlocks.Time;

namespace ParkingSystem.Identity.Application.Accounts;

public sealed class AuthSessionManager(
    IAuthSessionRepository authSessions,
    IRefreshTokenRepository refreshTokens,
    IAccountRepository accounts,
    IClock clock) : IAuthSessionManager
{
    public async Task RevokeSessionAsync(Guid sessionId, string reason, CancellationToken cancellationToken = default)
    {
        var now = clock.UtcNow;
        var session = await authSessions.FindByIdForUpdateAsync(sessionId, cancellationToken);
        if (session is not null && !session.IsRevoked)
        {
            session.Revoke(now, reason);
            await refreshTokens.RevokeActiveTokensBySessionIdAsync(sessionId, now, reason, cancellationToken);
            await accounts.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task RevokeAllSessionsAsync(Guid accountId, string reason, CancellationToken cancellationToken = default)
    {
        var now = clock.UtcNow;
        await authSessions.RevokeAllForAccountAsync(accountId, now, reason, cancellationToken);
    }
}
