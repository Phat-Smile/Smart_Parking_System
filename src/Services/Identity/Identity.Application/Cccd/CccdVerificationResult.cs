namespace ParkingSystem.Identity.Application.Cccd;

// Unavailable means "could not check" (provider down, timeout, unreadable answer); it must never be shown as "your CCCD is wrong".
public enum CccdVerificationStatus
{
    Valid,
    Invalid,
    Unavailable
}

public sealed record CccdVerificationResult(
    CccdVerificationStatus Status,
    string? FullName,
    DateOnly? DateOfBirth,
    string? Gender)
{
    public bool IsValid => Status == CccdVerificationStatus.Valid;

    public static CccdVerificationResult Valid(string? fullName, DateOnly? dateOfBirth, string? gender) =>
        new(CccdVerificationStatus.Valid, fullName, dateOfBirth, gender);

    public static CccdVerificationResult Invalid { get; } = new(CccdVerificationStatus.Invalid, null, null, null);

    public static CccdVerificationResult Unavailable { get; } = new(CccdVerificationStatus.Unavailable, null, null, null);
}
