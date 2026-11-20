using System.Text.Json;
using MediatR;
using ParkingSystem.BuildingBlocks.Results;
using ParkingSystem.BuildingBlocks.Time;
using ParkingSystem.Contracts.Events.Payment;
using ParkingSystem.Payment.Application.Common.Constants;
using ParkingSystem.Payment.Application.Common.Interfaces;
using ParkingSystem.Payment.Application.Common.Messaging.Outbox;
using ParkingSystem.Payment.Domain.Entities;
using ParkingSystem.Payment.Domain.Enums;
using ParkingSystem.Payment.Domain.Pricing;

namespace ParkingSystem.Payment.Application.Features.ParkingSessions.Commands.PayParkingFeeByWallet;

// Bộ điều phối thanh toán tiền đỗ xe qua ví
public sealed class PayParkingFeeByWalletCommandHandler(
    IParkingSessionGateway parkingGateway,
    IPrepaymentReader prepaymentReader,
    IWalletRepository walletRepository,
    IParkingFeePaymentRepository paymentRepository,
    IIdempotencyStore idempotencyStore,
    IOutboxWriter outboxWriter,
    IClock clock)
    : IRequestHandler<PayParkingFeeByWalletCommand, Result<PayParkingFeeByWalletResponse>>
{
    private const string OperationName = "pay_fee_wallet";

    public async Task<Result<PayParkingFeeByWalletResponse>> Handle(
        PayParkingFeeByWalletCommand request,
        CancellationToken cancellationToken)
    {
        // 1. Kiểm tra vai trò khách hàng
        if (!string.Equals(request.CallerRole, "Customer", StringComparison.OrdinalIgnoreCase))
        {
            return Result<PayParkingFeeByWalletResponse>.Failure(PayParkingFeeErrors.RoleNotAllowed);
        }

        // 2. Kiểm tra định dạng dữ liệu đầu vào
        if (request.SessionId == Guid.Empty)
        {
            return Result<PayParkingFeeByWalletResponse>.Failure(PayParkingFeeErrors.SessionIdInvalid);
        }

        if (string.IsNullOrWhiteSpace(request.IdempotencyKey) || !Guid.TryParse(request.IdempotencyKey, out _))
        {
            return Result<PayParkingFeeByWalletResponse>.Failure(PayParkingFeeErrors.IdempotencyKeyInvalid);
        }

        if (request.ExpectedAmountDue < 0)
        {
            return Result<PayParkingFeeByWalletResponse>.Failure(PayParkingFeeErrors.ExpectedAmountInvalid);
        }

        var requestHash = $"{request.SessionId}:{request.ExpectedAmountDue}";
        var nowUtc = clock.UtcNow;

        // 3. Tra cứu khóa chống trừ tiền lặp lại
        var existingRecord = await idempotencyStore.FindAsync(
            request.CallerAccountId, OperationName, request.IdempotencyKey, cancellationToken);

        if (existingRecord != null)
        {
            // Trùng khóa nhưng khác nội dung giao dịch
            if (existingRecord.RequestHash != requestHash)
            {
                return Result<PayParkingFeeByWalletResponse>.Failure(PayParkingFeeErrors.IdempotencyKeyReused);
            }

            // Giao dịch cũ đã thành công: trả lại kết quả cũ
            if (existingRecord.Status == "Success" && !string.IsNullOrWhiteSpace(existingRecord.ResponsePayload))
            {
                var cachedResponse = JsonSerializer.Deserialize<PayParkingFeeByWalletResponse>(existingRecord.ResponsePayload);
                if (cachedResponse != null)
                {
                    return Result<PayParkingFeeByWalletResponse>.Success(cachedResponse with { IsReplayed = true });
                }
            }

            // Yêu cầu trước vẫn đang được xử lý dở
            if (existingRecord.Status == "Pending" && existingRecord.LockedUntilUtc > nowUtc)
            {
                return Result<PayParkingFeeByWalletResponse>.Failure(PayParkingFeeErrors.RequestInProgress);
            }

            // Bản ghi cũ đã thất bại hoặc quá hạn khóa: giải phóng để thử lại
            await idempotencyStore.ReleasePendingAsync(existingRecord.Id, CancellationToken.None);
        }

        // 4. Gọi bãi xe lấy thông tin xe
        var contextResult = await parkingGateway.GetFeeContextAsync(
            request.SessionId, request.CallerAccountId, request.CallerRole, cancellationToken);

        if (!contextResult.IsSuccess)
        {
            return Result<PayParkingFeeByWalletResponse>.Failure(contextResult.Error!);
        }

        var ctx = contextResult.Value!;

        // Chặn xem và trả tiền xe của người khác
        if (ctx.AccountId != request.CallerAccountId)
        {
            return Result<PayParkingFeeByWalletResponse>.Failure(PayParkingFeeErrors.SessionAccessDenied);
        }

        // 5. Đánh dấu khóa ở trạng thái đang xử lý
        var pendingRecord = PaymentIdempotencyRecord.CreatePending(
            request.CallerAccountId, OperationName, request.IdempotencyKey, requestHash, nowUtc);

        var lockAcquired = await idempotencyStore.TryCreatePendingAsync(pendingRecord, cancellationToken);
        if (!lockAcquired)
        {
            return Result<PayParkingFeeByWalletResponse>.Failure(PayParkingFeeErrors.RequestInProgress);
        }

        try
        {
            // 6. Kiểm tra trạng thái xe
            if (!string.Equals(ctx.Status, "Active", StringComparison.OrdinalIgnoreCase))
            {
                await idempotencyStore.ReleasePendingAsync(pendingRecord.Id, CancellationToken.None);
                return Result<PayParkingFeeByWalletResponse>.Failure(PayParkingFeeErrors.SessionNotActive);
            }

            // Kiểm tra xe đã trả tiền trước đó chưa (BR-09.d: cho phép thu phụ phí nếu phiên bị reopen do quá giờ)
            var alreadyPaid = await paymentRepository.ExistsForSessionAsync(request.SessionId, cancellationToken);
            var isOverstayPayment = alreadyPaid && ctx.LastPaidAtUtc.HasValue;

            if (alreadyPaid && !isOverstayPayment)
            {
                await idempotencyStore.ReleasePendingAsync(pendingRecord.Id, CancellationToken.None);
                return Result<PayParkingFeeByWalletResponse>.Failure(PayParkingFeeErrors.SessionAlreadyPaid);
            }

            // Chặn thu trùng khi bãi chưa kịp nhận tin nhắn mở cổng (BR-09.d)
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
                        return Result<PayParkingFeeByWalletResponse>.Failure(PayParkingFeeErrors.SessionAlreadyPaid);
                    }

                    paymentSequence = latestSucceeded.PaymentSequence + 1;
                }
            }

            // Tính tiền gửi xe theo giờ thực
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

                // Đọc tiền cọc bằng đúng hàm có sẵn của US-PAY-001
                if (ctx.BookingId.HasValue && ctx.AccountId.HasValue)
                {
                    prepaidAmount = await prepaymentReader.GetPrepaidAmountAsync(
                        ctx.AccountId.Value, ctx.BookingId.Value, cancellationToken);
                }

                actualAmountDue = Math.Max(0, totalFee - prepaidAmount);
            }

            // Giá nhảy khác giá khách đã đồng ý
            if (actualAmountDue != request.ExpectedAmountDue)
            {
                await idempotencyStore.ReleasePendingAsync(pendingRecord.Id, CancellationToken.None);
                return Result<PayParkingFeeByWalletResponse>.Failure(PayParkingFeeErrors.FeeChanged);
            }

            // 7. Lấy ví và kiểm tra số dư
            var wallet = await walletRepository.GetByUserIdAsync(request.CallerAccountId, cancellationToken);
            if (wallet == null)
            {
                await idempotencyStore.ReleasePendingAsync(pendingRecord.Id, CancellationToken.None);
                return Result<PayParkingFeeByWalletResponse>.Failure(PayParkingFeeErrors.WalletNotFound);
            }

            // Ví không đủ tiền thanh toán khi có phát sinh phí
            if (actualAmountDue > 0 && wallet.Balance < actualAmountDue)
            {
                await idempotencyStore.ReleasePendingAsync(pendingRecord.Id, CancellationToken.None);
                return Result<PayParkingFeeByWalletResponse>.Failure(PayParkingFeeErrors.WalletInsufficientBalance);
            }

            // 8. Trừ tiền ví (nếu > 0) và ghi nhận trong 1 Transaction
            try
            {
                if (actualAmountDue > 0)
                {
                    // Trừ tiền ví và ghi nhận sổ cái
                    wallet.AddEntry(
                        entryType: LedgerEntryType.FeePayment,
                        amount: -actualAmountDue,
                        reason: $"Pay parking fee for session {request.SessionId}",
                        referenceType: ReferenceType.ParkingSession,
                        referenceId: request.SessionId.ToString(),
                        lotId: ctx.ParkingLotId,
                        idempotencyKey: request.IdempotencyKey,
                        createdBy: request.CallerAccountId);
                }
                else if (!isOverstayPayment && prepaidAmount > totalFee)
                {
                    // Hoàn phần tiền cọc dôi dư vào ví theo BR-10.b
                    var surplus = prepaidAmount - totalFee;
                    wallet.AddEntry(
                        entryType: LedgerEntryType.Refund,
                        amount: surplus,
                        reason: $"Refund surplus prepayment for session {request.SessionId}",
                        referenceType: ReferenceType.ParkingSession,
                        referenceId: request.SessionId.ToString(),
                        lotId: ctx.ParkingLotId,
                        idempotencyKey: $"{request.IdempotencyKey}:surplus",
                        createdBy: request.CallerAccountId);
                }

                // Hủy mã QR chờ thanh toán cũ để tránh kẹt đợt sau
                var stalePending = await paymentRepository.GetPendingBySessionIdAsync(request.SessionId, cancellationToken);
                stalePending?.MarkAsSuperseded();

                // Tạo hóa đơn mới bất biến (không sửa chứng từ cũ)
                var exitWindowExpiresUtc = calculationTime.AddMinutes(15);
                var payment = ParkingFeePayment.CreateWalletPayment(
                    sessionId: request.SessionId,
                    parkingLotId: ctx.ParkingLotId,
                    walletId: wallet.Id,
                    accountId: request.CallerAccountId,
                    plateNumber: ctx.PlateNumber,
                    vehicleType: ctx.VehicleType,
                    totalFee: totalFee,
                    prepaidAmount: isOverstayPayment ? 0 : prepaidAmount,
                    amountPaid: actualAmountDue,
                    paymentTimeUtc: calculationTime,
                    exitWindowExpiresUtc: exitWindowExpiresUtc,
                    paymentSequence: paymentSequence);

                await paymentRepository.AddAsync(payment, cancellationToken);
                var paymentId = payment.Id;

                // Gửi tin nhắn nâng thanh chắn cổng ra vào hộp thư
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

                // Đóng gói kết quả thành công
                var successResponse = new PayParkingFeeByWalletResponse(
                    TransactionId: paymentId,
                    SessionId: request.SessionId,
                    AmountPaid: actualAmountDue,
                    WalletBalanceRemaining: (long)wallet.Balance,
                    PaymentTimeUtc: calculationTime,
                    ExitWindowExpiresUtc: exitWindowExpiresUtc);

                // Đánh dấu đã trừ tiền xong để chống trừ lặp
                pendingRecord.MarkSuccess(JsonSerializer.Serialize(successResponse), 200);
                await idempotencyStore.UpdateAsync(pendingRecord, cancellationToken);

                // Lưu toàn bộ vào database
                await walletRepository.SaveChangesAsync(cancellationToken);

                return Result<PayParkingFeeByWalletResponse>.Success(successResponse);
            }
            catch (Exception ex) when (ex.GetType().Name == "DbUpdateConcurrencyException")
            {
                // Xung đột số dư đồng thời: nhả khóa để Client gọi lại an toàn
                await idempotencyStore.ReleasePendingAsync(pendingRecord.Id, CancellationToken.None);
                return Result<PayParkingFeeByWalletResponse>.Failure(PayParkingFeeErrors.WalletBusy);
            }
            catch (Exception ex) when (ex.GetType().Name is "DbUpdateException" or "PostgresException")
            {
                // Đua ghi cùng đợt thanh toán: nếu đợt đó đã thành công thì trả 409
                var latest = await paymentRepository.GetSucceededBySessionIdAsync(request.SessionId, CancellationToken.None);
                if (latest != null && latest.PaymentSequence >= paymentSequence)
                {
                    await idempotencyStore.ReleasePendingAsync(pendingRecord.Id, CancellationToken.None);
                    return Result<PayParkingFeeByWalletResponse>.Failure(PayParkingFeeErrors.SessionAlreadyPaid);
                }

                throw;
            }
        }
        catch
        {
            // Có lỗi bất ngờ: giải phóng khóa tạm
            await idempotencyStore.ReleasePendingAsync(pendingRecord.Id, CancellationToken.None);
            throw;
        }
    }
}
