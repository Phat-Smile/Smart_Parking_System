namespace ParkingSystem.Contracts.Events.Booking;

public sealed record ReservationConfirmedIntegrationEvent(Guid ReservationId, Guid UserId,
    Guid LotId, DateTimeOffset ConfirmedAtUtc);
