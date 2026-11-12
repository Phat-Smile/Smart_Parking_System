using System;
using ParkingSystem.Payment.Domain.Constants;

namespace ParkingSystem.Payment.Domain.Pricing;

public enum ParkingMethod
{
    PerTurn = 1,
    PerBlock = 2
}

public static class PricingEngine
{
    // Thời gian ân hạn mặc định theo mục 13 context.md (15 phút)
    public const int DefaultGracePeriodMinutes = 15;

    // Múi giờ địa phương mặc định của Việt Nam theo BR-09.b (UTC+7)
    public static readonly TimeSpan DefaultVietnamOffset = TimeSpan.FromHours(7);

    // Khung giờ cao điểm đô thị: Sáng 07:30 - 09:00, Chiều 17:00 - 19:00
    public static readonly (TimeSpan Start, TimeSpan End) MorningPeak = (new TimeSpan(7, 30, 0), new TimeSpan(9, 0, 0));
    public static readonly (TimeSpan Start, TimeSpan End) EveningPeak = (new TimeSpan(17, 0, 0), new TimeSpan(19, 0, 0));
    public const decimal DefaultPeakMultiplier = 1.2m; // +20% phụ thu giờ cao điểm
    public const decimal DefaultCongestionMultiplier = 1.3m; // +30% phụ thu bão ô đỗ (>85% lấp đầy)

    public static bool IsPeakHour(DateTimeOffset time, TimeSpan? localTimeZoneOffset = null)
    {
        var offset = localTimeZoneOffset ?? DefaultVietnamOffset;
        var local = time.ToOffset(offset);
        if (local.DayOfWeek == DayOfWeek.Saturday || local.DayOfWeek == DayOfWeek.Sunday)
        {
            return false;
        }

        var tod = local.TimeOfDay;
        return (tod >= MorningPeak.Start && tod <= MorningPeak.End)
            || (tod >= EveningPeak.Start && tod <= EveningPeak.End);
    }

    public static decimal CalculateSurgeMultiplier(
        DateTimeOffset time,
        double occupancyRate = 0.0,
        bool enablePeakSurge = true,
        bool enableOccupancySurge = true,
        TimeSpan? localTimeZoneOffset = null)
    {
        decimal multiplier = 1.0m;
        if (enablePeakSurge && IsPeakHour(time, localTimeZoneOffset))
        {
            multiplier = Math.Max(multiplier, DefaultPeakMultiplier);
        }

        if (enableOccupancySurge && occupancyRate >= 0.85)
        {
            multiplier = Math.Max(multiplier, DefaultCongestionMultiplier);
        }

        return multiplier;
    }

    public static decimal CalculateSurgeFee(
        decimal baseFee,
        DateTimeOffset time,
        double occupancyRate = 0.0,
        bool enablePeakSurge = true,
        bool enableOccupancySurge = true,
        TimeSpan? localTimeZoneOffset = null)
    {
        var multiplier = CalculateSurgeMultiplier(time, occupancyRate, enablePeakSurge, enableOccupancySurge, localTimeZoneOffset);
        return Math.Round(baseFee * multiplier, MidpointRounding.AwayFromZero);
    }

    public static decimal CalculateBaseFee(
        DateTimeOffset startTime,
        DateTimeOffset endTime,
        ParkingMethod method,
        int blockMinutes,
        decimal pricePerBlockOrTurn,
        decimal overnightFee,
        TimeSpan overnightTimeMark,
        TimeSpan? localTimeZoneOffset = null)
    {
        if (method == ParkingMethod.PerBlock && blockMinutes <= 0)
        {
            throw new ArgumentException(DomainErrorMessages.Pricing.BlockMinutesMustBePositive, nameof(blockMinutes));
        }

        if (endTime < startTime)
        {
            throw new ArgumentException(DomainErrorMessages.Pricing.EndTimeBeforeStartTime, nameof(endTime));
        }

        decimal totalFee = 0m;

        if (method == ParkingMethod.PerTurn)
        {
            // Theo lượt: Mỗi phiên vào ra luôn tính 1 lượt (BR-09.a)
            totalFee = pricePerBlockOrTurn;
        }
        else
        {
            // Theo block: Làm tròn lên block, tối thiểu 1 block kể cả khi endTime == startTime (BR-09.a)
            var duration = endTime - startTime;
            int blocks = (int)Math.Ceiling(duration.TotalMinutes / blockMinutes);
            if (blocks < 1)
            {
                blocks = 1;
            }
            totalFee = blocks * pricePerBlockOrTurn;
        }

        int nights = CountOvernightMarks(startTime, endTime, overnightTimeMark, localTimeZoneOffset);
        totalFee += nights * overnightFee;

        return totalFee;
    }

