using ParkingSystem.Identity.Application.Accounts.Admin.GetAuditLogs;

namespace ParkingSystem.Identity.Application.Common.Interfaces;

public sealed record IdentityAuditLogFilter(
    int Page,
    int PageSize,
    string? ActionType,
    Guid? ParkingLotId,
    Guid? ActorId,
    DateTime? FromUtc,
    DateTime? ToUtc);

public interface IIdentityAuditLogQueryRepository
{
    Task<AuditLogsPagedDto> GetAuditLogsAsync(IdentityAuditLogFilter filter, CancellationToken cancellationToken);
}
