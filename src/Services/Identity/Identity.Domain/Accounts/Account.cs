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
            PhoneNumber = string.IsNullOrWhiteSpace(phoneNumber) ? null : phoneNumber.Trim(),
            Status = AccountStatus.PendingEmailVerification,
            CreatedAtUtc = createdAtUtc
        };
    }

    // Email uniqueness is case-insensitive, so the stored form is trimmed lower case.
    public static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();
}
