namespace ParkingSystem.Contracts.Events.Identity;

// Outbox type: identity.owner-application-approved.v1 — consumed by Notification to notify the applicant.
public sealed record OwnerApplicationApprovedIntegrationEvent(
    Guid AccountId,
    string Email,
    DateTimeOffset ApprovedAtUtc);
