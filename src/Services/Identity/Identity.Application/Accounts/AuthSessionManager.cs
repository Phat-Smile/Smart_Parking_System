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
        var preliminary = await authSessions.FindByIdAsync(sessionId, cancellationToken);
        if (preliminary is null || preliminary.IsRevoked)
        {
            return;
        }

        await accounts.ExecuteInTransactionAsync(async () =>
        {
            // Global Lock Ordering: Account FOR UPDATE -> AuthSession FOR UPDATE to eliminate lock inversion
            await accounts.FindByIdForUpdateAsync(preliminary.AccountId, cancellationToken);

            var session = await authSessions.FindByIdForUpdateAsync(sessionId, cancellationToken);
            if (session is not null && !session.IsRevoked)
            {
                session.Revoke(now, reason);
                await refreshTokens.RevokeActiveTokensBySessionIdAsync(sessionId, now, reason, cancellationToken);
                await accounts.SaveChangesAsync(cancellationToken);
            }
            return true;
        }, cancellationToken);
    }

    public async Task RevokeAllSessionsAsync(Guid accountId, string reason, CancellationToken cancellationToken = default)
    {
        var now = clock.UtcNow;
        await accounts.ExecuteInTransactionAsync(async () =>
        {
            // Global Lock Ordering: Account FOR UPDATE
            await accounts.FindByIdForUpdateAsync(accountId, cancellationToken);

            await authSessions.RevokeAllForAccountAsync(accountId, now, reason, cancellationToken);
            await refreshTokens.RevokeAllForAccountAsync(accountId, now, reason, cancellationToken);
            return true;
        }, cancellationToken);
    }
}
