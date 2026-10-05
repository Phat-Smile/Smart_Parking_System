using System.Security.Cryptography;
using System.Text;

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
    public string OtpHash { get; private set; } = null!;
    public DateTimeOffset ExpiresAtUtc { get; private set; }
    public int AttemptCount { get; private set; }
    public bool IsUsed { get; private set; }
    public DateTimeOffset? InvalidatedAtUtc { get; private set; }
    public bool IsInvalidated => InvalidatedAtUtc.HasValue;
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public int RowVersion { get; private set; } = 1;

    public static string HashOtp(string otpCode, Guid accountId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(otpCode);
        if (accountId == Guid.Empty)
            throw new ArgumentException("AccountId must not be empty.", nameof(accountId));

        var raw = $"{accountId:D}:{otpCode.Trim()}";
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(bytes);
    }

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
            OtpHash = HashOtp(otpCode, accountId),
            ExpiresAtUtc = createdAtUtc.AddMinutes(ExpirationMinutes),
            AttemptCount = 0,
            IsUsed = false,
            InvalidatedAtUtc = null,
            CreatedAtUtc = createdAtUtc,
            RowVersion = 1
        };
    }

    public bool IsExpired(DateTimeOffset nowUtc) => nowUtc >= ExpiresAtUtc;

    public bool CanAttempt() => !IsUsed && !IsInvalidated && AttemptCount < MaxAttempts;

    public bool RecordAttempt(string inputOtp, DateTimeOffset nowUtc)
    {
        if (IsUsed || IsInvalidated || IsExpired(nowUtc) || AttemptCount >= MaxAttempts)
            return false;

        AttemptCount++;
        RowVersion++;

        if (string.IsNullOrWhiteSpace(inputOtp))
            return false;

        var inputHash = HashOtp(inputOtp, AccountId);
        var expectedBytes = Encoding.UTF8.GetBytes(OtpHash);
        var actualBytes = Encoding.UTF8.GetBytes(inputHash);

        if (CryptographicOperations.FixedTimeEquals(expectedBytes, actualBytes))
        {
            IsUsed = true;
            return true;
        }

        return false;
    }

    public void Invalidate(DateTimeOffset nowUtc)
    {
        if (IsInvalidated)
            return;

        InvalidatedAtUtc = nowUtc;
        RowVersion++;
    }
}

