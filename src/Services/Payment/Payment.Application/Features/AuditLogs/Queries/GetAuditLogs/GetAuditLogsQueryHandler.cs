using MediatR;
using ParkingSystem.BuildingBlocks.Results;
using ParkingSystem.Payment.Application.Common.Interfaces;

namespace ParkingSystem.Payment.Application.Features.AuditLogs.Queries.GetAuditLogs;

public sealed class GetAuditLogsQueryHandler(IAuditLogQueryRepository repository)
    : IRequestHandler<GetAuditLogsQuery, Result<AuditLogsPagedDto>>
{
    public async Task<Result<AuditLogsPagedDto>> Handle(GetAuditLogsQuery request, CancellationToken cancellationToken)
    {
        var filter = new AuditLogFilter(
            request.Page,
            request.PageSize,
            request.ActionType,
            request.ParkingLotId,
            request.ActorId,
            request.FromUtc,
            request.ToUtc);

        var pagedResult = await repository.GetAuditLogsAsync(filter, cancellationToken);
        return Result<AuditLogsPagedDto>.Success(pagedResult);
    }
}
