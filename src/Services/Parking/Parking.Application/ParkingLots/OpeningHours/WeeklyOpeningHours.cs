using System.Globalization;
using System.Text.Json;

namespace ParkingSystem.Parking.Application.ParkingLots.OpeningHours;

// DayOfWeek uses .NET numbering (Sunday = 0 ... Saturday = 6).
// Times are Vietnam-local HH:mm. Overnight intervals finish on the next calendar day.
public sealed record DailyOpeningHours(
    int DayOfWeek,
    bool IsClosed,
    bool Is24Hours,
    string? OpensAt,
    string? ClosesAt);

public sealed record WeeklyOpeningHours(IReadOnlyList<DailyOpeningHours> Days);

public sealed record OpeningHoursView(Guid LotId, bool IsConfigured, WeeklyOpeningHours? Schedule);

public static class OpeningHoursSchedule
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly TimeSpan VietnamUtcOffset = TimeSpan.FromHours(7);

    public static bool IsValid(WeeklyOpeningHours? schedule)
    {
        if (schedule?.Days is not { Count: 7 })
            return false;

        var seen = new HashSet<int>();
        foreach (var day in schedule.Days)
        {
            if (day is null || day.DayOfWeek is < 0 or > 6 || !seen.Add(day.DayOfWeek))
                return false;

            if (day.IsClosed || day.Is24Hours)
            {
                if (day.IsClosed && day.Is24Hours)
                    return false;
                if (day.OpensAt is not null || day.ClosesAt is not null)
                    return false;
                continue;
            }

            if (!TryTime(day.OpensAt, out var opens) ||
                !TryTime(day.ClosesAt, out var closes) || opens == closes)
                return false;
        }

        return true;
    }

    public static string Serialize(WeeklyOpeningHours schedule) => JsonSerializer.Serialize(schedule, JsonOptions);

    public static WeeklyOpeningHours? Deserialize(string? json) =>
        string.IsNullOrEmpty(json) ? null : JsonSerializer.Deserialize<WeeklyOpeningHours>(json, JsonOptions);

    // Unconfigured legacy lots stay visible. Configured lots must be open at the supplied arrival time.
    public static bool IsOpenAt(string? json, DateTimeOffset arrivalAt)
    {
        if (string.IsNullOrEmpty(json))
            return true;

        WeeklyOpeningHours? schedule;
        try
        {
            schedule = Deserialize(json);
        }
        catch (JsonException)
        {
            return false; // Bad stored data must not advertise a closed lot as open.
        }

        if (!IsValid(schedule))
            return false;

        var local = arrivalAt.ToOffset(VietnamUtcOffset);
        var day = (int)local.DayOfWeek;
        var time = TimeOnly.FromDateTime(local.DateTime);
        var current = schedule!.Days.Single(item => item.DayOfWeek == day);
        if (current.Is24Hours)
            return true;
        if (!current.IsClosed)
        {
            var opens = ParseTime(current.OpensAt!);
            var closes = ParseTime(current.ClosesAt!);
            if (closes > opens && time >= opens && time < closes)
                return true;
            if (closes < opens && time >= opens)
                return true;
        }

        // A late-night window from yesterday may extend into today.
        var previous = schedule.Days.Single(item => item.DayOfWeek == (day + 6) % 7);
        if (previous.IsClosed || previous.Is24Hours)
            return false;
        var previousOpen = ParseTime(previous.OpensAt!);
        var previousClose = ParseTime(previous.ClosesAt!);
        return previousClose < previousOpen && time < previousClose;
    }

    private static bool TryTime(string? value, out TimeOnly time) =>
        TimeOnly.TryParseExact(value, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out time);

    private static TimeOnly ParseTime(string value) =>
        TimeOnly.ParseExact(value, "HH:mm", CultureInfo.InvariantCulture);
}
