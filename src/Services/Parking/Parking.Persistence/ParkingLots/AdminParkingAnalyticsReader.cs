using Microsoft.EntityFrameworkCore;
using ParkingSystem.Parking.Application.Common.Auditing;
using ParkingSystem.Parking.Application.ParkingLots.AdminLots;

namespace ParkingSystem.Parking.Persistence.ParkingLots;

internal sealed class AdminParkingAnalyticsReader(ParkingDbContext dbContext) : IAdminParkingAnalyticsReader
{
    public async Task<AdminParkingAnalyticsCounts> GetCountsAsync(
        DateTime startUtc,
        DateTime endUtcExclusive,
        CancellationToken cancellationToken)
    {
        var submissions = await dbContext.Database.SqlQuery<DailyCountRow>($"""
                SELECT (created_at AT TIME ZONE 'Asia/Ho_Chi_Minh')::date AS "DayLocal", count(*)::integer AS "Count"
                FROM parking.parking_lots
                WHERE created_at >= {startUtc} AND created_at < {endUtcExclusive}
                GROUP BY (created_at AT TIME ZONE 'Asia/Ho_Chi_Minh')::date
                """)
            .ToListAsync(cancellationToken);

        var approvals = await dbContext.Database.SqlQuery<DailyCountRow>($"""
                SELECT (occurred_at AT TIME ZONE 'Asia/Ho_Chi_Minh')::date AS "DayLocal", count(*)::integer AS "Count"
                FROM parking.audit_logs
                WHERE action = {AuditActions.ParkingLotApproved}
                  AND occurred_at >= {startUtc} AND occurred_at < {endUtcExclusive}
                GROUP BY (occurred_at AT TIME ZONE 'Asia/Ho_Chi_Minh')::date
                """)
            .ToListAsync(cancellationToken);

        return new AdminParkingAnalyticsCounts(
            submissions.Select(row => new DailyParkingLotCount(row.DayLocal, row.Count)).ToArray(),
            approvals.Select(row => new DailyParkingLotCount(row.DayLocal, row.Count)).ToArray());
    }

    // EF Core 10's unmapped SqlQuery type only needs public settable properties.
    private sealed class DailyCountRow
    {
        public DateOnly DayLocal { get; set; }
        public int Count { get; set; }
    }
}
