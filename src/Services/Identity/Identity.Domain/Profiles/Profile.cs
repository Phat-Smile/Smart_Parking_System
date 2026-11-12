namespace ParkingSystem.Identity.Domain.Profiles;

public sealed class Profile
{
    public const int MaxFullNameLength = 100;

    private Profile()
    {
    }

    public Guid AccountId { get; private set; }
    public string FullName { get; private set; } = null!;
    public DateOnly? DateOfBirth { get; private set; }
    public string? CitizenIdNumber { get; private set; }
    public string? DriverLicenseNumber { get; private set; }
    public string? AvatarUrl { get; private set; }
    public string? Address { get; private set; }
    public string? IdentityDocumentType { get; private set; }
    public DateOnly? IdentityIssueDate { get; private set; }
    public string? IdentityIssuedBy { get; private set; }
    public Guid? IdentityFrontMediaId { get; private set; }
    public Guid? IdentityBackMediaId { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset? UpdatedAtUtc { get; private set; }

    public void Update(
        string fullName,
        DateTimeOffset updatedAtUtc,
        DateOnly? dateOfBirth = null,
        string? citizenIdNumber = null,
        string? driverLicenseNumber = null,
        string? avatarUrl = null,
        string? address = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fullName);
        var trimmedName = fullName.Trim();
        if (trimmedName.Length > MaxFullNameLength)
            throw new ArgumentException($"FullName must not exceed {MaxFullNameLength} characters.", nameof(fullName));

        FullName = trimmedName;
        DateOfBirth = dateOfBirth;
        CitizenIdNumber = NullIfBlank(citizenIdNumber);
        DriverLicenseNumber = NullIfBlank(driverLicenseNumber);
        AvatarUrl = NullIfBlank(avatarUrl);
        Address = NullIfBlank(address);
        UpdatedAtUtc = updatedAtUtc;
    }

    public void ClearIdentity(DateTimeOffset updatedAtUtc)
    {
        CitizenIdNumber = null;
        DriverLicenseNumber = null;
        IdentityDocumentType = null;
        IdentityIssueDate = null;
        IdentityIssuedBy = null;
        IdentityFrontMediaId = null;
        IdentityBackMediaId = null;
        UpdatedAtUtc = updatedAtUtc;
    }

    public void SetIdentityDocument(string documentType, DateOnly issueDate, string issuedBy,
        Guid frontMediaId, Guid backMediaId, DateTimeOffset updatedAtUtc)
    {
        IdentityDocumentType = documentType;
        IdentityIssueDate = issueDate;
        IdentityIssuedBy = issuedBy.Trim();
        IdentityFrontMediaId = frontMediaId;
        IdentityBackMediaId = backMediaId;
        UpdatedAtUtc = updatedAtUtc;
    }

    public static Profile Create(
        Guid accountId,
        string fullName,
        DateTimeOffset createdAtUtc,
        DateOnly? dateOfBirth = null,
        string? citizenIdNumber = null,
        string? driverLicenseNumber = null,
        string? avatarUrl = null,
        string? address = null)
    {
        if (accountId == Guid.Empty)
            throw new ArgumentException("AccountId must not be empty.", nameof(accountId));
        ArgumentException.ThrowIfNullOrWhiteSpace(fullName);

        var trimmedName = fullName.Trim();
        if (trimmedName.Length > MaxFullNameLength)
            throw new ArgumentException($"FullName must not exceed {MaxFullNameLength} characters.", nameof(fullName));

        return new Profile
        {
            AccountId = accountId,
            FullName = trimmedName,
            DateOfBirth = dateOfBirth,
            CitizenIdNumber = NullIfBlank(citizenIdNumber),
            DriverLicenseNumber = NullIfBlank(driverLicenseNumber),
            AvatarUrl = NullIfBlank(avatarUrl),
            Address = NullIfBlank(address),
            CreatedAtUtc = createdAtUtc,
            UpdatedAtUtc = null
        };
    }

    // A blank optional field means "not provided": it is stored as null so the field reads as removed.
    private static string? NullIfBlank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
