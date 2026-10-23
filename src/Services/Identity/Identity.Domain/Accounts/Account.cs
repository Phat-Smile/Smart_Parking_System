namespace ParkingSystem.Identity.Domain.Accounts;

public sealed class Account
{
    public const int MaxFailedAttemptsBeforeLockout = 3;
    public const int LockoutDurationMinutes = 15;

    private Account()
    {
    }

    public Guid Id { get; private set; }
    public string Email { get; private set; } = null!;
    // Null for an account created with Google until a password is set through forgot / reset password.
    public string? PasswordHash { get; private set; }
    public string? GoogleSubject { get; private set; }
    public bool HasPassword => !string.IsNullOrEmpty(PasswordHash);
    public string? PhoneNumber { get; private set; }
    public AccountRole Role { get; private set; } = AccountRole.Customer;
    public AccountStatus Status { get; private set; }
    public OwnerApplicationStatus? OwnerApplicationStatus { get; private set; }
    public string? OwnerTaxCode { get; private set; }
    public string? OwnerBusinessName { get; private set; }
    public OwnerType? OwnerType { get; private set; }
    public string? OwnerRejectionReason { get; private set; }
    public DateTimeOffset? OwnerReviewedAtUtc { get; private set; }
    public Guid? OwnerReviewedBy { get; private set; }
    public int FailedLoginAttempts { get; private set; }
    public DateTimeOffset? LockoutEndUtc { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public int RowVersion { get; private set; } = 1;

    public static Account Register(
        Guid id,
        string email,
        string passwordHash,
        string? phoneNumber,
        DateTimeOffset createdAtUtc,
        bool applyAsParkingLotOwner = false,
        string? taxCode = null,
        string? businessName = null)
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
            Role = AccountRole.Customer,
            OwnerApplicationStatus = applyAsParkingLotOwner ? Domain.Accounts.OwnerApplicationStatus.Pending : null,
            OwnerTaxCode = applyAsParkingLotOwner ? NullIfBlank(taxCode) : null,
            OwnerBusinessName = applyAsParkingLotOwner ? NullIfBlank(businessName) : null,
            Status = AccountStatus.PendingEmailVerification,
            FailedLoginAttempts = 0,
            LockoutEndUtc = null,
            CreatedAtUtc = createdAtUtc,
            RowVersion = 1
        };
    }

    // US-AUTH-002 (G2, G3, G4): Google already verified the email; the account is a customer without a password.
    public static Account RegisterWithGoogle(Guid id, string email, string googleSubject, DateTimeOffset createdAtUtc)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("Account id must not be empty.", nameof(id));
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        ArgumentException.ThrowIfNullOrWhiteSpace(googleSubject);

        return new Account
        {
            Id = id,
            Email = NormalizeEmail(email),
            PasswordHash = null,
            GoogleSubject = googleSubject,
            Role = AccountRole.Customer,
            Status = AccountStatus.EmailVerified,
            CreatedAtUtc = createdAtUtc,
            RowVersion = 1
        };
    }

    // US-AUTH-002 (G2): an existing account signs in with Google; a pending account becomes verified.
    public void LinkGoogle(string googleSubject)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(googleSubject);
        if (GoogleSubject is not null && GoogleSubject != googleSubject)
            throw new InvalidOperationException("The account is linked to another Google account.");

        GoogleSubject = googleSubject;
        if (Status == AccountStatus.PendingEmailVerification)
            Status = AccountStatus.EmailVerified;
        RowVersion++;
    }

    public void AssignRole(AccountRole role)
    {
        Role = role;
        if (role == AccountRole.ParkingLotOwner)
            OwnerApplicationStatus = Domain.Accounts.OwnerApplicationStatus.Approved;
        else if (OwnerApplicationStatus == Domain.Accounts.OwnerApplicationStatus.Approved)
            OwnerApplicationStatus = null;

        RowVersion++;
    }

    public void ApproveOwnerApplication(Guid reviewerId, DateTimeOffset reviewedAtUtc)
    {
        if (OwnerApplicationStatus != Domain.Accounts.OwnerApplicationStatus.Pending)
            throw new InvalidOperationException("Only pending owner applications can be approved.");
        if (reviewerId == Guid.Empty)
            throw new ArgumentException("Reviewer id must not be empty.", nameof(reviewerId));
        if (reviewerId == Id)
            throw new InvalidOperationException("An admin cannot approve their own owner application.");

        OwnerApplicationStatus = Domain.Accounts.OwnerApplicationStatus.Approved;
        Role = AccountRole.ParkingLotOwner;
        OwnerReviewedBy = reviewerId;
        OwnerReviewedAtUtc = reviewedAtUtc;
        OwnerRejectionReason = null;
        RowVersion++;
    }

    public void RejectOwnerApplication(Guid reviewerId, string reason, DateTimeOffset reviewedAtUtc)
    {
        if (OwnerApplicationStatus != Domain.Accounts.OwnerApplicationStatus.Pending)
            throw new InvalidOperationException("Only pending owner applications can be rejected.");
        if (reviewerId == Guid.Empty)
            throw new ArgumentException("Reviewer id must not be empty.", nameof(reviewerId));
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        var trimmedReason = reason.Trim();
        if (trimmedReason.Length > 500)
            throw new ArgumentException("Rejection reason must not exceed 500 characters.", nameof(reason));

        OwnerApplicationStatus = Domain.Accounts.OwnerApplicationStatus.Rejected;
        Role = AccountRole.Customer;
        OwnerReviewedBy = reviewerId;
        OwnerReviewedAtUtc = reviewedAtUtc;
        OwnerRejectionReason = trimmedReason;
        RowVersion++;
    }

    public void SetOwnerType(OwnerType ownerType)
    {
        if (OwnerApplicationStatus == null)
            throw new InvalidOperationException("Account does not have an owner application.");
        if (OwnerApplicationStatus == Domain.Accounts.OwnerApplicationStatus.Approved)
            throw new InvalidOperationException("Approved owner applications cannot change owner type.");

        OwnerType = ownerType;
        RowVersion++;
    }

    public void ApplyForParkingLotOwner(
        DateTimeOffset appliedAtUtc,
        string? taxCode = null,
        string? businessName = null,
        OwnerType? ownerType = null)
    {
        if (Role == AccountRole.ParkingLotOwner || OwnerApplicationStatus == Domain.Accounts.OwnerApplicationStatus.Approved)
            throw new InvalidOperationException("Account is already a parking lot owner.");
        if (OwnerApplicationStatus == Domain.Accounts.OwnerApplicationStatus.Pending)
            throw new InvalidOperationException("An owner application is already pending review.");

        OwnerApplicationStatus = Domain.Accounts.OwnerApplicationStatus.Pending;
        OwnerTaxCode = NullIfBlank(taxCode);
        OwnerBusinessName = NullIfBlank(businessName);
        if (ownerType.HasValue)
            OwnerType = ownerType.Value;
        OwnerRejectionReason = null;
        OwnerReviewedBy = null;
        OwnerReviewedAtUtc = null;
        RowVersion++;
    }

    public void ResubmitOwnerApplication(
        DateTimeOffset resubmittedAtUtc,
        string? taxCode = null,
        string? businessName = null,
        OwnerType? ownerType = null)
    {
        if (OwnerApplicationStatus != Domain.Accounts.OwnerApplicationStatus.Rejected)
            throw new InvalidOperationException("Only rejected owner applications can be resubmitted.");

        OwnerApplicationStatus = Domain.Accounts.OwnerApplicationStatus.Pending;
        if (!string.IsNullOrWhiteSpace(taxCode))
            OwnerTaxCode = NullIfBlank(taxCode);
        if (!string.IsNullOrWhiteSpace(businessName))
            OwnerBusinessName = NullIfBlank(businessName);
        if (ownerType.HasValue)
            OwnerType = ownerType.Value;

        OwnerRejectionReason = null;
        OwnerReviewedBy = null;
        OwnerReviewedAtUtc = null;
        RowVersion++;
    }

    public void UpdatePhoneNumber(string? phoneNumber)
    {
        PhoneNumber = NormalizePhoneNumber(phoneNumber);
        RowVersion++;
    }

    // Email uniqueness is case-insensitive, so the stored form is trimmed lower case.
    public static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

    public bool VerifyEmail()
    {
        if (Status != AccountStatus.PendingEmailVerification)
            return false;

        Status = AccountStatus.EmailVerified;
        RowVersion++;
        return true;
    }

    public void Suspend()
    {
        Status = AccountStatus.Suspended;
        RowVersion++;
    }

    public void Disable()
    {
        Status = AccountStatus.Disabled;
        RowVersion++;
    }

    public void Activate()
    {
        Status = AccountStatus.EmailVerified;
        RowVersion++;
    }

    public bool IsLockedOut(DateTimeOffset nowUtc) =>
        LockoutEndUtc.HasValue && LockoutEndUtc.Value > nowUtc;

    public void ClearExpiredLockout(DateTimeOffset nowUtc)
    {
        if (LockoutEndUtc.HasValue && LockoutEndUtc.Value <= nowUtc)
        {
            LockoutEndUtc = null;
            FailedLoginAttempts = 0;
            RowVersion++;
        }
    }

    public void RecordFailedLogin(DateTimeOffset nowUtc)
    {
        ClearExpiredLockout(nowUtc);
        FailedLoginAttempts++;
        RowVersion++;

        if (FailedLoginAttempts >= MaxFailedAttemptsBeforeLockout)
        {
            LockoutEndUtc = nowUtc.AddMinutes(LockoutDurationMinutes);
        }
    }

    // US-AUTH-006: the user proved they own the email, so the lockout and the failed-attempt streak are cleared.
    public void ResetPassword(string newPasswordHash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(newPasswordHash);
        PasswordHash = newPasswordHash;
        FailedLoginAttempts = 0;
        LockoutEndUtc = null;
        RowVersion++;
    }

    public void RecordCorrectCredential()
    {
        if (FailedLoginAttempts > 0 || LockoutEndUtc.HasValue)
        {
            FailedLoginAttempts = 0;
            LockoutEndUtc = null;
            RowVersion++;
        }
    }

    // 0901234567 and +84901234567 are the same number, so both are stored as +84901234567.
    public static string? NormalizePhoneNumber(string? phoneNumber)
    {
        if (string.IsNullOrWhiteSpace(phoneNumber))
            return null;

        var trimmed = phoneNumber.Trim();
        return trimmed.StartsWith('0') ? "+84" + trimmed[1..] : trimmed;
    }

    private static string? NullIfBlank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
