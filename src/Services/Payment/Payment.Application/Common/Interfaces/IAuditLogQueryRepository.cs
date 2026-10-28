using ParkingSystem.Payment.Application.Features.AuditLogs.Queries.GetAuditLogs;

namespace ParkingSystem.Payment.Application.Common.Interfaces;

public sealed record AuditLogFilter(
    int Page,
    int PageSize,
    string? ActionType,
    Guid? ParkingLotId,
    Guid? ActorId,
    DateTime? FromUtc,
    DateTime? ToUtc);

public interface IAuditLogQueryRepository
{
    Task<AuditLogsPagedDto> GetAuditLogsAsync(AuditLogFilter filter, CancellationToken cancellationToken);
}
