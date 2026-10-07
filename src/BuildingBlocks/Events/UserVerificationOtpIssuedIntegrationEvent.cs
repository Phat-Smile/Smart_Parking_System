namespace ParkingSystem.BuildingBlocks.Events;

public sealed record UserVerificationOtpIssuedIntegrationEvent(
    Guid AccountId,
    string Email,
    string OtpCode,
    DateTimeOffset ExpiresAtUtc);
