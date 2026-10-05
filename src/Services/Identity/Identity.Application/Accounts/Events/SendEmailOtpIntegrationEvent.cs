namespace ParkingSystem.Identity.Application.Accounts.Events;

public sealed record SendEmailOtpIntegrationEvent(
    Guid AccountId,
    string Email,
    string OtpCode,
    DateTimeOffset ExpiresAtUtc);
