namespace ParkingSystem.Contracts.Events.Identity;

// Outbox type: identity.account-email-verified.v1 — no consumer yet.
public sealed record AccountEmailVerifiedIntegrationEvent(
    Guid AccountId,
    string Email,
    DateTimeOffset VerifiedAtUtc);
