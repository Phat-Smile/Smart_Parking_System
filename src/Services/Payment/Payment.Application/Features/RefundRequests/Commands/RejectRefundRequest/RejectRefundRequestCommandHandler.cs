using MediatR;
using ParkingSystem.BuildingBlocks.Results;
using ParkingSystem.BuildingBlocks.Time;
using ParkingSystem.Payment.Application.Common.Constants;
using ParkingSystem.Payment.Application.Common.Interfaces;
using ParkingSystem.Payment.Domain.Constants;
using ParkingSystem.Payment.Domain.Enums;

namespace ParkingSystem.Payment.Application.Features.RefundRequests.Commands.RejectRefundRequest;

public sealed class RejectRefundRequestCommandHandler(
    IRefundRequestRepository refundRequestRepository,
    IAuditWriter auditWriter,
    IClock clock) : IRequestHandler<RejectRefundRequestCommand, Result<RejectRefundRequestResponse>>
{
    private const string AdminRole = "SystemAdmin";

    public async Task<Result<RejectRefundRequestResponse>> Handle(
        RejectRefundRequestCommand request,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(request.AdminRole, AdminRole, StringComparison.OrdinalIgnoreCase))
        {
            return Result<RejectRefundRequestResponse>.Failure(RefundRequestErrors.ForbiddenRole);
        }

        var refundRequest = await refundRequestRepository.GetByIdAsync(request.Id, cancellationToken);
        if (refundRequest is null)
        {
            return Result<RejectRefundRequestResponse>.Failure(RefundRequestErrors.NotFound);
        }

        // Separation of duties: Creator cannot be the reviewer
        if (refundRequest.CreatedBy == request.AdminId)
        {
            return Result<RejectRefundRequestResponse>.Failure(RefundRequestErrors.CreatorCannotReview);
        }

        // Decision D2: Owner must confirm before Admin can reject!
        if (refundRequest.OwnerConfirmedBy is null)
        {
            return Result<RejectRefundRequestResponse>.Failure(RefundRequestErrors.OwnerNotConfirmed);
        }

        if (refundRequest.Status == RefundStatus.Rejected || refundRequest.ReviewedBy.HasValue)
        {
            return Result<RejectRefundRequestResponse>.Failure(RefundRequestErrors.AlreadyReviewed);
        }

        if (refundRequest.Status != RefundStatus.Requested)
        {
            return Result<RejectRefundRequestResponse>.Failure(RefundRequestErrors.InvalidStatus);
        }

        refundRequest.Reject(request.AdminId, request.Reason, clock.UtcNow);

        // Audit log: Admin's rejection reason is recorded in audit_logs.reason
        await auditWriter.WriteAsync(new AuditEntryInput(
            ActionType: AuditActionCatalog.RefundRequestRejected,
            TargetType: "RefundRequest",
            TargetId: refundRequest.Id.ToString(),
            Reason: request.Reason.Trim(),
            ParkingLotId: refundRequest.LotId,
            Details: new Dictionary<string, object?>
            {
                ["adminId"] = request.AdminId,
                ["rejectionReason"] = request.Reason.Trim()
            }), cancellationToken);

        try
        {
            await refundRequestRepository.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex) when (ex.GetType().Name == "DbUpdateConcurrencyException")
        {
            await refundRequestRepository.ReloadAsync(refundRequest, cancellationToken);
            return Result<RejectRefundRequestResponse>.Failure(RefundRequestErrors.ConcurrencyConflict);
        }

        // Wallet balance and ledger entries are unchanged
        return Result<RejectRefundRequestResponse>.Success(
            new RejectRefundRequestResponse(
                refundRequest.Id,
                refundRequest.Status.ToString(),
                refundRequest.ReviewedBy,
                refundRequest.ReviewedAt));
    }
}
