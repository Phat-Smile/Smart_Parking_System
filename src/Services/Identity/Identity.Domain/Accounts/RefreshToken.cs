namespace ParkingSystem.Identity.Domain.Accounts;

public sealed class RefreshToken
{
    public const int DefaultExpirationDays = 7;

    private RefreshToken()
    {
    }

    public Guid Id { get; private set; }
    public Guid SessionId { get; private set; }
    public Guid AccountId { get; private set; }
    public string TokenHash { get; private set; } = null!;
    public DateTimeOffset ExpiresAtUtc { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset? RevokedAtUtc { get; private set; }
    public string? RevokedReason { get; private set; }
    public Guid? ReplacedByTokenId { get; private set; }
    public string? ReplacedByTokenHash { get; private set; }

    public static RefreshToken Issue(
        Guid id,
        Guid sessionId,
        Guid accountId,
        string tokenHash,
        DateTimeOffset createdAtUtc,
        DateTimeOffset expiresAtUtc)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("Id must not be empty.", nameof(id));
        if (sessionId == Guid.Empty)
            throw new ArgumentException("SessionId must not be empty.", nameof(sessionId));
        if (accountId == Guid.Empty)
            throw new ArgumentException("AccountId must not be empty.", nameof(accountId));
        ArgumentException.ThrowIfNullOrWhiteSpace(tokenHash);

        if (expiresAtUtc <= createdAtUtc)
            throw new ArgumentException("ExpiresAtUtc must be greater than CreatedAtUtc.", nameof(expiresAtUtc));

        return new RefreshToken
        {
            Id = id,
            SessionId = sessionId,
            AccountId = accountId,
            TokenHash = tokenHash.Trim(),
            ExpiresAtUtc = expiresAtUtc,
            CreatedAtUtc = createdAtUtc,
            RevokedAtUtc = null,
            RevokedReason = null,
            ReplacedByTokenId = null,
            ReplacedByTokenHash = null
        };
    }

    public static RefreshToken Issue(
        Guid id,
        Guid accountId,
        string tokenHash,
        DateTimeOffset createdAtUtc,
        DateTimeOffset expiresAtUtc) =>
        Issue(id, Guid.NewGuid(), accountId, tokenHash, createdAtUtc, expiresAtUtc);

    public bool IsExpired(DateTimeOffset nowUtc) => nowUtc >= ExpiresAtUtc;

    public bool IsRevoked => RevokedAtUtc.HasValue;

    public bool IsActive(DateTimeOffset nowUtc) => !IsRevoked && !IsExpired(nowUtc);

    public void Revoke(DateTimeOffset nowUtc, string reason, Guid? replacedByTokenId = null, string? replacedByTokenHash = null)
    {
        if (IsRevoked)
            return;

        RevokedAtUtc = nowUtc;
        RevokedReason = reason.Trim();
        ReplacedByTokenId = replacedByTokenId;
        ReplacedByTokenHash = replacedByTokenHash?.Trim();
    }

    public void Revoke(DateTimeOffset nowUtc, string? replacedByTokenHash = null) =>
        Revoke(nowUtc, "Revoked", null, replacedByTokenHash);
}
