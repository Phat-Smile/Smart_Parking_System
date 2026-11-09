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
                await MarkFailedAsync(pendingRecord, PayParkingFeeErrors.SessionNotActive, 409, cancellationToken);
                return Result<PayParkingFeeByWalletResponse>.Failure(PayParkingFeeErrors.SessionNotActive);
            }

            // Kiểm tra xe đã trả tiền trước đó chưa
            var alreadyPaid = await paymentRepository.ExistsForSessionAsync(request.SessionId, cancellationToken);
            if (alreadyPaid)
            {
                await MarkFailedAsync(pendingRecord, PayParkingFeeErrors.SessionAlreadyPaid, 409, cancellationToken);
                return Result<PayParkingFeeByWalletResponse>.Failure(PayParkingFeeErrors.SessionAlreadyPaid);
            }

            // Tính tiền gửi xe theo giờ thực
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
            // Đọc tiền cọc bằng đúng hàm có sẵn của US-PAY-001
            if (ctx.BookingId.HasValue && ctx.AccountId.HasValue)
            {
                prepaidAmount = await prepaymentReader.GetPrepaidAmountAsync(
                    ctx.AccountId.Value, ctx.BookingId.Value, cancellationToken);
            }

            var actualAmountDue = Math.Max(0, calculation.TotalFee - prepaidAmount);

            // Giá nhảy khác giá khách đã đồng ý
            if (actualAmountDue != request.ExpectedAmountDue)
            {
                await MarkFailedAsync(pendingRecord, PayParkingFeeErrors.FeeChanged, 409, cancellationToken);
                return Result<PayParkingFeeByWalletResponse>.Failure(PayParkingFeeErrors.FeeChanged);
            }

            // 7. Lấy ví và kiểm tra số dư
            var wallet = await walletRepository.GetByUserIdAsync(request.CallerAccountId, cancellationToken);
            if (wallet == null)
            {
                await MarkFailedAsync(pendingRecord, PayParkingFeeErrors.WalletNotFound, 404, cancellationToken);
                return Result<PayParkingFeeByWalletResponse>.Failure(PayParkingFeeErrors.WalletNotFound);
            }

            // Ví không đủ tiền thanh toán khi có phát sinh phí
            if (actualAmountDue > 0 && wallet.Balance < actualAmountDue)
            {
                await MarkFailedAsync(pendingRecord, PayParkingFeeErrors.WalletInsufficientBalance, 400, cancellationToken);
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

                // Tạo hóa đơn thanh toán tiền gửi xe
                var exitWindowExpiresUtc = calculationTime.AddMinutes(15);
                var payment = ParkingFeePayment.Create(
                    sessionId: request.SessionId,
                    parkingLotId: ctx.ParkingLotId,
                    walletId: wallet.Id,
                    accountId: request.CallerAccountId,
                    plateNumber: ctx.PlateNumber,
                    vehicleType: ctx.VehicleType,
                    totalFee: calculation.TotalFee,
                    prepaidAmount: prepaidAmount,
                    amountPaid: actualAmountDue,
                    paymentTimeUtc: calculationTime,
                    exitWindowExpiresUtc: exitWindowExpiresUtc);

                await paymentRepository.AddAsync(payment, cancellationToken);

                // Gửi tin nhắn nâng thanh chắn cổng ra vào hộp thư
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

                // Đóng gói kết quả thành công
                var successResponse = new PayParkingFeeByWalletResponse(
                    TransactionId: payment.Id,
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
                await idempotencyStore.ReleasePendingAsync(pendingRecord.Id, cancellationToken);
                return Result<PayParkingFeeByWalletResponse>.Failure(PayParkingFeeErrors.WalletBusy);
            }
        }
        catch
        {
            // Có lỗi bất ngờ: giải phóng khóa tạm
            await idempotencyStore.ReleasePendingAsync(pendingRecord.Id, cancellationToken);
            throw;
        }
    }

    private async Task MarkFailedAsync(PaymentIdempotencyRecord record, Error error, int statusCode, CancellationToken ct)
    {
        record.MarkFailed(error.Message, statusCode);
        await idempotencyStore.UpdateAsync(record, ct);
    }
}
