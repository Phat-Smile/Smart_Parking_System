namespace ParkingSystem.Parking.Application.ParkingLots.GetAuditLogs;

public sealed record ParkingAuditLogFilter(
    int Page,
    int PageSize,
    string? ActionType,
    Guid? ParkingLotId,
    Guid? ActorId,
    DateTime? FromUtc,
    DateTime? ToUtc);

public interface IParkingAuditLogQueryRepository
{
    Task<AuditLogsPagedDto> GetAuditLogsAsync(ParkingAuditLogFilter filter, CancellationToken cancellationToken);
}
