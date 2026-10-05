namespace ParkingSystem.Identity.Domain.EmailVerifications;

public sealed class EmailVerification
{
    public const int OtpLength = 6;
    public const int ExpirationMinutes = 5;
    public const int MaxAttempts = 3;
    public const int CooldownSeconds = 60;
    public const int MaxHourlyResends = 5;

    private EmailVerification()
    {
    }

    public Guid Id { get; private set; }
    public Guid AccountId { get; private set; }
    public string Email { get; private set; } = null!;
    public string OtpCode { get; private set; } = null!;
    public DateTimeOffset ExpiresAtUtc { get; private set; }
    public int AttemptCount { get; private set; }
    public bool IsUsed { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }

    public static EmailVerification Issue(
        Guid id,
        Guid accountId,
        string email,
        string otpCode,
        DateTimeOffset createdAtUtc)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("Id must not be empty.", nameof(id));
        if (accountId == Guid.Empty)
            throw new ArgumentException("AccountId must not be empty.", nameof(accountId));
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        ArgumentException.ThrowIfNullOrWhiteSpace(otpCode);

        return new EmailVerification
        {
            Id = id,
            AccountId = accountId,
            Email = email.Trim().ToLowerInvariant(),
            OtpCode = otpCode.Trim(),
            ExpiresAtUtc = createdAtUtc.AddMinutes(ExpirationMinutes),
            AttemptCount = 0,
            IsUsed = false,
            CreatedAtUtc = createdAtUtc
        };
    }

    public bool IsExpired(DateTimeOffset nowUtc) => nowUtc >= ExpiresAtUtc;

    public bool CanAttempt() => !IsUsed && AttemptCount < MaxAttempts;

    public bool RecordAttempt(string inputOtp, DateTimeOffset nowUtc)
    {
        if (IsUsed || IsExpired(nowUtc) || AttemptCount >= MaxAttempts)
            return false;

        AttemptCount++;

        if (string.Equals(OtpCode, inputOtp.Trim(), StringComparison.Ordinal))
        {
            IsUsed = true;
            return true;
        }

        return false;
    }

    public void Invalidate()
    {
        IsUsed = true;
    }
}
