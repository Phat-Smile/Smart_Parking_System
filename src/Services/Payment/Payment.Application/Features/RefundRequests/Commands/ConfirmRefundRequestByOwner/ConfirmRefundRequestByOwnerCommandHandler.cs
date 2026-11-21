using MediatR;
using ParkingSystem.BuildingBlocks.Results;
using ParkingSystem.BuildingBlocks.Time;
using ParkingSystem.Payment.Application.Common.Constants;
using ParkingSystem.Payment.Application.Common.Interfaces;
using ParkingSystem.Payment.Domain.Constants;
using ParkingSystem.Payment.Domain.Enums;

namespace ParkingSystem.Payment.Application.Features.RefundRequests.Commands.ConfirmRefundRequestByOwner;

public sealed class ConfirmRefundRequestByOwnerCommandHandler(
    IRefundRequestRepository refundRequestRepository,
    IParkingLotMembershipGateway membershipGateway,
    IAuditWriter auditWriter,
    IClock clock) : IRequestHandler<ConfirmRefundRequestByOwnerCommand, Result<ConfirmRefundRequestByOwnerResponse>>
{
    private static readonly string[] OwnerRoles = ["ParkingLotOwner", "LotOwner"];

    public async Task<Result<ConfirmRefundRequestByOwnerResponse>> Handle(
        ConfirmRefundRequestByOwnerCommand request,
        CancellationToken cancellationToken)
    {
        var isOwner = OwnerRoles.Any(r => string.Equals(request.CallerRole, r, StringComparison.OrdinalIgnoreCase));
        if (!isOwner)
        {
            return Result<ConfirmRefundRequestByOwnerResponse>.Failure(RefundRequestErrors.ForbiddenRole);
        }

        var refundRequest = await refundRequestRepository.GetByIdAsync(request.Id, cancellationToken);
        if (refundRequest is null)
        {
            return Result<ConfirmRefundRequestByOwnerResponse>.Failure(RefundRequestErrors.NotFound);
        }

        var membership = await membershipGateway.GetMembershipAsync(refundRequest.LotId, request.CallerId, cancellationToken);
        if (!membership.IsSuccess || membership.Value is null)
        {
            return Result<ConfirmRefundRequestByOwnerResponse>.Failure(RefundRequestErrors.ParkingServiceUnavailable);
        }

        if (!membership.Value.IsMember || !string.Equals(membership.Value.Role, "Owner", StringComparison.OrdinalIgnoreCase))
        {
            return Result<ConfirmRefundRequestByOwnerResponse>.Failure(RefundRequestErrors.ForbiddenLotAccess);
        }

        if (refundRequest.Status != RefundStatus.Requested)
        {
            return Result<ConfirmRefundRequestByOwnerResponse>.Failure(RefundRequestErrors.InvalidStatus);
        }

        if (refundRequest.OwnerConfirmedBy.HasValue)
        {
            return Result<ConfirmRefundRequestByOwnerResponse>.Failure(RefundRequestErrors.AlreadyConfirmed);
        }

        refundRequest.ConfirmByOwner(request.CallerId, clock.UtcNow);

        await auditWriter.WriteAsync(new AuditEntryInput(
            ActionType: AuditActionCatalog.RefundRequestOwnerConfirmed,
            TargetType: "RefundRequest",
            TargetId: refundRequest.Id.ToString(),
            Reason: "Chủ bãi xác nhận số liệu yêu cầu hoàn tiền",
            ParkingLotId: refundRequest.LotId,
            Details: new Dictionary<string, object?>
            {
                ["ownerId"] = request.CallerId
            }), cancellationToken);

        try
        {
            await refundRequestRepository.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex) when (ex.GetType().Name == "DbUpdateConcurrencyException")
        {
            await refundRequestRepository.ReloadAsync(refundRequest, cancellationToken);
            return Result<ConfirmRefundRequestByOwnerResponse>.Failure(RefundRequestErrors.ConcurrencyConflict);
        }

        return Result<ConfirmRefundRequestByOwnerResponse>.Success(
            new ConfirmRefundRequestByOwnerResponse(
                refundRequest.Id,
                refundRequest.Status.ToString(),
                refundRequest.OwnerConfirmedBy));
    }
}
