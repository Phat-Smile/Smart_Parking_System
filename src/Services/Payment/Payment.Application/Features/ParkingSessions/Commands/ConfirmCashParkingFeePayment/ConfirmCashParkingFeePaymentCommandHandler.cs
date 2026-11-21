using System.Text.Json;
using MediatR;
using ParkingSystem.BuildingBlocks.Results;
using ParkingSystem.BuildingBlocks.Time;
using ParkingSystem.Contracts.Events.Payment;
using ParkingSystem.Payment.Application.Common.Constants;
using ParkingSystem.Payment.Application.Common.Interfaces;
using ParkingSystem.Payment.Application.Common.Messaging.Outbox;
using ParkingSystem.Payment.Application.Common.Utilities;
using ParkingSystem.Payment.Domain.Constants;
using ParkingSystem.Payment.Domain.Entities;
using ParkingSystem.Payment.Domain.Pricing;

namespace ParkingSystem.Payment.Application.Features.ParkingSessions.Commands.ConfirmCashParkingFeePayment;

// Xử lý nhân viên xác nhận thu tiền mặt tại bãi
public sealed class ConfirmCashParkingFeePaymentCommandHandler(
    IParkingSessionGateway parkingGateway,
    IParkingLotMembershipGateway membershipGateway,
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

            // Bản ghi cũ đã thất bại hoặc quá hạn khóa: giải phóng để thử lại
            await idempotencyStore.ReleasePendingAsync(existingRecord.Id, CancellationToken.None);
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

        // Kiểm tra quyền thành viên tại bãi đỗ của phiên xe
        var membershipResult = await membershipGateway.GetMembershipAsync(
            ctx.ParkingLotId, request.StaffAccountId, cancellationToken);

        if (!membershipResult.IsSuccess)
        {
            return Result<ConfirmCashParkingFeePaymentResponse>.Failure(
                membershipResult.Error ?? PayParkingFeeErrors.ParkingServiceUnavailable);
        }

        var membership = membershipResult.Value!;
        var hasAccess = membership.IsMember && (
            (isStaff && (string.Equals(membership.Role, "Staff", StringComparison.OrdinalIgnoreCase)
                         || string.Equals(membership.Role, "Owner", StringComparison.OrdinalIgnoreCase)))
            || (isOwner && string.Equals(membership.Role, "Owner", StringComparison.OrdinalIgnoreCase))
        );

        if (!hasAccess)
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
                await idempotencyStore.ReleasePendingAsync(pendingRecord.Id, CancellationToken.None);
                return Result<ConfirmCashParkingFeePaymentResponse>.Failure(PayParkingFeeErrors.SessionNotActive);
            }

            // Kiểm tra xe đã trả tiền trước đó (BR-09.d: cho phép thu phụ phí nếu phiên bị reopen do quá giờ)
            var hasSucceeded = await paymentRepository.HasSucceededPaymentAsync(request.SessionId, cancellationToken);
            var isOverstayPayment = hasSucceeded && ctx.LastPaidAtUtc.HasValue;

            if (hasSucceeded && !isOverstayPayment)
            {
                await idempotencyStore.ReleasePendingAsync(pendingRecord.Id, CancellationToken.None);
                return Result<ConfirmCashParkingFeePaymentResponse>.Failure(PayParkingFeeErrors.SessionAlreadyPaid);
            }

            // Chặn thu trùng khi bãi chưa kịp nhận tin nhắn mở cổng
            var paymentSequence = 1;
            if (isOverstayPayment)
            {
                var latestSucceeded = await paymentRepository.GetSucceededBySessionIdAsync(request.SessionId, cancellationToken);
                if (latestSucceeded != null)
                {
                    var latestPaidAtUtc = latestSucceeded.ConfirmedAtUtc ?? latestSucceeded.PaymentTimeUtc;
                    if (latestPaidAtUtc > ctx.LastPaidAtUtc!.Value.AddSeconds(1))
                    {
                        await idempotencyStore.ReleasePendingAsync(pendingRecord.Id, CancellationToken.None);
                        return Result<ConfirmCashParkingFeePaymentResponse>.Failure(PayParkingFeeErrors.SessionAlreadyPaid);
                    }

                    paymentSequence = latestSucceeded.PaymentSequence + 1;
                }
            }

            // Chỉ chặn khi có mã QR cùng đợt và còn hạn quét
            var nowForPendingCheck = clock.UtcNow;
            var pendingPayment = await paymentRepository.GetPendingBySessionIdAsync(request.SessionId, cancellationToken);
            if (pendingPayment != null)
            {
                var isActiveCurrentCycle = pendingPayment.PaymentSequence == paymentSequence
                    && (!pendingPayment.ExpiresAtUtc.HasValue || pendingPayment.ExpiresAtUtc.Value > nowForPendingCheck);
                if (isActiveCurrentCycle)
                {
                    await idempotencyStore.ReleasePendingAsync(pendingRecord.Id, CancellationToken.None);
                    return Result<ConfirmCashParkingFeePaymentResponse>.Failure(PayParkingFeeErrors.PaymentAlreadyInProgress);
                }

                // Hủy mã QR cũ hoặc hết hạn để không chặn tiền mặt
                pendingPayment.MarkAsSuperseded();
            }

            // Tính tiền gửi xe theo biểu phí
            var calculationTime = clock.UtcNow;
            var isPerTurn = string.Equals(ctx.Pricing.CalculationMethod, "PerTurn", StringComparison.OrdinalIgnoreCase)
                || string.Equals(ctx.Pricing.CalculationMethod, "PerEntry", StringComparison.OrdinalIgnoreCase);
            var parkingMethod = isPerTurn ? ParkingMethod.PerTurn : ParkingMethod.PerBlock;

            long totalFee;
            long prepaidAmount = 0;
            long actualAmountDue;

            if (isOverstayPayment)
            {
                var exitTime = ctx.ArrivedAtExitUtc ?? calculationTime;
                var overstayFee = PricingEngine.CalculateOverstayFee(
                    ctx.LastPaidAtUtc!.Value,
                    exitTime,
                    parkingMethod,
                    ctx.Pricing.BlockMinutes,
                    ctx.Pricing.PricePerUnit,
                    overnightFee: 0m,
                    overnightTimeMark: TimeSpan.Zero,
                    gracePeriodMinutes: ctx.GracePeriodMinutes);

                totalFee = (long)overstayFee;
                actualAmountDue = totalFee;
            }
            else
            {
                var calculation = ParkingFeeCalculator.Calculate(
                    parkingMethod,
                    ctx.Pricing.BlockMinutes,
                    ctx.Pricing.PricePerUnit,
                    ctx.EntryTimeUtc,
                    calculationTime);

                totalFee = calculation.TotalFee;

                if (ctx.BookingId.HasValue && ctx.AccountId.HasValue)
                {
                    prepaidAmount = await prepaymentReader.GetPrepaidAmountAsync(
                        ctx.AccountId.Value, ctx.BookingId.Value, cancellationToken);
                }

                actualAmountDue = Math.Max(0, totalFee - prepaidAmount);
            }

            if (actualAmountDue != request.ExpectedAmountDue)
            {
                await idempotencyStore.ReleasePendingAsync(pendingRecord.Id, CancellationToken.None);
                return Result<ConfirmCashParkingFeePaymentResponse>.Failure(PayParkingFeeErrors.FeeChanged);
            }

            // Tạo hóa đơn tiền mặt mới bất biến (không sửa chứng từ cũ)
            var exitWindowExpiresUtc = calculationTime.AddMinutes(15);
            var payment = ParkingFeePayment.CreateCashPayment(
                request.SessionId,
                ctx.ParkingLotId,
                ctx.AccountId,
                ctx.PlateNumber,
                ctx.VehicleType,
                totalFee,
                isOverstayPayment ? 0 : prepaidAmount,
                actualAmountDue,
                calculationTime,
                exitWindowExpiresUtc,
                paymentSequence);

            await paymentRepository.AddAsync(payment, cancellationToken);
            var paymentId = payment.Id;

            // Ghi nhật ký kiểm toán thu tiền mặt
            var auditReason = string.IsNullOrWhiteSpace(request.Reason)
                ? "Khách thanh toán tiền mặt tại bãi"
                : request.Reason.Trim();

            var auditDetails = new Dictionary<string, object?>
            {
                ["paymentId"] = paymentId,
                ["paymentSequence"] = paymentSequence,
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
                PaymentId: paymentId,
                AmountPaid: actualAmountDue,
                PaidAtUtc: calculationTime,
                ExitWindowExpiresUtc: exitWindowExpiresUtc);

            outboxWriter.Enqueue(
                integrationEvent: paidEvent,
                eventType: "payment.session-paid.v1",
                eventVersion: 1,
                correlationId: paymentId);

            var invoiceEvent = new ElectronicInvoiceIssuedIntegrationEvent(
                SessionId: payment.SessionId,
                PaymentId: payment.Id,
                ParkingLotId: payment.ParkingLotId,
                MaskedPlateNumber: LicensePlateMasker.MaskPlateNumber(payment.PlateNumber),
                TotalFee: payment.TotalFee,
                PaidAmount: payment.AmountPaid,
                AmountDue: Math.Max(0, payment.TotalFee - payment.PrepaidAmount - payment.AmountPaid),
                PaymentTimeUtc: payment.PaymentTimeUtc,
                ExitWindowExpiresUtc: payment.ExitWindowExpiresUtc ?? payment.PaymentTimeUtc,
                AccountId: payment.AccountId);

            outboxWriter.Enqueue(
                integrationEvent: invoiceEvent,
                eventType: "payment.electronic-invoice-issued.v1",
                eventVersion: 1,
                correlationId: paymentId);

            var response = new ConfirmCashParkingFeePaymentResponse(
                TransactionId: paymentId,
                SessionId: request.SessionId,
                AmountPaid: actualAmountDue,
                PaymentMethod: PaymentMethod.Cash,
                ConfirmedByStaffId: request.StaffAccountId,
                ParkingLotId: ctx.ParkingLotId,
                PaymentTimeUtc: calculationTime,
                ExitWindowExpiresUtc: exitWindowExpiresUtc);

            try
            {
                // Đánh dấu giao dịch thành công
                pendingRecord.MarkSuccess(JsonSerializer.Serialize(response), 200);
                await idempotencyStore.UpdateAsync(pendingRecord, cancellationToken);
                await paymentRepository.SaveChangesAsync(cancellationToken);

                return Result<ConfirmCashParkingFeePaymentResponse>.Success(response);
            }
            catch (Exception ex) when (ex.GetType().Name is "DbUpdateException" or "DbUpdateConcurrencyException" or "PostgresException")
            {
                // Xóa dữ liệu tạm trước khi đọc lại trạng thái mới nhất
                paymentRepository.ClearChangeTracker();

                var latest = await paymentRepository.GetSucceededBySessionIdAsync(request.SessionId, CancellationToken.None);
                if (latest != null && latest.PaymentSequence >= paymentSequence)
                {
                    await idempotencyStore.ReleasePendingAsync(pendingRecord.Id, CancellationToken.None);
                    return Result<ConfirmCashParkingFeePaymentResponse>.Failure(PayParkingFeeErrors.SessionAlreadyPaid);
                }

                if (ex.GetType().Name == "DbUpdateConcurrencyException")
                {
                    var latestPending = await paymentRepository.GetPendingBySessionIdAsync(request.SessionId, CancellationToken.None);
                    var hasActiveCurrentQr = latestPending != null
                        && latestPending.PaymentSequence == paymentSequence
                        && (!latestPending.ExpiresAtUtc.HasValue || latestPending.ExpiresAtUtc.Value > clock.UtcNow);
                    if (hasActiveCurrentQr)
                    {
                        await idempotencyStore.ReleasePendingAsync(pendingRecord.Id, CancellationToken.None);
                        return Result<ConfirmCashParkingFeePaymentResponse>.Failure(PayParkingFeeErrors.PaymentAlreadyInProgress);
                    }
                }

                throw;
            }
        }
        catch
        {
            // Mở khóa nếu xảy ra lỗi bất ngờ
            await idempotencyStore.ReleasePendingAsync(pendingRecord.Id, CancellationToken.None);
            throw;
        }
    }
}
