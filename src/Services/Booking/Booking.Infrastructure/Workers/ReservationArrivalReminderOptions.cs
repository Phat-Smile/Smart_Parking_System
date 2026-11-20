namespace ParkingSystem.Booking.Infrastructure.Workers;

/// <summary>
/// Configuration options for the background worker that sends reservation arrival reminders.
/// </summary>
public sealed class ReservationArrivalReminderOptions
{
    public const string SectionName = "ReservationArrivalReminder";

    /// <summary>
    /// Worker execution cycle interval in seconds. Defaults to 60 seconds.
    /// </summary>
    public int IntervalSeconds { get; set; } = 60;

    /// <summary>
    /// Maximum number of candidate reservations evaluated per execution cycle. Defaults to 50.
    /// </summary>
    public int BatchSize { get; set; } = 50;
}
