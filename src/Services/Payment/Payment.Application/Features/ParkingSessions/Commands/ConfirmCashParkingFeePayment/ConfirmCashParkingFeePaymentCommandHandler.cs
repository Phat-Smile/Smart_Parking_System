using System.Text.Json;
using MediatR;
using ParkingSystem.BuildingBlocks.Results;
using ParkingSystem.BuildingBlocks.Time;
using ParkingSystem.Contracts.Events.Payment;
using ParkingSystem.Payment.Application.Common.Constants;
using ParkingSystem.Payment.Application.Common.Interfaces;
using ParkingSystem.Payment.Application.Common.Messaging.Outbox;
using ParkingSystem.Payment.Domain.Entities;
using ParkingSystem.Payment.Domain.Pricing;

namespace ParkingSystem.Payment.Application.Features.ParkingSessions.Commands.ConfirmCashParkingFeePayment;

// Xử lý nhân viên xác nhận thu tiền mặt tại bãi
public sealed class ConfirmCashParkingFeePaymentCommandHandler(
    IParkingSessionGateway parkingGateway,
    IPrepaymentReader prepaymentReader,
    IParkingFeePaymentRepository paymentRepository,
    IIdempotencyStore idempotencyStore,
    IAuditWriter auditWriter,
    IOutboxWriter outboxWriter,
    IClock clock)
    : IRequestHandler<ConfirmCashParkingFeePaymentCommand, Result<ConfirmCashParkingFeePaymentResponse>>
{
    private const string OperationName = "confirm_cash_payment";

    public async Task<Result<ConfirmCashParkingFeePaymentResponse>> Handle(
        ConfirmCashParkingFeePaymentCommand request,
        CancellationToken cancellationToken)
    {
        // Kiểm tra vai trò nhân viên hoặc chủ bãi
        var role = request.StaffRole ?? string.Empty;
        var isStaff = string.Equals(role, "Staff", StringComparison.OrdinalIgnoreCase);
        var isOwner = string.Equals(role, "ParkingLotOwner", StringComparison.OrdinalIgnoreCase)
            || string.Equals(role, "LotOwner", StringComparison.OrdinalIgnoreCase);

        if (!isStaff && !isOwner)
        {
            return Result<ConfirmCashParkingFeePaymentResponse>.Failure(PayParkingFeeErrors.RoleNotAllowedCash);
        }

        // Kiểm tra dữ liệu đầu vào hợp lệ
        if (request.SessionId == Guid.Empty)
        {
            return Result<ConfirmCashParkingFeePaymentResponse>.Failure(PayParkingFeeErrors.SessionIdInvalid);
        }

        if (string.IsNullOrWhiteSpace(request.IdempotencyKey) || !Guid.TryParse(request.IdempotencyKey, out _))
        {
            return Result<ConfirmCashParkingFeePaymentResponse>.Failure(PayParkingFeeErrors.IdempotencyKeyInvalid);
        }

        if (request.ExpectedAmountDue < 0)
        {
            return Result<ConfirmCashParkingFeePaymentResponse>.Failure(PayParkingFeeErrors.ExpectedAmountInvalid);
        }

        if (request.Reason != null && request.Reason.Length > 500)
        {
            return Result<ConfirmCashParkingFeePaymentResponse>.Failure(PayParkingFeeErrors.ReasonTooLong);
        }

        var requestHash = $"{request.SessionId}:{request.ExpectedAmountDue}";
        var nowUtc = clock.UtcNow;

        // Tra cứu khóa chống thu trùng tiền
        var existingRecord = await idempotencyStore.FindAsync(
            request.StaffAccountId, OperationName, request.IdempotencyKey, cancellationToken);

        if (existingRecord != null)
        {
            if (existingRecord.RequestHash != requestHash)
            {
                return Result<ConfirmCashParkingFeePaymentResponse>.Failure(PayParkingFeeErrors.IdempotencyKeyReused);
            }

            if (existingRecord.Status == "Success" && !string.IsNullOrWhiteSpace(existingRecord.ResponsePayload))
            {
                var cachedResponse = JsonSerializer.Deserialize<ConfirmCashParkingFeePaymentResponse>(existingRecord.ResponsePayload);
                if (cachedResponse != null)
                {
                    return Result<ConfirmCashParkingFeePaymentResponse>.Success(cachedResponse);
                }
            }

            if (existingRecord.Status == "Pending" && existingRecord.LockedUntilUtc > nowUtc)
            {
                return Result<ConfirmCashParkingFeePaymentResponse>.Failure(PayParkingFeeErrors.RequestInProgress);
            }
        }

        // Lấy thông tin phiên xe từ bãi đỗ
        var contextResult = await parkingGateway.GetFeeContextAsync(
            request.SessionId, request.StaffAccountId, role, cancellationToken);

        if (!contextResult.IsSuccess || contextResult.Value == null)
        {
            return Result<ConfirmCashParkingFeePaymentResponse>.Failure(
                contextResult.Error ?? PayParkingFeeErrors.SessionNotFound);
        }

        var ctx = contextResult.Value;

        // Chặn nhân viên xác nhận bãi đỗ khác
        if (isStaff && (!request.StaffLotId.HasValue || request.StaffLotId.Value != ctx.ParkingLotId))
        {
            return Result<ConfirmCashParkingFeePaymentResponse>.Failure(PayParkingFeeErrors.LotAccessDenied);
        }

        // Khóa giao dịch đang xử lý
        var pendingRecord = PaymentIdempotencyRecord.CreatePending(
            request.StaffAccountId, OperationName, request.IdempotencyKey, requestHash, nowUtc);

        var lockAcquired = await idempotencyStore.TryCreatePendingAsync(pendingRecord, cancellationToken);
        if (!lockAcquired)
        {
            return Result<ConfirmCashParkingFeePaymentResponse>.Failure(PayParkingFeeErrors.RequestInProgress);
        }

        try
        {
            // Kiểm tra phiên xe còn đang gửi
            if (!string.Equals(ctx.Status, "Active", StringComparison.OrdinalIgnoreCase))
            {
                await MarkFailedAsync(pendingRecord, PayParkingFeeErrors.SessionNotActive, 409, cancellationToken);
                return Result<ConfirmCashParkingFeePaymentResponse>.Failure(PayParkingFeeErrors.SessionNotActive);
            }

            // Kiểm tra xe đã trả tiền trước đó
            var hasSucceeded = await paymentRepository.HasSucceededPaymentAsync(request.SessionId, cancellationToken);
            if (hasSucceeded)
            {
                await MarkFailedAsync(pendingRecord, PayParkingFeeErrors.SessionAlreadyPaid, 409, cancellationToken);
                return Result<ConfirmCashParkingFeePaymentResponse>.Failure(PayParkingFeeErrors.SessionAlreadyPaid);
            }

            // Kiểm tra đang có giao dịch xử lý dở
            var pendingPayment = await paymentRepository.GetPendingBySessionIdAsync(request.SessionId, cancellationToken);
            if (pendingPayment != null)
            {
                await MarkFailedAsync(pendingRecord, PayParkingFeeErrors.PaymentAlreadyInProgress, 409, cancellationToken);
                return Result<ConfirmCashParkingFeePaymentResponse>.Failure(PayParkingFeeErrors.PaymentAlreadyInProgress);
            }

            // Tính tiền gửi xe theo biểu phí
            var calculationTime = clock.UtcNow;
            var isPerTurn = string.Equals(ctx.Pricing.CalculationMethod, "PerTurn", StringComparison.OrdinalIgnoreCase)
                || string.Equals(ctx.Pricing.CalculationMethod, "PerEntry", StringComparison.OrdinalIgnoreCase);
            var parkingMethod = isPerTurn ? ParkingMethod.PerTurn : ParkingMethod.PerBlock;

            var calculation = ParkingFeeCalculator.Calculate(
                parkingMethod,
                ctx.Pricing.BlockMinutes,
                ctx.Pricing.PricePerUnit,
                ctx.EntryTimeUtc,
                calculationTime);

            long prepaidAmount = 0;
            if (ctx.BookingId.HasValue && ctx.AccountId.HasValue)
            {
                prepaidAmount = await prepaymentReader.GetPrepaidAmountAsync(
                    ctx.AccountId.Value, ctx.BookingId.Value, cancellationToken);
            }

            var actualAmountDue = Math.Max(0, calculation.TotalFee - prepaidAmount);

            if (actualAmountDue != request.ExpectedAmountDue)
            {
                await MarkFailedAsync(pendingRecord, PayParkingFeeErrors.FeeChanged, 409, cancellationToken);
                return Result<ConfirmCashParkingFeePaymentResponse>.Failure(PayParkingFeeErrors.FeeChanged);
            }

            // Tạo hóa đơn thu tiền mặt
            var exitWindowExpiresUtc = calculationTime.AddMinutes(15);
            var payment = ParkingFeePayment.CreateCashPayment(
                request.SessionId,
                ctx.ParkingLotId,
                ctx.AccountId,
                ctx.PlateNumber,
                ctx.VehicleType,
                calculation.TotalFee,
                prepaidAmount,
                actualAmountDue,
                calculationTime,
                exitWindowExpiresUtc);

            await paymentRepository.AddAsync(payment, cancellationToken);

            // Ghi nhật ký kiểm toán thu tiền mặt
            var auditReason = string.IsNullOrWhiteSpace(request.Reason)
                ? "Khách thanh toán tiền mặt tại bãi"
                : request.Reason.Trim();

            var auditDetails = new Dictionary<string, object?>
            {
                ["paymentId"] = payment.Id,
                ["amountPaid"] = actualAmountDue,
                ["paymentMethod"] = "Cash",
                ["exitWindowExpiresUtc"] = exitWindowExpiresUtc
            };

            var auditEntry = new AuditEntryInput(
                ActionType: "CashPaymentConfirmed",
                TargetType: "ParkingSession",
                TargetId: request.SessionId.ToString(),
                Reason: auditReason,
                ParkingLotId: ctx.ParkingLotId,
                Details: auditDetails);

            await auditWriter.WriteAsync(auditEntry, cancellationToken);

            // Đưa sự kiện mở cổng vào hộp thư Outbox
            var paidEvent = new ParkingSessionPaidIntegrationEvent(
                SessionId: request.SessionId,
                PaymentId: payment.Id,
                AmountPaid: actualAmountDue,
                PaidAtUtc: calculationTime,
                ExitWindowExpiresUtc: exitWindowExpiresUtc);

            outboxWriter.Enqueue(
                integrationEvent: paidEvent,
                eventType: "payment.session-paid.v1",
                eventVersion: 1,
                correlationId: payment.Id);

            var response = new ConfirmCashParkingFeePaymentResponse(
                TransactionId: payment.Id,
                SessionId: request.SessionId,
                AmountPaid: actualAmountDue,
                PaymentMethod: payment.PaymentMethod,
                ConfirmedByStaffId: request.StaffAccountId,
                ParkingLotId: ctx.ParkingLotId,
                PaymentTimeUtc: calculationTime,
                ExitWindowExpiresUtc: exitWindowExpiresUtc);

            // Đánh dấu giao dịch thành công
            pendingRecord.MarkSuccess(JsonSerializer.Serialize(response), 200);
            await idempotencyStore.UpdateAsync(pendingRecord, cancellationToken);
            await paymentRepository.SaveChangesAsync(cancellationToken);

            return Result<ConfirmCashParkingFeePaymentResponse>.Success(response);
        }
        catch
        {
            // Mở khóa nếu xảy ra lỗi bất ngờ
            await idempotencyStore.ReleasePendingAsync(pendingRecord.Id, cancellationToken);
            throw;
        }
    }

    // Ghi nhận lỗi vào khóa chống trùng
    private async Task MarkFailedAsync(
        PaymentIdempotencyRecord record,
        Error error,
        int statusCode,
        CancellationToken ct)
    {
        record.MarkFailed(error.Message, statusCode);
        await idempotencyStore.UpdateAsync(record, ct);
    }
}
