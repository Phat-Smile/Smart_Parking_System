namespace ParkingSystem.Contracts.Events.Identity;

// Outbox type: identity.owner-application-rejected.v1 — consumed by Notification to explain rejection reasons.
public sealed record OwnerApplicationRejectedIntegrationEvent(
    Guid AccountId,
    string Email,
    string Reason,
    DateTimeOffset RejectedAtUtc);
