namespace ParkingSystem.Booking.Application.Common.Options;

public sealed class BookingRulesOptions
{
    public const string SectionName = "BookingRules";

    public int MaxActiveReservationsPerAccount { get; set; } = 3;
    public decimal DefaultReservationAllocationRatio { get; set; } = 0.50m;
    public int TemporaryHoldMinutes { get; set; } = 10;
    public int LeadTimeArrivalBufferMinutes { get; set; } = 30;
    public int NoShowGraceMinutes { get; set; } = 15;
    public int MaxScheduledDurationMinutes { get; set; } = 1440; // 24 hours
    public int MaxScheduledLeadDays { get; set; } = 7;
}