    public static decimal CalculateOverstayFee(
        DateTimeOffset paymentTime,
        DateTimeOffset exitTime,
        ParkingMethod method,
        int blockMinutes,
        decimal pricePerBlockOrTurn,
        decimal overnightFee,
        TimeSpan overnightTimeMark,
        int gracePeriodMinutes = DefaultGracePeriodMinutes,
        TimeSpan? localTimeZoneOffset = null)
    {
        if (method == ParkingMethod.PerBlock && blockMinutes <= 0)
        {
            throw new ArgumentException(DomainErrorMessages.Pricing.BlockMinutesMustBePositive, nameof(blockMinutes));
        }

        if (exitTime < paymentTime)
        {
            throw new ArgumentException(DomainErrorMessages.Pricing.ExitTimeBeforePaymentTime, nameof(exitTime));
        }

        if (gracePeriodMinutes < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(gracePeriodMinutes), DomainErrorMessages.Pricing.GracePeriodMinutesNegative);
        }

        var gracePeriodEnd = paymentTime.AddMinutes(gracePeriodMinutes);

        // Ra cổng trong thời gian ân hạn thì không phạt phí vượt (BR-09.d)
        if (exitTime <= gracePeriodEnd)
        {
            return 0m;
        }

        decimal overstayFee = 0m;
        var overstayDuration = exitTime - gracePeriodEnd;

        if (method == ParkingMethod.PerTurn)
        {
            // Bãi tính theo lượt thì không thu thêm tiền lượt (BR-09.d)
            overstayFee = 0m;
        }
        else
        {
            // Bãi tính theo block thì làm tròn lên block, tối thiểu 1 block (BR-09.d)
            int blocks = (int)Math.Ceiling(overstayDuration.TotalMinutes / blockMinutes);
            if (blocks < 1)
            {
                blocks = 1;
            }
            overstayFee = blocks * pricePerBlockOrTurn;
        }

        // Nếu trong thời gian vượt xe có vắt qua mốc qua đêm thì cộng phí qua đêm (BR-09.d)
        int overstayNights = CountOvernightMarks(paymentTime, exitTime, overnightTimeMark, localTimeZoneOffset);
        overstayFee += overstayNights * overnightFee;

        return overstayFee;
    }

    public static int CountOvernightMarks(
        DateTimeOffset start,
        DateTimeOffset end,
        TimeSpan overnightTimeMark,
        TimeSpan? localTimeZoneOffset = null)
    {
        // Xác định múi giờ bãi xe: ưu tiên tham số truyền vào, nếu không thì lấy start.Offset (nếu khác UTC), mặc định UTC+7
        TimeSpan effectiveOffset;
        if (localTimeZoneOffset.HasValue)
        {
            effectiveOffset = localTimeZoneOffset.Value;
        }
        else
        {
            if (start.Offset != TimeSpan.Zero)
            {
                effectiveOffset = start.Offset;
            }
            else
            {
                effectiveOffset = DefaultVietnamOffset;
            }
        }

        // Đổi mốc thời gian sang giờ địa phương của bãi xe theo BR-09.b
        DateTimeOffset startLocal = start.ToOffset(effectiveOffset);
        DateTimeOffset endLocal = end.ToOffset(effectiveOffset);

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

            // Xe còn trong bãi tại mốc qua đêm: vào trước mốc và ra sau hoặc đúng mốc
            if (markTime > startLocal && markTime <= endLocal)
            {
                count++;
            }

            currentDay = currentDay.AddDays(1);
        }

        return count;
    }
}
