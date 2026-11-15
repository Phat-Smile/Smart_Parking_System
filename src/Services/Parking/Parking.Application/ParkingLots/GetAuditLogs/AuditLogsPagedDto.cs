namespace ParkingSystem.Parking.Application.ParkingLots.GetAuditLogs;

public sealed record AuditLogDto(
    Guid Id,
    DateTime OccurredAtUtc,
    string ActionType,
    string SourceService,
    Guid? ActorId,
    string? ActorEmail,
    string ActorRole,
    Guid? ParkingLotId,
    string TargetType,
    string TargetId,
    string Reason,
    string? IpAddress,
    object? Details);

public sealed record AuditLogsPagedDto(
    IReadOnlyList<AuditLogDto> Items,
    int Page,
    int PageSize,
    int TotalItems,
    int TotalPages);
