namespace ParkingSystem.Identity.Domain.Accounts;

public sealed class AuthSession
{
    public const int DefaultAbsoluteLifetimeDays = 30;
    public const int MaxDeviceInfoLength = 512;
    public const int MaxIpAddressLength = 64;

    private AuthSession()
    {
    }

    public Guid Id { get; private set; }
    public Guid AccountId { get; private set; }
    public string? DeviceInfo { get; private set; }
    public string? IpAddress { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset LastActivityAtUtc { get; private set; }
    public DateTimeOffset AbsoluteExpiresAtUtc { get; private set; }
    public DateTimeOffset? RevokedAtUtc { get; private set; }
    public string? RevokedReason { get; private set; }

    public static AuthSession Create(
        Guid id,
        Guid accountId,
        string? deviceInfo,
        string? ipAddress,
        DateTimeOffset createdAtUtc,
        DateTimeOffset absoluteExpiresAtUtc)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("Session Id must not be empty.", nameof(id));
        if (accountId == Guid.Empty)
            throw new ArgumentException("AccountId must not be empty.", nameof(accountId));
        if (absoluteExpiresAtUtc <= createdAtUtc)
            throw new ArgumentException("AbsoluteExpiresAtUtc must be greater than CreatedAtUtc.", nameof(absoluteExpiresAtUtc));

        return new AuthSession
        {
            Id = id,
            AccountId = accountId,
            DeviceInfo = Truncate(deviceInfo, MaxDeviceInfoLength),
            IpAddress = Truncate(ipAddress, MaxIpAddressLength),
            CreatedAtUtc = createdAtUtc,
            LastActivityAtUtc = createdAtUtc,
            AbsoluteExpiresAtUtc = absoluteExpiresAtUtc,
            RevokedAtUtc = null,
            RevokedReason = null
        };
    }

    // Client-supplied (User-Agent, remote IP): cut to the column length instead of failing the login.
    private static string? Truncate(string? value, int maxLength)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed))
            return null;

        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }

    public bool IsRevoked => RevokedAtUtc.HasValue;

    public bool IsExpired(DateTimeOffset nowUtc) => nowUtc >= AbsoluteExpiresAtUtc;

    public bool IsActive(DateTimeOffset nowUtc) => !IsRevoked && !IsExpired(nowUtc);

    public void RecordActivity(DateTimeOffset nowUtc)
    {
        LastActivityAtUtc = nowUtc;
    }

    public void Revoke(DateTimeOffset nowUtc, string reason)
    {
        if (IsRevoked)
            return;

        RevokedAtUtc = nowUtc;
        RevokedReason = reason.Trim();
    }

    public DateTimeOffset CalculateNextTokenExpiration(DateTimeOffset nowUtc, int idleDays = 7)
    {
        var idleExpiresAt = nowUtc.AddDays(idleDays);
        return idleExpiresAt < AbsoluteExpiresAtUtc ? idleExpiresAt : AbsoluteExpiresAtUtc;
    }
}
