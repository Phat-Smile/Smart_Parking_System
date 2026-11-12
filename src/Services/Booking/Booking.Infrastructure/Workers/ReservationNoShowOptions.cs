namespace ParkingSystem.Booking.Infrastructure.Workers;

/// <summary>
/// Configuration options for the background worker that automatically marks no-show reservations (US-RES-007).
/// </summary>
public sealed class ReservationNoShowOptions
{
    public const string SectionName = "ReservationNoShow";

    /// <summary>
    /// Worker execution cycle interval in seconds. Defaults to 60 seconds.
    /// </summary>
    public int IntervalSeconds { get; set; } = 60;

    /// <summary>
    /// Maximum number of no-show candidate reservations evaluated per execution cycle. Defaults to 50.
    /// </summary>
    public int BatchSize { get; set; } = 50;
}
