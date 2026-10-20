namespace ParkingSystem.Contracts.Events.Identity;

// Outbox type: identity.user-verification-otp-issued.v1 — consumed by Notification to send the OTP email.
public sealed record UserVerificationOtpIssuedIntegrationEvent(
    Guid AccountId,
    string Email,
    string OtpCode,
    DateTimeOffset ExpiresAtUtc);
