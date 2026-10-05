namespace ParkingSystem.Identity.Application.Accounts.Events;

[Obsolete("Use UserVerificationOtpIssuedIntegrationEvent instead.")]
public sealed record SendEmailOtpIntegrationEvent(
    Guid AccountId,
    string Email,
    string OtpCode,
    DateTimeOffset ExpiresAtUtc);
