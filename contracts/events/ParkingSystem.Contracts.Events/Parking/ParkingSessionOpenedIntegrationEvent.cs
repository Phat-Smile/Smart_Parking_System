namespace ParkingSystem.Contracts.Events.Parking;

public sealed record ParkingSessionOpenedIntegrationEvent(Guid SessionId, Guid UserId,
    Guid LotId, DateTimeOffset EntryTimeUtc);
