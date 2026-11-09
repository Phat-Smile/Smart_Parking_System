namespace ParkingSystem.Booking.Domain.Reservations;

/// <summary>
/// Domain service for calculating 100% upfront prepayment for scheduled reservations (BR-10.a, BR-09.b).
/// - BillingMode "Block": booked duration rounded UP to whole blocks x block price (minimum 1 block).
///   Valid BlockMinutes are 15, 30, or 60.
/// - BillingMode "PerEntry": prepayment = price of ONE entry, independent of duration.
/// - If the booked interval crosses overnight marks, add OvernightFee x number of marks inside the interval (BR-09.b).
///   Mark is in the local time of the lot (default 00:00, Asia/Ho_Chi_Minh UTC+7).
/// </summary>
public static class ScheduledPrepaymentCalculator
{
    public const string BillingModeBlock = "Block";
    public const string BillingModePerEntry = "PerEntry";

    public static readonly TimeSpan DefaultOvernightTimeMark = TimeSpan.Zero; // 00:00
    public static readonly TimeSpan DefaultVietnamOffset = TimeSpan.FromHours(7); // UTC+7

    /// <summary>
    /// Calculates the upfront prepayment amount for a scheduled booking.
    /// </summary>
    public static decimal Calculate(
        string billingMode,
        int? blockMinutes,
        decimal price,
        decimal overnightFee,
        TimeSpan? overnightMark,
        DateTimeOffset start,
        DateTimeOffset end,
        TimeSpan? localTimeZoneOffset = null)
    {
        if (string.IsNullOrWhiteSpace(billingMode))
        {
            throw new ReservationDomainException("Billing mode cannot be null or empty.");
        }

        if (price < 0m || price % 1m != 0m)
        {
            throw new ReservationDomainException($"Price must be a non-negative integer amount: {price}.");
        }

        if (overnightFee < 0m || overnightFee % 1m != 0m)
        {
            throw new ReservationDomainException($"Overnight fee must be a non-negative integer amount: {overnightFee}.");
        }

        if (end <= start)
        {
            throw new ReservationDomainException($"End time ({end}) must be strictly after start time ({start}).");
        }

        decimal basePrepayment;

        if (string.Equals(billingMode, BillingModePerEntry, StringComparison.OrdinalIgnoreCase))
        {
            basePrepayment = price;
        }
        else if (string.Equals(billingMode, BillingModeBlock, StringComparison.OrdinalIgnoreCase))
        {
            if (!blockMinutes.HasValue || (blockMinutes.Value != 15 && blockMinutes.Value != 30 && blockMinutes.Value != 60))
            {
                throw new ReservationDomainException($"Block billing mode requires BlockMinutes to be 15, 30, or 60. Received: {blockMinutes?.ToString() ?? "null"}.");
            }

            long durationMinutes = (long)Math.Ceiling((end - start).TotalMinutes);
            if (durationMinutes <= 0)
            {
                durationMinutes = 1;
            }

            int blocks = (int)Math.Ceiling((double)durationMinutes / blockMinutes.Value);
            if (blocks <= 0)
            {
                blocks = 1;
            }

            basePrepayment = checked(blocks * price);
        }
        else
        {
            throw new ReservationDomainException($"Unsupported billing mode: '{billingMode}'. Supported modes are '{BillingModePerEntry}' and '{BillingModeBlock}'.");
        }

        // Calculate overnight surcharge (BR-10.a, BR-09.b)
        int overnightMarksCount = 0;
        if (overnightFee > 0m)
        {
            overnightMarksCount = CountOvernightMarks(
                start,
                end,
                overnightMark ?? DefaultOvernightTimeMark,
                localTimeZoneOffset ?? DefaultVietnamOffset);
        }

        decimal totalPrepayment = checked(basePrepayment + (overnightMarksCount * overnightFee));
        return totalPrepayment;
    }

    /// <summary>
    /// Counts how many overnight marks are crossed within (start, end].
    /// A mark counts when start < mark <= end in the lot's local time.
    /// Matches Payment's PricingEngine.CountOvernightMarks.
    /// </summary>
    public static int CountOvernightMarks(
        DateTimeOffset start,
        DateTimeOffset end,
        TimeSpan overnightTimeMark,
        TimeSpan? localTimeZoneOffset = null)
    {
        TimeSpan effectiveOffset = localTimeZoneOffset ?? (start.Offset != TimeSpan.Zero ? start.Offset : DefaultVietnamOffset);

        DateTimeOffset startLocal = start.ToOffset(effectiveOffset);
        DateTimeOffset endLocal = end.ToOffset(effectiveOffset);

        if (endLocal <= startLocal)
        {
            return 0;
        }

        int count = 0;
        var currentDay = startLocal.Date;

        while (currentDay <= endLocal.Date)
        {
            var markTime = new DateTimeOffset(
                currentDay.Year,
                currentDay.Month,
                currentDay.Day,
                overnightTimeMark.Hours,
                overnightTimeMark.Minutes,
                overnightTimeMark.Seconds,
                effectiveOffset);

            // Vehicle is inside the lot at the mark: entered before the mark and exits at or after the mark
            if (markTime > startLocal && markTime <= endLocal)
            {
                count++;
            }

            currentDay = currentDay.AddDays(1);
        }

        return count;
    }
}

