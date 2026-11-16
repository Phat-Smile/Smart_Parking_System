using MediatR;
using ParkingSystem.BuildingBlocks.Results;

namespace ParkingSystem.Parking.Application.ParkingLots.GetAuditLogs;

public sealed class GetParkingAuditLogsQueryHandler(IParkingAuditLogQueryRepository repository)
    : IRequestHandler<GetParkingAuditLogsQuery, Result<AuditLogsPagedDto>>
{
    public async Task<Result<AuditLogsPagedDto>> Handle(GetParkingAuditLogsQuery request, CancellationToken cancellationToken)
    {
        if (request.Page < 1)
            return Result<AuditLogsPagedDto>.Failure(new Error("parking.audit.invalid_page", "Page must be greater than or equal to 1."));

        if (request.PageSize is < 1 or > 100)
            return Result<AuditLogsPagedDto>.Failure(new Error("parking.audit.invalid_page_size", "PageSize must be between 1 and 100."));

        var filter = new ParkingAuditLogFilter(
            request.Page,
            request.PageSize,
            request.ActionType,
            request.ParkingLotId,
            request.ActorId,
            request.FromUtc,
            request.ToUtc);

        var result = await repository.GetAuditLogsAsync(filter, cancellationToken);
        return Result<AuditLogsPagedDto>.Success(result);
    }
}
