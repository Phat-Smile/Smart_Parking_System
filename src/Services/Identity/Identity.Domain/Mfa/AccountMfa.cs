namespace ParkingSystem.Identity.Domain.Mfa;

// US-AUTH-007: TOTP của một tài khoản. EnabledAtUtc null = đang cài (đã cấp secret, chưa nhập đúng mã lần đầu).
public sealed class AccountMfa
{
    private AccountMfa()
    {
    }

    public Guid AccountId { get; private set; }

    // Secret đã mã hoá; Domain không bao giờ thấy secret gốc.
    public string SecretProtected { get; private set; } = null!;
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset? EnabledAtUtc { get; private set; }

    // Bước thời gian (30 s) cuối cùng đã dùng; mã của bước này hoặc cũ hơn không dùng lại được.
    public long LastUsedStep { get; private set; }

    public bool IsEnabled => EnabledAtUtc.HasValue;

    public static AccountMfa StartSetup(Guid accountId, string secretProtected, DateTimeOffset nowUtc)
    {
        if (accountId == Guid.Empty)
            throw new ArgumentException("AccountId must not be empty.", nameof(accountId));
        ArgumentException.ThrowIfNullOrWhiteSpace(secretProtected);

        return new AccountMfa { AccountId = accountId, SecretProtected = secretProtected, CreatedAtUtc = nowUtc };
    }

    // Cài lại khi chưa bật (người dùng quét QR chưa xong rồi bấm tạo mã mới).
    public void RestartSetup(string secretProtected, DateTimeOffset nowUtc)
    {
        if (IsEnabled)
            throw new InvalidOperationException("MFA is already enabled.");
        ArgumentException.ThrowIfNullOrWhiteSpace(secretProtected);

        SecretProtected = secretProtected;
        CreatedAtUtc = nowUtc;
        LastUsedStep = 0;
    }

    public void Enable(long usedStep, DateTimeOffset nowUtc)
    {
        EnabledAtUtc = nowUtc;
        LastUsedStep = usedStep;
    }

    public bool TryUseStep(long step)
    {
        if (step <= LastUsedStep)
            return false;

        LastUsedStep = step;
        return true;
    }
}

public sealed class MfaRecoveryCode
{
    public const int CodesPerSet = 10;

    private MfaRecoveryCode()
    {
    }

    public Guid Id { get; private set; }
    public Guid AccountId { get; private set; }
    public string CodeHash { get; private set; } = null!;
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset? UsedAtUtc { get; private set; }

    public static MfaRecoveryCode Create(Guid accountId, string codeHash, DateTimeOffset nowUtc) =>
        new() { Id = Guid.NewGuid(), AccountId = accountId, CodeHash = codeHash, CreatedAtUtc = nowUtc };

    public void Use(DateTimeOffset nowUtc) => UsedAtUtc = nowUtc;
}

// Bước 2 của đăng nhập: mật khẩu (hoặc Google) đã đúng, chờ mã TOTP. Token gốc chỉ trả cho client, DB giữ hash.
public sealed class MfaChallenge
{
    public const int LifetimeMinutes = 5;
    public const int MaxAttempts = 3;

    private MfaChallenge()
    {
    }

    public Guid Id { get; private set; }
    public Guid AccountId { get; private set; }
    public string TokenHash { get; private set; } = null!;
    public string AuthenticationMethod { get; private set; } = null!;
    public string? DeviceInfo { get; private set; }
    public string? IpAddress { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset ExpiresAtUtc { get; private set; }
    public DateTimeOffset? UsedAtUtc { get; private set; }
    public int AttemptCount { get; private set; }

    public static MfaChallenge Issue(Guid accountId, string tokenHash, string authenticationMethod, string? deviceInfo, string? ipAddress, DateTimeOffset nowUtc) =>
        new()
        {
            Id = Guid.NewGuid(),
            AccountId = accountId,
            TokenHash = tokenHash,
            AuthenticationMethod = authenticationMethod,
            DeviceInfo = deviceInfo,
            IpAddress = ipAddress,
            CreatedAtUtc = nowUtc,
            ExpiresAtUtc = nowUtc.AddMinutes(LifetimeMinutes)
        };

    public bool IsUsable(DateTimeOffset nowUtc) => UsedAtUtc is null && nowUtc < ExpiresAtUtc && AttemptCount < MaxAttempts;

    public void RecordFailedAttempt() => AttemptCount++;

    public void Complete(DateTimeOffset nowUtc) => UsedAtUtc = nowUtc;
}
