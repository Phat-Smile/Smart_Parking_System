using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ParkingSystem.Parking.Application.ParkingLots.GetAuditLogs;

namespace ParkingSystem.Parking.Persistence.Auditing;

public sealed class ParkingAuditLogQueryRepository(ParkingDbContext dbContext) : IParkingAuditLogQueryRepository
{
    public async Task<AuditLogsPagedDto> GetAuditLogsAsync(ParkingAuditLogFilter filter, CancellationToken cancellationToken)
    {
        var query = dbContext.AuditLogEntries.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(filter.ActionType))
            query = query.Where(x => x.Action == filter.ActionType);

        if (filter.ParkingLotId.HasValue)
            query = query.Where(x => x.LotId == filter.ParkingLotId.Value);

        if (filter.ActorId.HasValue)
            query = query.Where(x => x.ActorUserId == filter.ActorId.Value);

        if (filter.FromUtc.HasValue)
        {
            var fromOffset = new DateTimeOffset(filter.FromUtc.Value, TimeSpan.Zero);
            query = query.Where(x => x.OccurredAt >= fromOffset);
        }

        if (filter.ToUtc.HasValue)
        {
            var toOffset = new DateTimeOffset(filter.ToUtc.Value, TimeSpan.Zero);
            query = query.Where(x => x.OccurredAt < toOffset);
        }

        var totalItems = await query.CountAsync(cancellationToken);
        if (totalItems == 0)
        {
            return new AuditLogsPagedDto([], filter.Page, filter.PageSize, 0, 0);
        }

        var totalPages = (int)Math.Ceiling((double)totalItems / filter.PageSize);

        var entities = await query
            .OrderByDescending(x => x.OccurredAt)
            .ThenByDescending(x => x.Id)
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToListAsync(cancellationToken);

        var dtos = entities.Select(x => new AuditLogDto(
            x.Id,
            x.OccurredAt.UtcDateTime,
            x.Action,
            "parking",
            x.ActorUserId,
            null,
            "Staff",
            x.LotId,
            x.TargetType,
            x.TargetId,
            x.Reason ?? string.Empty,
            null,
            DeserializeDetails(x.Details)
        )).ToList();

        return new AuditLogsPagedDto(dtos, filter.Page, filter.PageSize, totalItems, totalPages);
    }

    private static object? DeserializeDetails(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            return JsonSerializer.Deserialize<JsonElement>(json);
        }
        catch
        {
            return null;
        }
    }
}
