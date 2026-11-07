namespace ParkingSystem.Contracts.Events.Parking;

/// <summary>
/// parking.slot-needs-verification.v1: published through the Outbox (US-PARK-005, BR-04.e).
/// Stage is Staff (10 minutes without a parking confirmation) or Owner (30 minutes in NeedsVerification).
/// </summary>
public sealed record ParkingSlotNeedsVerificationIntegrationEvent(
    Guid LotId,
    Guid SlotId,
    string SlotCode,
    Guid? SessionId,
    Guid? IncidentId,
    string Stage,
    DateTimeOffset OccurredAtUtc);
