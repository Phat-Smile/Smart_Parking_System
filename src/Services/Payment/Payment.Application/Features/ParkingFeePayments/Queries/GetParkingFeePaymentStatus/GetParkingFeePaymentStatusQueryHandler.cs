using MediatR;
using ParkingSystem.BuildingBlocks.Results;
using ParkingSystem.Payment.Application.Common.Authorization;
using ParkingSystem.Payment.Application.Common.Constants;
using ParkingSystem.Payment.Application.Common.Interfaces;
using ParkingSystem.Payment.Domain.Constants;

namespace ParkingSystem.Payment.Application.Features.ParkingFeePayments.Queries.GetParkingFeePaymentStatus;

// Xử lý tra cứu trạng thái thanh toán tiền gửi xe
public sealed class GetParkingFeePaymentStatusQueryHandler(
    IParkingFeePaymentRepository paymentRepository,
    IParkingLotMembershipGateway membershipGateway)
    : IRequestHandler<GetParkingFeePaymentStatusQuery, Result<GetParkingFeePaymentStatusResponse>>
{
    public async Task<Result<GetParkingFeePaymentStatusResponse>> Handle(
        GetParkingFeePaymentStatusQuery request,
        CancellationToken ct)
    {
        if (request.TransactionId == Guid.Empty)
        {
            return Result<GetParkingFeePaymentStatusResponse>.Failure(
                ParkingFeePaymentErrors.TransactionIdInvalid);
        }

        var payment = await paymentRepository.GetByIdAsync(request.TransactionId, ct);
        if (payment is null)
        {
            return Result<GetParkingFeePaymentStatusResponse>.Failure(
                ParkingFeePaymentErrors.TransactionNotFound);
        }

        if (!await ParkingFeePaymentReadAccess.CanReadAsync(
                payment, request.CallerAccountId, request.CallerRole, membershipGateway, ct))
        {
            return Result<GetParkingFeePaymentStatusResponse>.Failure(PayParkingFeeErrors.SessionAccessDenied);
        }

        // Ẩn link QR khi đã hết hạn hoặc không còn chờ thanh toán
        string? paymentUrl = null;
        if (payment.Status == PaymentStatus.Pending
            && payment.ExpiresAtUtc.HasValue
            && payment.ExpiresAtUtc.Value > DateTimeOffset.UtcNow
            && !string.IsNullOrWhiteSpace(payment.PaymentUrl))
        {
            paymentUrl = payment.PaymentUrl;
        }

        // Chỉ hiện giờ chốt phí và hạn ra cổng khi đã trả xong
        var status = payment.Status;
        DateTimeOffset? paymentTime = null;
        DateTimeOffset? exitWindow = null;
        if (payment.Status == PaymentStatus.Succeeded)
        {
            paymentTime = payment.PaymentTimeUtc;
            exitWindow = payment.ExitWindowExpiresUtc;
        }
        else if (payment.Status == PaymentStatus.Superseded)
        {
            // Kiểm tra nếu là mã overstay đã gộp thành công vào hóa đơn phiên
            var sessionPayment = await paymentRepository.GetSucceededBySessionIdAsync(payment.SessionId, ct);
            if (sessionPayment != null && sessionPayment.ExitWindowExpiresUtc.HasValue && sessionPayment.ExitWindowExpiresUtc.Value > payment.PaymentTimeUtc)
            {
                status = PaymentStatus.Succeeded;
                paymentTime = sessionPayment.PaymentTimeUtc;
                exitWindow = sessionPayment.ExitWindowExpiresUtc;
            }
        }

        var response = new GetParkingFeePaymentStatusResponse(
            payment.Id,
            payment.SessionId,
            payment.PaymentMethod,
            status,
            payment.AmountPaid,
            paymentUrl,
            payment.ExpiresAtUtc,
            paymentTime,
            exitWindow,
            payment.ConfirmedAtUtc,
            payment.FailureCode);

        return Result<GetParkingFeePaymentStatusResponse>.Success(response);
    }
}
