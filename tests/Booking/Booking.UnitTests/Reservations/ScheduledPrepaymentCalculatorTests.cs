using ParkingSystem.Booking.Domain.Reservations;
using Xunit;

namespace ParkingSystem.Tests.Booking.UnitTests.Reservations;

public sealed class ScheduledPrepaymentCalculatorTests
{
    private static readonly DateTimeOffset BaseUtc = new(2026, 11, 5, 0, 0, 0, TimeSpan.Zero);
    // In UTC+7, BaseUtc is 07:00:00 on 2026-11-05

    // =========================================================================
    // 1. PerEntry calculation
    // =========================================================================

    [Theory]
    [InlineData(1)]
    [InlineData(60)]
    [InlineData(240)]
    [InlineData(1440)]
    public void Calculate_PerEntry_returns_exact_unit_price_regardless_of_duration(int durationMinutes)
    {
        var start = BaseUtc;
        var end = start.AddMinutes(durationMinutes);

        var total = ScheduledPrepaymentCalculator.Calculate(
            billingMode: "PerEntry",
            blockMinutes: null,
            price: 30000m,
            overnightFee: 0m,
            overnightMark: null,
            start: start,
            end: end);

        Assert.Equal(30000m, total);
    }

    [Fact]
    public void Calculate_PerEntry_with_overnight_fee_adds_fee_for_marks_crossed()
    {
        // 22:00 to 02:00 local time crosses 00:00 mark exactly once
        // In local time UTC+7:
        // 2026-11-05 22:00 UTC+7 is 2026-11-05 15:00 UTC
        // 2026-11-06 02:00 UTC+7 is 2026-11-05 19:00 UTC
        var startLocal = new DateTimeOffset(2026, 11, 5, 22, 0, 0, TimeSpan.FromHours(7));
        var endLocal = new DateTimeOffset(2026, 11, 6, 2, 0, 0, TimeSpan.FromHours(7));

        var total = ScheduledPrepaymentCalculator.Calculate(
            billingMode: "PerEntry",
            blockMinutes: null,
            price: 30000m,
            overnightFee: 15000m,
            overnightMark: TimeSpan.Zero,
            start: startLocal,
            end: endLocal);

        // 30,000 + 1 * 15,000 = 45,000
        Assert.Equal(45000m, total);
    }

    // =========================================================================
    // 2. Block calculation (15, 30, 60 minutes)
    // =========================================================================

    [Fact]
    public void Calculate_Block60_120min_calculates_2_blocks()
    {
        var start = BaseUtc;
        var end = start.AddMinutes(120);

        var total = ScheduledPrepaymentCalculator.Calculate(
            billingMode: "Block",
            blockMinutes: 60,
            price: 20000m,
            overnightFee: 0m,
            overnightMark: null,
            start: start,
            end: end);

        // 2 blocks * 20,000 = 40,000
        Assert.Equal(40000m, total);
    }

    [Fact]
    public void Calculate_Block60_121min_rounds_up_to_3_blocks()
    {
        var start = BaseUtc;
        var end = start.AddMinutes(121);

        var total = ScheduledPrepaymentCalculator.Calculate(
            billingMode: "Block",
            blockMinutes: 60,
            price: 20000m,
            overnightFee: 0m,
            overnightMark: null,
            start: start,
            end: end);

        // 3 blocks * 20,000 = 60,000
        Assert.Equal(60000m, total);
    }

    [Fact]
    public void Calculate_Block60_1min_rounds_up_to_1_block()
    {
        var start = BaseUtc;
        var end = start.AddMinutes(1);

        var total = ScheduledPrepaymentCalculator.Calculate(
            billingMode: "Block",
            blockMinutes: 60,
            price: 20000m,
            overnightFee: 0m,
            overnightMark: null,
            start: start,
            end: end);

        // 1 block * 20,000 = 20,000
        Assert.Equal(20000m, total);
    }

    [Fact]
    public void Calculate_Block60_1440min_calculates_24_blocks()
    {
        var start = BaseUtc;
        var end = start.AddMinutes(1440);

        var total = ScheduledPrepaymentCalculator.Calculate(
            billingMode: "Block",
            blockMinutes: 60,
            price: 20000m,
            overnightFee: 0m,
            overnightMark: null,
            start: start,
            end: end);

        // 24 blocks * 20,000 = 480,000
        Assert.Equal(480000m, total);
    }

    [Theory]
    [InlineData(15, 10, 1, 5000)]
    [InlineData(15, 15, 1, 5000)]
    [InlineData(15, 16, 2, 10000)]
    [InlineData(30, 20, 1, 10000)]
    [InlineData(30, 30, 1, 10000)]
    [InlineData(30, 31, 2, 20000)]
    public void Calculate_Block15_and_Block30_cases(int blockMinutes, int duration, int expectedBlocks, decimal expectedTotal)
    {
        var start = BaseUtc;
        var end = start.AddMinutes(duration);

        var total = ScheduledPrepaymentCalculator.Calculate(
            billingMode: "Block",
            blockMinutes: blockMinutes,
            price: blockMinutes == 15 ? 5000m : 10000m,
            overnightFee: 0m,
            overnightMark: null,
            start: start,
            end: end);

        Assert.Equal(expectedTotal, total);
    }

