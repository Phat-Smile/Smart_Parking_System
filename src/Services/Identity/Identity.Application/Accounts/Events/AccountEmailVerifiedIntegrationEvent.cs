namespace ParkingSystem.Identity.Application.Accounts.Events;

public sealed record AccountEmailVerifiedIntegrationEvent(
    Guid AccountId,
    string Email,
    DateTimeOffset VerifiedAtUtc);
