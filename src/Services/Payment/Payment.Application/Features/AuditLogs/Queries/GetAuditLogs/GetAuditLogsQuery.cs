using MediatR;
using ParkingSystem.BuildingBlocks.Results;

namespace ParkingSystem.Payment.Application.Features.AuditLogs.Queries.GetAuditLogs;

public sealed record GetAuditLogsQuery(
    int Page = 1,
    int PageSize = 20,
    string? ActionType = null,
    Guid? ParkingLotId = null,
    Guid? ActorId = null,
    DateTime? FromUtc = null,
    DateTime? ToUtc = null) : IRequest<Result<AuditLogsPagedDto>>;
