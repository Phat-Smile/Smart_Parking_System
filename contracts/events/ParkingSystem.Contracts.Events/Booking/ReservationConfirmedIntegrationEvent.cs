using System.Text.Json.Serialization;

namespace ParkingSystem.Contracts.Events.Booking;

public sealed record ReservationConfirmedIntegrationEvent
{
    [JsonConstructor]
    public ReservationConfirmedIntegrationEvent(
        Guid reservationId,
        Guid userId,
        Guid lotId,
        string reservationType,
        decimal prepaidAmount,
        DateTimeOffset appointmentTime,
        DateTimeOffset confirmedAtUtc)
    {
        ReservationId = reservationId;
        UserId = userId;
        LotId = lotId;
        ReservationType = reservationType;
        PrepaidAmount = prepaidAmount;
        AppointmentTime = appointmentTime;
        ConfirmedAtUtc = confirmedAtUtc;
    }

    public ReservationConfirmedIntegrationEvent(
        Guid reservationId,
        Guid userId,
        Guid lotId,
        DateTimeOffset confirmedAtUtc)
        : this(reservationId, userId, lotId, "HoldWhileDriving", 0m, confirmedAtUtc, confirmedAtUtc)
    {
    }

    public Guid ReservationId { get; init; }
    public Guid UserId { get; init; }
    public Guid LotId { get; init; }
    public string ReservationType { get; init; }
    public decimal PrepaidAmount { get; init; }
    public DateTimeOffset AppointmentTime { get; init; }
    public DateTimeOffset ConfirmedAtUtc { get; init; }
}
