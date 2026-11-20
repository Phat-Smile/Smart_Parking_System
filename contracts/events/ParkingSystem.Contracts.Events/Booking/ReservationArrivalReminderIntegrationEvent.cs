using System.Text.Json.Serialization;

namespace ParkingSystem.Contracts.Events.Booking;

public sealed record ReservationArrivalReminderIntegrationEvent
{
    [JsonConstructor]
    public ReservationArrivalReminderIntegrationEvent(
        Guid accountId,
        Guid reservationId,
        Guid lotId,
        string reminderType,
        DateTime scheduledArrivalTimeUtc,
        DateTime cutoffTimeUtc,
        string? maskedPlateNumber)
    {
        AccountId = accountId;
        ReservationId = reservationId;
        LotId = lotId;
        ReminderType = reminderType;
        ScheduledArrivalTimeUtc = scheduledArrivalTimeUtc;
        CutoffTimeUtc = cutoffTimeUtc;
        MaskedPlateNumber = maskedPlateNumber;
    }

    public Guid AccountId { get; init; }
    public Guid ReservationId { get; init; }
    public Guid LotId { get; init; }
    public string ReminderType { get; init; }
    public DateTime ScheduledArrivalTimeUtc { get; init; }
    public DateTime CutoffTimeUtc { get; init; }
    public string? MaskedPlateNumber { get; init; }
}

public static class ReservationArrivalReminderTypes
{
    public const string ThirtyMinutesBeforeArrival = "ThirtyMinutesBeforeArrival";
    public const string FiveMinutesBeforeNoShow = "FiveMinutesBeforeNoShow";
}
