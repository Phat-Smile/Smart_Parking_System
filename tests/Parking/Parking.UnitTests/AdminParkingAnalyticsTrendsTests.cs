using ParkingSystem.BuildingBlocks.Time;
using ParkingSystem.Parking.Application.ParkingLots.AdminLots;
using Xunit;

namespace ParkingSystem.Tests.Parking.UnitTests;

public sealed class AdminParkingAnalyticsTrendsTests
{
    [Fact]
    public async Task Seven_day_trends_use_vietnam_days_fill_gaps_and_keep_arrays_aligned()
    {
        var reader = new FakeReader(new AdminParkingAnalyticsCounts(
            [new(new DateOnly(2026, 11, 16), 2), new(new DateOnly(2026, 11, 21), 3)],
            [new(new DateOnly(2026, 11, 19), 1)]));
        var handler = new GetAdminParkingAnalyticsTrendsQueryHandler(reader, new FixedClock());

        var result = await handler.Handle(new("7days"), TestContext.Current.CancellationToken);

        Assert.Equal(new[] { "15/11", "16/11", "17/11", "18/11", "19/11", "20/11", "21/11" }, result.Labels);
        Assert.Equal(new[] { 0, 2, 0, 0, 0, 0, 3 }, result.NewSubmissions);
        Assert.Equal(new[] { 0, 0, 0, 0, 1, 0, 0 }, result.ApprovedCount);
        Assert.Equal(result.Labels.Length, result.NewSubmissions.Length);
        Assert.Equal(result.Labels.Length, result.ApprovedCount.Length);
        Assert.Equal(DateTimeKind.Utc, reader.StartUtc!.Value.Kind);
        Assert.Equal(new DateTime(2026, 11, 14, 17, 0, 0, DateTimeKind.Utc), reader.StartUtc);
        Assert.Equal(new DateTime(2026, 11, 21, 17, 0, 0, DateTimeKind.Utc), reader.EndUtcExclusive);
    }

    [Fact]
    public async Task Thirty_day_trends_return_thirty_chronological_labels()
    {
        var reader = new FakeReader(new AdminParkingAnalyticsCounts([], []));
        var result = await new GetAdminParkingAnalyticsTrendsQueryHandler(reader, new FixedClock())
            .Handle(new("30days"), TestContext.Current.CancellationToken);

        Assert.Equal(30, result.Labels.Length);
        Assert.Equal("23/10", result.Labels[0]);
        Assert.Equal("21/11", result.Labels[^1]);
        Assert.All(result.NewSubmissions, count => Assert.Equal(0, count));
        Assert.All(result.ApprovedCount, count => Assert.Equal(0, count));
    }

    [Fact]
    public async Task Omitted_period_defaults_to_seven_days_and_invalid_period_is_rejected()
    {
        var reader = new FakeReader(new AdminParkingAnalyticsCounts([], []));
        var handler = new GetAdminParkingAnalyticsTrendsQueryHandler(reader, new FixedClock());

        var result = await handler.Handle(new(), TestContext.Current.CancellationToken);
        Assert.Equal(7, result.Labels.Length);
        await Assert.ThrowsAsync<ArgumentException>(() => handler.Handle(
            new("90days"), TestContext.Current.CancellationToken));
    }

    private sealed class FixedClock : IClock
    {
        public DateTimeOffset UtcNow => new(2026, 11, 21, 10, 30, 0, TimeSpan.Zero);
    }

    private sealed class FakeReader(AdminParkingAnalyticsCounts result) : IAdminParkingAnalyticsReader
    {
        public DateTime? StartUtc { get; private set; }
        public DateTime? EndUtcExclusive { get; private set; }

        public Task<AdminParkingAnalyticsCounts> GetCountsAsync(
            DateTime startUtc, DateTime endUtcExclusive, CancellationToken cancellationToken)
        {
            StartUtc = startUtc;
            EndUtcExclusive = endUtcExclusive;
            return Task.FromResult(result);
        }
    }
}