    // =========================================================================
    // 3. Overnight surcharge calculations and boundaries
    // =========================================================================

    [Fact]
    public void Calculate_Block60_with_overnight_fee_adds_correct_total()
    {
        // 22:00 to 02:00 (4 hours = 240 minutes = 4 blocks of 60 min).
        // Crosses 00:00 mark exactly once.
        var startLocal = new DateTimeOffset(2026, 11, 5, 22, 0, 0, TimeSpan.FromHours(7));
        var endLocal = new DateTimeOffset(2026, 11, 6, 2, 0, 0, TimeSpan.FromHours(7));

        var total = ScheduledPrepaymentCalculator.Calculate(
            billingMode: "Block",
            blockMinutes: 60,
            price: 20000m,
            overnightFee: 10000m,
            overnightMark: TimeSpan.Zero,
            start: startLocal,
            end: endLocal);

        // 4 blocks * 20,000 + 1 * 10,000 = 90,000 VND
        Assert.Equal(90000m, total);
    }

    [Fact]
    public void CountOvernightMarks_interval_ending_exactly_at_mark_counts_mark()
    {
        // Interval: 20:00 to 00:00 next day.
        // Rule: start < mark <= end -> 20:00 < 00:00 <= 00:00 is TRUE -> 1 mark
        var startLocal = new DateTimeOffset(2026, 11, 5, 20, 0, 0, TimeSpan.FromHours(7));
        var endLocal = new DateTimeOffset(2026, 11, 6, 0, 0, 0, TimeSpan.FromHours(7));

        int marks = ScheduledPrepaymentCalculator.CountOvernightMarks(startLocal, endLocal, TimeSpan.Zero);
        Assert.Equal(1, marks);
    }

    [Fact]
    public void CountOvernightMarks_interval_starting_exactly_at_mark_does_not_count_that_mark()
    {
        // Interval: 00:00 to 04:00.
        // Rule: start < mark <= end -> markTime (00:00) > start (00:00) is FALSE -> 0 marks
        var startLocal = new DateTimeOffset(2026, 11, 5, 0, 0, 0, TimeSpan.FromHours(7));
        var endLocal = new DateTimeOffset(2026, 11, 5, 4, 0, 0, TimeSpan.FromHours(7));

        int marks = ScheduledPrepaymentCalculator.CountOvernightMarks(startLocal, endLocal, TimeSpan.Zero);
        Assert.Equal(0, marks);
    }

    [Fact]
    public void CountOvernightMarks_24h_crossing_counts_exactly_one_mark()
    {
        // Interval: 12:00 to 12:00 next day (24h). Crosses 00:00 once.
        var startLocal = new DateTimeOffset(2026, 11, 5, 12, 0, 0, TimeSpan.FromHours(7));
        var endLocal = new DateTimeOffset(2026, 11, 6, 12, 0, 0, TimeSpan.FromHours(7));

        int marks = ScheduledPrepaymentCalculator.CountOvernightMarks(startLocal, endLocal, TimeSpan.Zero);
        Assert.Equal(1, marks);
    }

    [Fact]
    public void CountOvernightMarks_custom_mark_at_23_00()
    {
        // Mark = 23:00. Interval: 22:00 to 23:30 -> 1 mark.
        var startLocal = new DateTimeOffset(2026, 11, 5, 22, 0, 0, TimeSpan.FromHours(7));
        var endLocal = new DateTimeOffset(2026, 11, 5, 23, 30, 0, TimeSpan.FromHours(7));

        int marks = ScheduledPrepaymentCalculator.CountOvernightMarks(startLocal, endLocal, TimeSpan.FromHours(23));
        Assert.Equal(1, marks);
    }

    [Fact]
    public void CountOvernightMarks_utc_inputs_are_converted_to_utc7_correctly()
    {
        // 17:00 UTC = 00:00 UTC+7 next day.
        // Interval: 15:00 UTC to 18:00 UTC.
        // In UTC+7: 22:00 to 01:00 next day. Crosses 00:00 UTC+7 -> 1 mark.
        var startUtc = new DateTimeOffset(2026, 11, 5, 15, 0, 0, TimeSpan.Zero);
        var endUtc = new DateTimeOffset(2026, 11, 5, 18, 0, 0, TimeSpan.Zero);

        int marks = ScheduledPrepaymentCalculator.CountOvernightMarks(startUtc, endUtc, TimeSpan.Zero);
        Assert.Equal(1, marks);
    }

