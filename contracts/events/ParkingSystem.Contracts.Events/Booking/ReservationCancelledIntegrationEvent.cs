using System.Text.Json.Serialization;

namespace ParkingSystem.Contracts.Events.Booking;

public sealed record ReservationCancelledIntegrationEvent
{
    [JsonConstructor]
    public ReservationCancelledIntegrationEvent(
        Guid reservationId,
        Guid userId,
        Guid lotId,
        string cancelReason,
        decimal refundAmount,
        DateTimeOffset cancelledAtUtc,
        decimal forfeitedAmount = 0m)
    {
        ReservationId = reservationId;
        UserId = userId;
        LotId = lotId;
        CancelReason = cancelReason;
        RefundAmount = refundAmount;
        CancelledAtUtc = cancelledAtUtc;
        ForfeitedAmount = forfeitedAmount;
    }

    public Guid ReservationId { get; init; }
    public Guid UserId { get; init; }
    public Guid LotId { get; init; }
    public string CancelReason { get; init; }
    public decimal RefundAmount { get; init; }
    public DateTimeOffset CancelledAtUtc { get; init; }
    public decimal ForfeitedAmount { get; init; }
}
