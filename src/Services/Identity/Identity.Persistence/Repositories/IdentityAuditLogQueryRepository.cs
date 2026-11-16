using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ParkingSystem.Identity.Application.Accounts.Admin.GetAuditLogs;
using ParkingSystem.Identity.Application.Common.Interfaces;
using ParkingSystem.Identity.Domain.AuditLogs;

namespace ParkingSystem.Identity.Persistence.Repositories;

public sealed class IdentityAuditLogQueryRepository(IdentityDbContext dbContext) : IIdentityAuditLogQueryRepository
{
    public async Task<AuditLogsPagedDto> GetAuditLogsAsync(IdentityAuditLogFilter filter, CancellationToken cancellationToken)
    {
        var query = dbContext.AuditLogs.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(filter.ActionType))
            query = query.Where(x => x.ActionType == filter.ActionType);

        if (filter.ParkingLotId.HasValue)
            query = query.Where(x => x.ParkingLotId == filter.ParkingLotId.Value);

        if (filter.ActorId.HasValue)
            query = query.Where(x => x.ActorId == filter.ActorId.Value);

        if (filter.FromUtc.HasValue)
            query = query.Where(x => x.OccurredAtUtc >= filter.FromUtc.Value);

        if (filter.ToUtc.HasValue)
            query = query.Where(x => x.OccurredAtUtc < filter.ToUtc.Value);

        var totalItems = await query.CountAsync(cancellationToken);
        if (totalItems == 0)
        {
            return new AuditLogsPagedDto([], filter.Page, filter.PageSize, 0, 0);
        }

        var totalPages = (int)Math.Ceiling((double)totalItems / filter.PageSize);

        var entities = await query
            .OrderByDescending(x => x.OccurredAtUtc)
            .ThenByDescending(x => x.Seq)
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToListAsync(cancellationToken);

        var dtos = entities.Select(x => new AuditLogDto(
            x.Id,
            x.OccurredAtUtc,
            x.ActionType,
            x.SourceService,
            x.ActorId,
            x.ActorEmail,
            x.ActorRole,
            x.ParkingLotId,
            x.TargetType,
            x.TargetId,
            x.Reason,
            x.IpAddress,
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
