namespace ParkingSystem.Contracts.Events.Parking;

/// <summary>
/// parking.session-closed.v1: published through the Outbox when a parking session becomes Completed (US-PAY-006).
/// </summary>
public sealed record ParkingSessionClosedIntegrationEvent(
    Guid SessionId,
    Guid LotId,
    string PlateNumber,
    string VehicleType,
    DateTimeOffset EntryTimeUtc,
    DateTimeOffset ExitTimeUtc,
    DateTimeOffset CompletedAtUtc);
