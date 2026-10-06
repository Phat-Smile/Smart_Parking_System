using ParkingSystem.Booking.Domain.Enums;

namespace ParkingSystem.Booking.Domain.Reservations;

/// <summary>
/// Represents a half-open time interval [Start, End) used for reservation capacity counting and overlap checks (BR-02.e).
/// </summary>
public readonly record struct ReservationTimeWindow
{
    public const int LeadTimeArrivalBufferMinutes = 30;
    public const int NoShowGraceMinutes = 15;

    public DateTimeOffset Start { get; }
    public DateTimeOffset End { get; }

    public ReservationTimeWindow(DateTimeOffset start, DateTimeOffset end)
    {
        if (start >= end)
        {
            throw new ArgumentException($"Window start ({start:O}) must be strictly earlier than window end ({end:O}).", nameof(start));
        }

        Start = start;
        End = end;
    }

    /// <summary>
    /// Calculates the effective reservation hold window based on BR-02.e and BR-03.d.
    /// Capacity starts counting 30 minutes before the target/entry time.
    /// Scheduled booking window spans through the end of the booked duration.
    /// Hold While Driving window spans until the end of the no-show grace period (15 minutes after appointment time).
    /// </summary>
    public static ReservationTimeWindow ForReservation(
        ReservationType reservationType,
        DateTimeOffset appointmentTime,
        int? durationMinutes)
    {
        var start = appointmentTime.AddMinutes(-LeadTimeArrivalBufferMinutes);

        var end = reservationType switch
        {
            ReservationType.Scheduled => appointmentTime.AddMinutes(durationMinutes ?? throw new ArgumentException("Duration in minutes is required for scheduled reservations.", nameof(durationMinutes))),
            ReservationType.HoldWhileDriving => appointmentTime.AddMinutes(NoShowGraceMinutes),
            _ => throw new ArgumentOutOfRangeException(nameof(reservationType), reservationType, "Unsupported reservation type.")
        };

        return new ReservationTimeWindow(start, end);
    }

    /// <summary>
    /// Evaluates if two half-open intervals [Start, End) overlap.
    /// Two windows overlap iff A.Start &lt; B.End AND B.Start &lt; A.End.
    /// Touching boundaries [A, B) and [B, C) do NOT overlap.
    /// </summary>
    public bool Overlaps(ReservationTimeWindow other)
    {
        return Start < other.End && other.Start < End;
    }

    /// <summary>
    /// Checks whether the evaluated timestamp falls inside the half-open interval [Start, End).
    /// </summary>
    public bool Contains(DateTimeOffset timestamp)
    {
        return timestamp >= Start && timestamp < End;
    }
}
