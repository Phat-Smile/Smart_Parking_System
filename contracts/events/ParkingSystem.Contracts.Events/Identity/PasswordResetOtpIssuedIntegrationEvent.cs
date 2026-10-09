namespace ParkingSystem.Contracts.Events.Identity;

// Outbox type: identity.password-reset-otp-issued.v1 — consumed by Notification to send the password reset code.
// OtpCode is removed from the outbox payload once dispatched (Outbox:RedactedPayloadKeys).
public sealed record PasswordResetOtpIssuedIntegrationEvent(
    Guid AccountId,
    string Email,
    string OtpCode,
    DateTimeOffset ExpiresAtUtc);
