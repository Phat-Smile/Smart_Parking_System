namespace ParkingSystem.Booking.Application.Reservations;

/// <summary>
/// Configuration options for booking capacity guard and temporary hold windows.
/// </summary>
public sealed class BookingCapacityOptions
{
    public const string SectionName = "BookingCapacity";

    /// <summary>
    /// Lead time in minutes before appointment time when active hold window begins (default 30 min).
    /// </summary>
    public int LeadTimeMinutes { get; set; } = 30;

    /// <summary>
    /// Grace period in minutes after appointment time when active hold window ends (default 15 min).
    /// </summary>
    public int GracePeriodMinutes { get; set; } = 15;

    /// <summary>
    /// Duration of temporary hold in minutes (default 10 min).
    /// </summary>
    public int TempHoldDurationMinutes { get; set; } = 10;

    /// <summary>
    /// Row lock timeout in seconds when acquiring SELECT ... FOR UPDATE (default 5s).
    /// </summary>
    public int LockTimeoutSeconds { get; set; } = 5;
}

