using MediatR;
using ParkingSystem.BuildingBlocks.Results;
using ParkingSystem.Payment.Application.Common.Constants;
using ParkingSystem.Payment.Application.Common.Interfaces;

namespace ParkingSystem.Payment.Application.Features.ParkingSessions.Queries.GetParkingFeeReceipt;

public sealed class GetParkingFeeReceiptQueryHandler(
    IParkingFeePaymentRepository paymentRepository,
    IParkingSessionGateway parkingGateway)
    : IRequestHandler<GetParkingFeeReceiptQuery, Result<ParkingFeeReceiptResponse>>
{
    public async Task<Result<ParkingFeeReceiptResponse>> Handle(
        GetParkingFeeReceiptQuery query,
        CancellationToken cancellationToken)
    {
        if (query.Identifier == Guid.Empty)
        {
            return Result<ParkingFeeReceiptResponse>.Failure(ParkingFeePaymentErrors.TransactionIdInvalid);
        }

        var payment = await paymentRepository.GetReceiptPaymentAsync(query.Identifier, cancellationToken);
        if (payment is null)
        {
            return Result<ParkingFeeReceiptResponse>.Failure(ParkingFeePaymentErrors.TransactionNotFound);
        }

        var isPrivilegedRole = string.Equals(query.CallerRole, "Admin", StringComparison.OrdinalIgnoreCase) ||
                               string.Equals(query.CallerRole, "Staff", StringComparison.OrdinalIgnoreCase) ||
                               string.Equals(query.CallerRole, "Owner", StringComparison.OrdinalIgnoreCase);

        if (!isPrivilegedRole && payment.AccountId.HasValue && query.CallerAccountId.HasValue)
        {
            if (payment.AccountId.Value != query.CallerAccountId.Value)
            {
                return Result<ParkingFeeReceiptResponse>.Failure(PayParkingFeeErrors.SessionAccessDenied);
            }
        }

        DateTimeOffset? entryTimeUtc = null;
        int? durationMinutes = null;

        var contextResult = await parkingGateway.GetFeeContextAsync(
            payment.SessionId, query.CallerAccountId, query.CallerRole, cancellationToken);

        if (contextResult.IsSuccess && contextResult.Value is not null)
        {
            entryTimeUtc = contextResult.Value.EntryTimeUtc;
            var duration = payment.PaymentTimeUtc - contextResult.Value.EntryTimeUtc;
            durationMinutes = (int)Math.Max(0, duration.TotalMinutes);
        }

        var txnRef = !string.IsNullOrWhiteSpace(payment.GatewayTransactionNo)
            ? payment.GatewayTransactionNo
            : payment.TxnRef;

        return Result<ParkingFeeReceiptResponse>.Success(new ParkingFeeReceiptResponse(
            PaymentId: payment.Id,
            SessionId: payment.SessionId,
            ParkingLotId: payment.ParkingLotId,
            AccountId: payment.AccountId,
            PlateNumber: payment.PlateNumber,
            VehicleType: payment.VehicleType,
            PaymentMethod: payment.PaymentMethod,
            TotalFee: payment.TotalFee,
            PrepaidAmount: payment.PrepaidAmount,
            AmountPaid: payment.AmountPaid,
            PaymentTimeUtc: payment.PaymentTimeUtc,
            ExitWindowExpiresUtc: payment.ExitWindowExpiresUtc,
            Status: payment.Status,
            EntryTimeUtc: entryTimeUtc,
            DurationMinutes: durationMinutes,
            TransactionReference: txnRef));
    }
}
