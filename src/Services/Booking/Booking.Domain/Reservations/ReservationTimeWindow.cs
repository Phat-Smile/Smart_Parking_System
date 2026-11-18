namespace ParkingSystem.Booking.Domain.Reservations;

/// <summary>
/// Domain service / value object providing pure calculation methods for reservation time windows,
/// half-open interval overlap checks, and capacity ceiling calculations.
/// Invariants:
/// - Intervals are half-open: [start, end)
/// - Lead time, grace period, and hold duration are configurable
/// - Ceiling formula: floor(validCapacity * ratioPercent / 100)
/// </summary>
public static class ReservationTimeWindow
{
    public const int DefaultLeadTimeMinutes = 30;
    public const int DefaultGracePeriodMinutes = Reservation.TotalNoShowGracePeriodMinutes;
    public const int DefaultTempHoldDurationMinutes = 10;
    public const int MaxScheduledDurationMinutes = 24 * 60; // 1440 minutes = 24 hours
    public const int MaxScheduledAdvanceDays = 7;

    /// <summary>
    /// Validates scheduled booking time constraints per US-RES-002:
    /// - Entry time must be in the future (appointmentTime > now)
    /// - Entry time must be within 7 days from booking time (appointmentTime <= now + 7 days)
    /// - Duration must be positive (durationMinutes > 0)
    /// - Duration must not exceed 24 hours (durationMinutes <= 1440)
    /// - Guards against arithmetic overflow in date/time calculations
    /// </summary>
    public static void ValidateScheduledConstraints(
        DateTimeOffset appointmentTime,
        int? durationMinutes,
        DateTimeOffset now)
    {
        var nowUtc = now.ToUniversalTime();
        var appTimeUtc = appointmentTime.ToUniversalTime();

        if (appTimeUtc <= nowUtc)
        {
            throw new ReservationDomainException("Appointment time must be in the future for scheduled reservations.");
        }

        DateTimeOffset maxAllowedTime;
        try
        {
            maxAllowedTime = checked(nowUtc.AddDays(MaxScheduledAdvanceDays));
        }
        catch (ArgumentOutOfRangeException)
        {
            throw new ReservationDomainException("Invalid time range calculation.");
        }

        if (appTimeUtc > maxAllowedTime)
        {
            throw new ReservationDomainException($"Appointment time must be within {MaxScheduledAdvanceDays} days from current time.");
        }

        if (!durationMinutes.HasValue || durationMinutes.Value <= 0)
        {
            throw new ReservationDomainException("Duration must be greater than zero for scheduled reservations.");
        }

        if (durationMinutes.Value > MaxScheduledDurationMinutes)
        {
            throw new ReservationDomainException($"Duration must not exceed {MaxScheduledDurationMinutes / 60} hours for scheduled reservations.");
        }

        try
        {
            _ = checked(appTimeUtc.AddMinutes(durationMinutes.Value));
            _ = checked(appTimeUtc.AddMinutes(-DefaultLeadTimeMinutes));
        }
        catch (ArgumentOutOfRangeException)
        {
            throw new ReservationDomainException("Date calculation resulted in an invalid or overflow date range.");
        }
    }

    /// <summary>
    /// Calculates the Active Hold Window and Ceiling Check Window for a reservation.
    /// Active Hold Window: [appointmentTime - leadMinutes, appointmentTime + graceMinutes)
    /// Ceiling Check Window:
    ///   - HoldWhileDriving: [appointmentTime - leadMinutes, appointmentTime + graceMinutes)
    ///   - Scheduled: [appointmentTime - leadMinutes, appointmentTime + durationMinutes)
    /// </summary>
    public static (DateTimeOffset WindowStartAt, DateTimeOffset WindowEndAt, DateTimeOffset HoldEndAt) CalculateWindows(
        ReservationType reservationType,
        DateTimeOffset appointmentTime,
        int? durationMinutes,
        int leadMinutes = DefaultLeadTimeMinutes,
        int graceMinutes = DefaultGracePeriodMinutes)
    {
        if (leadMinutes < 0)
            throw new ArgumentOutOfRangeException(nameof(leadMinutes), "Lead time minutes cannot be negative.");
        if (graceMinutes < 0)
            throw new ArgumentOutOfRangeException(nameof(graceMinutes), "Grace period minutes cannot be negative.");

        var appTimeUtc = appointmentTime.ToUniversalTime();
        DateTimeOffset windowStartAt;
        DateTimeOffset holdEndAt;

        try
        {
            windowStartAt = checked(appTimeUtc.AddMinutes(-leadMinutes));
            holdEndAt = checked(appTimeUtc.AddMinutes(graceMinutes));
        }
        catch (ArgumentOutOfRangeException)
        {
            throw new ReservationDomainException("Date calculation resulted in an invalid or overflow date range.");
        }

        DateTimeOffset windowEndAt;
        if (reservationType == ReservationType.HoldWhileDriving)
        {
            windowEndAt = holdEndAt;
        }
        else if (reservationType == ReservationType.Scheduled)
        {
            if (durationMinutes.HasValue && durationMinutes.Value <= 0)
            {
                throw new ReservationDomainException("Duration must be greater than zero for scheduled reservations.");
            }

            if (durationMinutes.HasValue && durationMinutes.Value > MaxScheduledDurationMinutes)
            {
                throw new ReservationDomainException($"Duration must not exceed {MaxScheduledDurationMinutes / 60} hours for scheduled reservations.");
            }

            try
            {
                windowEndAt = durationMinutes.HasValue
                    ? checked(appTimeUtc.AddMinutes(durationMinutes.Value))
                    : holdEndAt;
            }
            catch (ArgumentOutOfRangeException)
            {
                throw new ReservationDomainException("Date calculation resulted in an invalid or overflow date range.");
            }

            if (windowEndAt <= windowStartAt)
            {
                throw new ReservationDomainException("Scheduled reservation end time must be after the window start time.");
            }
        }
        else
        {
            throw new ArgumentOutOfRangeException(nameof(reservationType), $"Unsupported reservation type: {reservationType}");
        }

        return (windowStartAt, windowEndAt, holdEndAt);
    }

    /// <summary>
    /// Checks whether two half-open intervals [startA, endA) and [startB, endB) overlap.
    /// Overlap condition: startA &lt; endB &amp;&amp; startB &lt; endA.
    /// Boundary: if endA == startB, they do not overlap.
    /// </summary>
    public static bool IsOverlapping(
        DateTimeOffset startA,
        DateTimeOffset endA,
        DateTimeOffset startB,
        DateTimeOffset endB)
    {
        if (endA <= startA)
            throw new ArgumentException("Interval A end must be greater than start.", nameof(endA));
        if (endB <= startB)
            throw new ArgumentException("Interval B end must be greater than start.", nameof(endB));

        return startA < endB && startB < endA;
    }

    /// <summary>
    /// Calculates the reservation ceiling capacity: floor(validCapacity * ratioPercent / 100).
    /// </summary>
    public static int CalculateCeiling(int validCapacity, int ratioPercent)
    {
        if (validCapacity < 0)
            throw new ArgumentOutOfRangeException(nameof(validCapacity), "Valid capacity cannot be negative.");
        if (ratioPercent < 0 || ratioPercent > 100)
            throw new ArgumentOutOfRangeException(nameof(ratioPercent), "Reservation ratio percent must be between 0 and 100.");

        return (int)((long)validCapacity * ratioPercent / 100);
    }
}
