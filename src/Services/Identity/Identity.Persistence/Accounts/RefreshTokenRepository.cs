using Microsoft.EntityFrameworkCore;
using ParkingSystem.Identity.Application.Accounts;
using ParkingSystem.Identity.Domain.Accounts;

namespace ParkingSystem.Identity.Persistence.Accounts;

internal sealed class RefreshTokenRepository(IdentityDbContext dbContext) : IRefreshTokenRepository
{
    public void Add(RefreshToken refreshToken) =>
        dbContext.RefreshTokens.Add(refreshToken);

    public Task<RefreshToken?> FindByHashAsync(string tokenHash, CancellationToken cancellationToken) =>
        dbContext.RefreshTokens.SingleOrDefaultAsync(rt => rt.TokenHash == tokenHash, cancellationToken);

    public Task<RefreshToken?> FindLatestBySessionIdAsync(Guid sessionId, CancellationToken cancellationToken) =>
        dbContext.RefreshTokens
            .Where(rt => rt.SessionId == sessionId && rt.RevokedAtUtc == null)
            .OrderByDescending(rt => rt.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task RevokeActiveTokensBySessionIdAsync(Guid sessionId, DateTimeOffset nowUtc, string reason, CancellationToken cancellationToken)
    {
        await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE "identity"."refresh_tokens"
            SET "revoked_at_utc" = {nowUtc}, "revoked_reason" = {reason}
            WHERE "session_id" = {sessionId} AND "revoked_at_utc" IS NULL
            """, cancellationToken);
    }
}
