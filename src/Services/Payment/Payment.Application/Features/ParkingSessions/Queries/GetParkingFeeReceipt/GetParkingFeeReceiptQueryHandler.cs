using MediatR;
using ParkingSystem.BuildingBlocks.Results;
using ParkingSystem.Payment.Application.Common.Authorization;
using ParkingSystem.Payment.Application.Common.Constants;
using ParkingSystem.Payment.Application.Common.Interfaces;

namespace ParkingSystem.Payment.Application.Features.ParkingSessions.Queries.GetParkingFeeReceipt;

public sealed class GetParkingFeeReceiptQueryHandler(
    IParkingFeePaymentRepository paymentRepository,
    IParkingSessionGateway parkingGateway,
    IParkingLotMembershipGateway membershipGateway)
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

        if (!await ParkingFeePaymentReadAccess.CanReadAsync(
                payment, query.CallerAccountId, query.CallerRole, membershipGateway, cancellationToken))
        {
            return Result<ParkingFeeReceiptResponse>.Failure(PayParkingFeeErrors.SessionAccessDenied);
        }

        DateTimeOffset? entryTimeUtc = null;
        int? durationMinutes = null;
        long? unitPrice = null;
        string? pricingMethod = null;
        int? blockMinutes = null;

        var contextResult = await parkingGateway.GetFeeContextAsync(
            payment.SessionId, query.CallerAccountId, query.CallerRole, cancellationToken);

        if (contextResult.IsSuccess && contextResult.Value is not null)
        {
            entryTimeUtc = contextResult.Value.EntryTimeUtc;
            var duration = payment.PaymentTimeUtc - contextResult.Value.EntryTimeUtc;
            durationMinutes = (int)Math.Max(0, duration.TotalMinutes);

            // Lấy thông tin đơn giá từ bảng giá gốc
            if (contextResult.Value.Pricing is not null)
            {
                unitPrice = contextResult.Value.Pricing.PricePerUnit;
                pricingMethod = contextResult.Value.Pricing.CalculationMethod;
                blockMinutes = contextResult.Value.Pricing.BlockMinutes;
            }
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
            TransactionReference: txnRef,
            UnitPrice: unitPrice,
            PricingCalculationMethod: pricingMethod,
            BlockMinutes: blockMinutes,
            ExitTimeUtc: payment.PaymentTimeUtc));
    }
}
