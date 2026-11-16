using MediatR;
using ParkingSystem.BuildingBlocks.Results;

namespace ParkingSystem.Identity.Application.Accounts.Admin.GetAuditLogs;

public sealed record GetIdentityAuditLogsQuery(
    int Page = 1,
    int PageSize = 20,
    string? ActionType = null,
    Guid? ParkingLotId = null,
    Guid? ActorId = null,
    DateTime? FromUtc = null,
    DateTime? ToUtc = null) : IRequest<Result<AuditLogsPagedDto>>;
