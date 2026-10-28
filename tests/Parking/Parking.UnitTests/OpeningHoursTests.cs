using System.Text.Json;
using ParkingSystem.Parking.Application.ParkingLots.OpeningHours;
using Xunit;

namespace ParkingSystem.Tests.Parking.UnitTests;

public sealed class OpeningHoursTests
{
    [Fact]
    public void Schedule_requires_seven_unique_days_and_rejects_invalid_times()
    {
        var valid = Week(1, "09:00", "18:00");
        Assert.True(OpeningHoursSchedule.IsValid(valid));
        Assert.False(OpeningHoursSchedule.IsValid(new WeeklyOpeningHours(valid.Days.Take(6).ToArray())));
        Assert.False(OpeningHoursSchedule.IsValid(new WeeklyOpeningHours(
            valid.Days.Take(6).Append(valid.Days[0]).ToArray())));
        Assert.False(OpeningHoursSchedule.IsValid(Week(1, "09:00", "09:00")));
        Assert.False(OpeningHoursSchedule.IsValid(Week(1, "9am", "18:00")));
        Assert.False(OpeningHoursSchedule.IsValid(new WeeklyOpeningHours(
            valid.Days.Select(d => d.DayOfWeek == 1 ? d with { IsClosed = true } : d).ToArray())));
    }

    [Fact]
    public void Search_applies_vietnam_local_time_and_half_open_intervals()
    {
        var json = OpeningHoursSchedule.Serialize(Week(1, "09:00", "18:00"));
        // Monday Oct 19, 2026; 02:00 UTC is 09:00 in Vietnam.
        Assert.True(OpeningHoursSchedule.IsOpenAt(json, new DateTimeOffset(2026, 10, 19, 2, 0, 0, TimeSpan.Zero)));
        Assert.True(OpeningHoursSchedule.IsOpenAt(json, new DateTimeOffset(2026, 10, 19, 10, 59, 0, TimeSpan.Zero)));
        Assert.False(OpeningHoursSchedule.IsOpenAt(json, new DateTimeOffset(2026, 10, 19, 11, 0, 0, TimeSpan.Zero)));
        Assert.False(OpeningHoursSchedule.IsOpenAt(json, new DateTimeOffset(2026, 10, 20, 2, 0, 0, TimeSpan.Zero)));
    }

    [Fact]
    public void Overnight_schedule_carries_from_sunday_into_monday()
    {
        var json = OpeningHoursSchedule.Serialize(Week(0, "22:00", "02:00"));
        Assert.True(OpeningHoursSchedule.IsOpenAt(json, new DateTimeOffset(2026, 10, 18, 16, 0, 0, TimeSpan.Zero)));
        Assert.True(OpeningHoursSchedule.IsOpenAt(json, new DateTimeOffset(2026, 10, 18, 18, 59, 0, TimeSpan.Zero)));
        Assert.False(OpeningHoursSchedule.IsOpenAt(json, new DateTimeOffset(2026, 10, 18, 19, 0, 0, TimeSpan.Zero)));
    }

    [Fact]
    public void Always_open_closed_and_unconfigured_lots_behave_as_documented()
    {
        var days = Enumerable.Range(0, 7)
            .Select(d => new DailyOpeningHours(d, true, false, null, null)).ToArray();
        days[2] = days[2] with { IsClosed = false, Is24Hours = true };
        var json = OpeningHoursSchedule.Serialize(new WeeklyOpeningHours(days));
        var tuesday = new DateTimeOffset(2026, 10, 20, 8, 0, 0, TimeSpan.FromHours(7));
        Assert.True(OpeningHoursSchedule.IsOpenAt(json, tuesday));
        Assert.False(OpeningHoursSchedule.IsOpenAt(json, tuesday.AddDays(1)));
        Assert.True(OpeningHoursSchedule.IsOpenAt(null, tuesday));
        Assert.False(OpeningHoursSchedule.IsOpenAt("{bad json", tuesday));
    }

    [Fact]
    public void Schedule_roundtrips_with_camel_case_json()
    {
        var schedule = Week(3, "06:30", "21:15");
        var json = OpeningHoursSchedule.Serialize(schedule);
        using var parsed = JsonDocument.Parse(json);
        Assert.True(parsed.RootElement.TryGetProperty("days", out _));
        Assert.True(schedule.Days.SequenceEqual(OpeningHoursSchedule.Deserialize(json)!.Days));
    }

    private static WeeklyOpeningHours Week(int openDay, string openAt, string closeAt) =>
        new(Enumerable.Range(0, 7)
            .Select(d => d == openDay
                ? new DailyOpeningHours(d, false, false, openAt, closeAt)
                : new DailyOpeningHours(d, true, false, null, null))
            .ToArray());
}
