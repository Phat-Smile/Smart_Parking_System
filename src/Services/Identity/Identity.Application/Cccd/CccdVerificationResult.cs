namespace ParkingSystem.Identity.Application.Cccd;

public sealed record CccdVerificationResult(
    bool IsValid,
    string? FullName,
    DateOnly? DateOfBirth,
    string? Gender);
