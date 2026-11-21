using MediatR;
using ParkingSystem.BuildingBlocks.Results;
using ParkingSystem.BuildingBlocks.Time;
using ParkingSystem.Payment.Application.Common.Constants;
using ParkingSystem.Payment.Application.Common.Interfaces;
using ParkingSystem.Payment.Domain.Constants;
using ParkingSystem.Payment.Domain.Entities;

namespace ParkingSystem.Payment.Application.Features.RefundRequests.Commands.CreateRefundRequest;

public sealed class CreateRefundRequestCommandHandler(
    IWalletRepository walletRepository,
    IRefundRequestRepository refundRequestRepository,
    IAuditWriter auditWriter,
    IClock clock) : IRequestHandler<CreateRefundRequestCommand, Result<CreateRefundRequestResponse>>
{
    private const string CustomerRole = "Customer";

    public async Task<Result<CreateRefundRequestResponse>> Handle(
        CreateRefundRequestCommand request,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(request.CallerRole, CustomerRole, StringComparison.OrdinalIgnoreCase))
        {
            return Result<CreateRefundRequestResponse>.Failure(RefundRequestErrors.CustomerOnly);
        }

        var wallet = await walletRepository.GetByUserIdAsync(request.CallerId, cancellationToken);
        if (wallet is null)
        {
            return Result<CreateRefundRequestResponse>.Failure(RefundRequestErrors.WalletNotFound);
        }

        var refundRequest = RefundRequest.Create(
            walletId: wallet.Id,
            lotId: request.LotId,
            amount: request.Amount,
            reason: request.Reason,
            createdBy: request.CallerId,
            referenceType: request.ReferenceType,
            referenceId: request.ReferenceId,
            now: clock.UtcNow);

        await refundRequestRepository.AddAsync(refundRequest, cancellationToken);

        await auditWriter.WriteAsync(new AuditEntryInput(
            ActionType: AuditActionCatalog.RefundRequestCreated,
            TargetType: "RefundRequest",
            TargetId: refundRequest.Id.ToString(),
            Reason: refundRequest.Reason,
            ParkingLotId: refundRequest.LotId,
            Details: new Dictionary<string, object?>
            {
                ["walletId"] = wallet.Id,
                ["amount"] = refundRequest.Amount,
                ["referenceType"] = refundRequest.ReferenceType?.ToString(),
                ["referenceId"] = refundRequest.ReferenceId
            }), cancellationToken);

        await refundRequestRepository.SaveChangesAsync(cancellationToken);

        return Result<CreateRefundRequestResponse>.Success(
            new CreateRefundRequestResponse(refundRequest.Id, refundRequest.Status.ToString()));
    }
}
