using MediatR;
using ParkingSystem.BuildingBlocks.Results;

namespace ParkingSystem.Parking.Application.ParkingLots.GetAuditLogs;

public sealed record GetParkingAuditLogsQuery(
    int Page = 1,
    int PageSize = 20,
    string? ActionType = null,
    Guid? ParkingLotId = null,
    Guid? ActorId = null,
    DateTime? FromUtc = null,
    DateTime? ToUtc = null) : IRequest<Result<AuditLogsPagedDto>>;
