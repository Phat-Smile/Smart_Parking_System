namespace ParkingSystem.Identity.Domain.Accounts;

public sealed class Account
{
    private Account()
    {
    }

    public Guid Id { get; private set; }
    public string Email { get; private set; } = null!;
    public string PasswordHash { get; private set; } = null!;
    public string? PhoneNumber { get; private set; }
    public AccountStatus Status { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }

    public static Account Register(Guid id, string email, string passwordHash, string? phoneNumber, DateTimeOffset createdAtUtc)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("Account id must not be empty.", nameof(id));
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        ArgumentException.ThrowIfNullOrWhiteSpace(passwordHash);

        return new Account
        {
            Id = id,
            Email = NormalizeEmail(email),
            PasswordHash = passwordHash,
            PhoneNumber = NormalizePhoneNumber(phoneNumber),
            Status = AccountStatus.PendingEmailVerification,
            CreatedAtUtc = createdAtUtc
        };
    }

    // Email uniqueness is case-insensitive, so the stored form is trimmed lower case.
    public static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

    public bool VerifyEmail()
    {
        if (Status != AccountStatus.PendingEmailVerification)
            return false;

        Status = AccountStatus.EmailVerified;
        return true;
    }

    // 0901234567 and +84901234567 are the same number, so both are stored as +84901234567.
    public static string? NormalizePhoneNumber(string? phoneNumber)
    {
        if (string.IsNullOrWhiteSpace(phoneNumber))
            return null;

        var trimmed = phoneNumber.Trim();
        return trimmed.StartsWith('0') ? "+84" + trimmed[1..] : trimmed;
    }
}
