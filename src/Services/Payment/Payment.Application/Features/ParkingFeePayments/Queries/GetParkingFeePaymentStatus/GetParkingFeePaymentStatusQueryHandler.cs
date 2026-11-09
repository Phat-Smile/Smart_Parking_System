using MediatR;
using ParkingSystem.BuildingBlocks.Results;
using ParkingSystem.Payment.Application.Common.Constants;
using ParkingSystem.Payment.Application.Common.Interfaces;
using ParkingSystem.Payment.Domain.Constants;

namespace ParkingSystem.Payment.Application.Features.ParkingFeePayments.Queries.GetParkingFeePaymentStatus;

// Xử lý tra cứu trạng thái thanh toán tiền gửi xe
public sealed class GetParkingFeePaymentStatusQueryHandler(
    IParkingFeePaymentRepository paymentRepository)
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
        DateTimeOffset? paymentTime = null;
        DateTimeOffset? exitWindow = null;
        if (payment.Status == PaymentStatus.Succeeded)
        {
            paymentTime = payment.PaymentTimeUtc;
            exitWindow = payment.ExitWindowExpiresUtc;
        }

        var response = new GetParkingFeePaymentStatusResponse(
            payment.Id,
            payment.SessionId,
            payment.PaymentMethod,
            payment.Status,
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