    [Fact]
    public void Calculate_Block60_24h_crossing_one_overnight_mark()
    {
        // Interval: 12:00 to 12:00 next day (24h = 1440 min = 24 blocks of 60). Crosses 00:00 once.
        var startLocal = new DateTimeOffset(2026, 11, 5, 12, 0, 0, TimeSpan.FromHours(7));
        var endLocal = new DateTimeOffset(2026, 11, 6, 12, 0, 0, TimeSpan.FromHours(7));

        var total = ScheduledPrepaymentCalculator.Calculate(
            billingMode: "Block",
            blockMinutes: 60,
            price: 20000m,
            overnightFee: 10000m,
            overnightMark: TimeSpan.Zero,
            start: startLocal,
            end: endLocal);

        // 24 * 20,000 + 1 * 10,000 = 490,000 VND
        Assert.Equal(490000m, total);
    }

    [Fact]
    public void Calculate_Block60_with_custom_mark_at_23_00()
    {
        // Mark = 23:00. Interval: 22:00 to 23:30 (90 min = 2 blocks of 60). Crosses 23:00 once.
        var startLocal = new DateTimeOffset(2026, 11, 5, 22, 0, 0, TimeSpan.FromHours(7));
        var endLocal = new DateTimeOffset(2026, 11, 5, 23, 30, 0, TimeSpan.FromHours(7));

        var total = ScheduledPrepaymentCalculator.Calculate(
            billingMode: "Block",
            blockMinutes: 60,
            price: 20000m,
            overnightFee: 10000m,
            overnightMark: TimeSpan.FromHours(23),
            start: startLocal,
            end: endLocal);

        // 2 * 20,000 + 1 * 10,000 = 50,000 VND
        Assert.Equal(50000m, total);
    }

    [Fact]
    public void Calculate_Block60_utc_inputs_converted_to_utc7_correctly()
    {
        // 15:00 UTC to 18:00 UTC (180 min = 3 blocks of 60).
        // In UTC+7: 22:00 to 01:00 next day. Crosses 00:00 UTC+7 once.
        var startUtc = new DateTimeOffset(2026, 11, 5, 15, 0, 0, TimeSpan.Zero);
        var endUtc = new DateTimeOffset(2026, 11, 5, 18, 0, 0, TimeSpan.Zero);

        var total = ScheduledPrepaymentCalculator.Calculate(
            billingMode: "Block",
            blockMinutes: 60,
            price: 20000m,
            overnightFee: 10000m,
            overnightMark: TimeSpan.Zero,
            start: startUtc,
            end: endUtc);

        // 3 * 20,000 + 1 * 10,000 = 70,000 VND
        Assert.Equal(70000m, total);
    }

    // =========================================================================
    // 4. Invalid configurations and validation exceptions
    // =========================================================================

    [Theory]
    [InlineData("Hourly")]
    [InlineData("DayPass")]
    [InlineData("")]
    [InlineData(null)]
    public void Calculate_unknown_or_empty_billing_mode_throws_ReservationDomainException(string? billingMode)
    {
        Assert.Throws<ReservationDomainException>(() =>
            ScheduledPrepaymentCalculator.Calculate(
                billingMode!,
                60,
                20000m,
                0m,
                null,
                BaseUtc,
                BaseUtc.AddHours(2)));
    }

    [Theory]
    [InlineData(null)]
    [InlineData(20)]
    [InlineData(45)]
    [InlineData(0)]
    [InlineData(-15)]
    public void Calculate_Block_mode_with_invalid_block_minutes_throws_ReservationDomainException(int? blockMinutes)
    {
        Assert.Throws<ReservationDomainException>(() =>
            ScheduledPrepaymentCalculator.Calculate(
                "Block",
                blockMinutes,
                20000m,
                0m,
                null,
                BaseUtc,
                BaseUtc.AddHours(2)));
    }

    [Theory]
    [InlineData(-1000)]
    [InlineData(20000.5)]
    public void Calculate_invalid_price_throws_ReservationDomainException(decimal invalidPrice)
    {
        Assert.Throws<ReservationDomainException>(() =>
            ScheduledPrepaymentCalculator.Calculate(
                "PerEntry",
                null,
                invalidPrice,
                0m,
                null,
                BaseUtc,
                BaseUtc.AddHours(2)));
    }

    [Theory]
    [InlineData(-500)]
    [InlineData(1000.25)]
    public void Calculate_invalid_overnight_fee_throws_ReservationDomainException(decimal invalidFee)
    {
        Assert.Throws<ReservationDomainException>(() =>
            ScheduledPrepaymentCalculator.Calculate(
                "PerEntry",
                null,
                20000m,
                invalidFee,
                null,
                BaseUtc,
                BaseUtc.AddHours(2)));
    }
}

