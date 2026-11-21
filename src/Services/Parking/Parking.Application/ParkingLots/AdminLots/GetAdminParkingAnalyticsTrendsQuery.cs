using MediatR;
using ParkingSystem.BuildingBlocks.Time;

namespace ParkingSystem.Parking.Application.ParkingLots.AdminLots;

public sealed record GetAdminParkingAnalyticsTrendsQuery(string? Period = null)
    : IRequest<AdminParkingAnalyticsTrendsResult>;

public sealed record AdminParkingAnalyticsTrendsResult(
    string[] Labels,
    int[] NewSubmissions,
    int[] ApprovedCount);

public sealed record DailyParkingLotCount(DateOnly DayLocal, int Count);

public sealed record AdminParkingAnalyticsCounts(
    IReadOnlyList<DailyParkingLotCount> NewSubmissions,
    IReadOnlyList<DailyParkingLotCount> Approvals);

public interface IAdminParkingAnalyticsReader
{
    Task<AdminParkingAnalyticsCounts> GetCountsAsync(
        DateTime startUtc,
        DateTime endUtcExclusive,
        CancellationToken cancellationToken);
}

public sealed class GetAdminParkingAnalyticsTrendsQueryHandler(
    IAdminParkingAnalyticsReader reader,
    IClock clock) : IRequestHandler<GetAdminParkingAnalyticsTrendsQuery, AdminParkingAnalyticsTrendsResult>
{
    public async Task<AdminParkingAnalyticsTrendsResult> Handle(
        GetAdminParkingAnalyticsTrendsQuery request,
        CancellationToken cancellationToken)
    {
        var days = request.Period switch
        {
            null or "7days" => 7,
            "30days" => 30,
            _ => throw new ArgumentException("period must be either '7days' or '30days'.", nameof(request.Period))
        };

        // Vietnam does not observe daylight saving time, so its calendar-day boundary is UTC+07:00.
        var vietnamOffset = TimeSpan.FromHours(7);
        var todayVietnam = DateOnly.FromDateTime(clock.UtcNow.ToOffset(vietnamOffset).DateTime);
        var firstDayVietnam = todayVietnam.AddDays(1 - days);
        var startUtc = new DateTimeOffset(
            firstDayVietnam.ToDateTime(TimeOnly.MinValue), vietnamOffset).UtcDateTime;
        var endUtcExclusive = new DateTimeOffset(
            todayVietnam.AddDays(1).ToDateTime(TimeOnly.MinValue), vietnamOffset).UtcDateTime;
        var counts = await reader.GetCountsAsync(startUtc, endUtcExclusive, cancellationToken);

        var submissionsByDay = counts.NewSubmissions.ToDictionary(item => item.DayLocal, item => item.Count);
        var approvalsByDay = counts.Approvals.ToDictionary(item => item.DayLocal, item => item.Count);
        var dates = Enumerable.Range(0, days).Select(offset => firstDayVietnam.AddDays(offset)).ToArray();

        return new AdminParkingAnalyticsTrendsResult(
            dates.Select(day => day.ToString("dd/MM", System.Globalization.CultureInfo.InvariantCulture)).ToArray(),
            dates.Select(day => submissionsByDay.GetValueOrDefault(day)).ToArray(),
            dates.Select(day => approvalsByDay.GetValueOrDefault(day)).ToArray());
    }
}
