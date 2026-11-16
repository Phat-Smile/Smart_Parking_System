using MediatR;
using ParkingSystem.BuildingBlocks.Results;
using ParkingSystem.Identity.Application.Common.Interfaces;

namespace ParkingSystem.Identity.Application.Accounts.Admin.GetAuditLogs;

public sealed class GetIdentityAuditLogsQueryHandler(IIdentityAuditLogQueryRepository repository)
    : IRequestHandler<GetIdentityAuditLogsQuery, Result<AuditLogsPagedDto>>
{
    public async Task<Result<AuditLogsPagedDto>> Handle(GetIdentityAuditLogsQuery request, CancellationToken cancellationToken)
    {
        if (request.Page < 1)
            return Result<AuditLogsPagedDto>.Failure(new Error("identity.audit.invalid_page", "Page must be greater than or equal to 1."));

        if (request.PageSize is < 1 or > 100)
            return Result<AuditLogsPagedDto>.Failure(new Error("identity.audit.invalid_page_size", "PageSize must be between 1 and 100."));

        var filter = new IdentityAuditLogFilter(
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
