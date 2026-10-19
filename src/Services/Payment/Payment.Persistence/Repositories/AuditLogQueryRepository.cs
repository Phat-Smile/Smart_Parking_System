using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ParkingSystem.Payment.Application.Common.Interfaces;
using ParkingSystem.Payment.Application.Features.AuditLogs.Queries.GetAuditLogs;
using ParkingSystem.Payment.Domain.Entities;

namespace ParkingSystem.Payment.Persistence.Repositories;

public sealed class AuditLogQueryRepository(PaymentDbContext dbContext) : IAuditLogQueryRepository
{
    public async Task<AuditLogsPagedDto> GetAuditLogsAsync(AuditLogFilter filter, CancellationToken cancellationToken)
    {
        var query = dbContext.Set<AuditLog>().AsNoTracking();

        // 1. Áp dụng các bộ lọc kết hợp AND
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

        // 2. Đếm tổng số bản ghi khớp bộ lọc
        var totalItems = await query.CountAsync(cancellationToken);

        if (totalItems == 0)
        {
            return new AuditLogsPagedDto([], filter.Page, filter.PageSize, 0, 0);
        }

        var totalPages = (int)Math.Ceiling((double)totalItems / filter.PageSize);

        // 3. Sắp xếp mới nhất trước và lấy đúng trang yêu cầu
        var entities = await query
            .OrderByDescending(x => x.OccurredAtUtc)
            .ThenByDescending(x => x.Seq)
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToListAsync(cancellationToken);

        // 4. Ánh xạ sang DTO và deserialize JSON details
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
