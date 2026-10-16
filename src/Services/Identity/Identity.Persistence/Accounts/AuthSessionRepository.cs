using Microsoft.EntityFrameworkCore;
using ParkingSystem.Identity.Application.Accounts;
using ParkingSystem.Identity.Domain.Accounts;

namespace ParkingSystem.Identity.Persistence.Accounts;

internal sealed class AuthSessionRepository(IdentityDbContext dbContext) : IAuthSessionRepository
{
    public Task<IReadOnlyList<AuthSession>> ListActiveForAccountAsync(
        Guid accountId,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken) =>
        dbContext.AuthSessions
            .AsNoTracking()
            .Where(s => s.AccountId == accountId && s.RevokedAtUtc == null && s.AbsoluteExpiresAtUtc > nowUtc)
            .OrderByDescending(s => s.LastActivityAtUtc)
            .ToListAsync(cancellationToken)
            .ContinueWith<IReadOnlyList<AuthSession>>(t => t.Result, cancellationToken);

    public void Add(AuthSession session) => dbContext.AuthSessions.Add(session);

    public Task<AuthSession?> FindByIdAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.AuthSessions.SingleOrDefaultAsync(s => s.Id == id, cancellationToken);

    public Task<AuthSession?> FindByIdForUpdateAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.AuthSessions
            .FromSqlInterpolated($"""
                SELECT * FROM "identity"."auth_sessions"
                WHERE "id" = {id}
                FOR UPDATE
                """)
            .SingleOrDefaultAsync(cancellationToken);

    public async Task RevokeAllForAccountAsync(Guid accountId, DateTimeOffset nowUtc, string reason, CancellationToken cancellationToken)
    {
        await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE "identity"."auth_sessions"
            SET "revoked_at_utc" = {nowUtc}, "revoked_reason" = {reason}
            WHERE "account_id" = {accountId} AND "revoked_at_utc" IS NULL
            """, cancellationToken);
    }
}
