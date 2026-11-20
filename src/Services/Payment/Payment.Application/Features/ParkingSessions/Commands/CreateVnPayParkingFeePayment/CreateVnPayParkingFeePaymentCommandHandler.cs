using System.Text.Json;
using MediatR;
using Microsoft.Extensions.Options;
using ParkingSystem.BuildingBlocks.Results;
using ParkingSystem.BuildingBlocks.Time;
using ParkingSystem.Payment.Application.Common.Constants;
using ParkingSystem.Payment.Application.Common.Interfaces;
using ParkingSystem.Payment.Application.Common.Options;
using ParkingSystem.Payment.Domain.Constants;
using ParkingSystem.Payment.Domain.Entities;
using ParkingSystem.Payment.Domain.Enums;
using ParkingSystem.Payment.Domain.Pricing;

namespace ParkingSystem.Payment.Application.Features.ParkingSessions.Commands.CreateVnPayParkingFeePayment;

// Xử lý tạo giao dịch thanh toán gửi xe qua VNPay
public sealed class CreateVnPayParkingFeePaymentCommandHandler(
    IParkingSessionGateway parkingGateway,
    IPrepaymentReader prepaymentReader,
    IParkingFeePaymentRepository paymentRepository,
    IVnpayPaymentGateway vnpayGateway,
    IIdempotencyStore idempotencyStore,
    IOptions<VnpayOptions> vnpayOptions,
    IClock clock)
    : IRequestHandler<CreateVnPayParkingFeePaymentCommand, Result<CreateVnPayParkingFeePaymentResponse>>
{
    private const string OperationName = "create_vnpay_payment";

    public async Task<Result<CreateVnPayParkingFeePaymentResponse>> Handle(
        CreateVnPayParkingFeePaymentCommand request,
        CancellationToken ct)
    {
        // 1. Kiểm tra vai trò người gọi
        var role = request.CallerRole ?? string.Empty;
        var isCustomer = string.Equals(role, "Customer", StringComparison.OrdinalIgnoreCase);
        var isStaffOrOwner = string.Equals(role, "Staff", StringComparison.OrdinalIgnoreCase)
            || string.Equals(role, "LotOwner", StringComparison.OrdinalIgnoreCase);

        if (!isCustomer && !isStaffOrOwner)
        {
            return Result<CreateVnPayParkingFeePaymentResponse>.Failure(PayParkingFeeErrors.RoleNotAllowedVNPay);
        }

        // 2. Kiểm tra định dạng đầu vào
        if (request.SessionId == Guid.Empty)
        {
            return Result<CreateVnPayParkingFeePaymentResponse>.Failure(PayParkingFeeErrors.SessionIdInvalid);
        }

        if (string.IsNullOrWhiteSpace(request.IdempotencyKey) || !Guid.TryParse(request.IdempotencyKey, out _))
        {
            return Result<CreateVnPayParkingFeePaymentResponse>.Failure(PayParkingFeeErrors.IdempotencyKeyInvalid);
        }

        if (request.ExpectedAmountDue <= 0)
        {
            return Result<CreateVnPayParkingFeePaymentResponse>.Failure(PayParkingFeeErrors.ExpectedAmountInvalid);
        }

        var requestHash = $"{request.SessionId}:{request.ExpectedAmountDue}";
        var nowUtc = clock.UtcNow;
        var idempotencyOwnerId = request.CallerAccountId ?? Guid.Empty;

        // 3. Tra cứu khóa chống lặp yêu cầu
        var existingRecord = await idempotencyStore.FindAsync(
            idempotencyOwnerId, OperationName, request.IdempotencyKey, ct);

        if (existingRecord != null)
        {
            if (existingRecord.RequestHash != requestHash)
            {
                return Result<CreateVnPayParkingFeePaymentResponse>.Failure(PayParkingFeeErrors.IdempotencyKeyReused);
            }

            if (existingRecord.Status == "Success" && !string.IsNullOrWhiteSpace(existingRecord.ResponsePayload))
            {
                var cached = JsonSerializer.Deserialize<CreateVnPayParkingFeePaymentResponse>(existingRecord.ResponsePayload);
                if (cached != null)
                {
                    return Result<CreateVnPayParkingFeePaymentResponse>.Success(cached with { IsReplayed = true });
                }
            }

            if (existingRecord.Status == "Pending" && existingRecord.LockedUntilUtc > nowUtc)
            {
                return Result<CreateVnPayParkingFeePaymentResponse>.Failure(PayParkingFeeErrors.RequestInProgress);
            }

            // Bản ghi cũ đã thất bại hoặc quá hạn khóa: giải phóng để thử lại
            await idempotencyStore.ReleasePendingAsync(existingRecord.Id, CancellationToken.None);
        }

        // 4. Lấy thông tin phiên xe từ bãi đỗ
        var contextResult = await parkingGateway.GetFeeContextAsync(
            request.SessionId, request.CallerAccountId, role, ct);

        if (!contextResult.IsSuccess)
        {
            return Result<CreateVnPayParkingFeePaymentResponse>.Failure(contextResult.Error!);
        }

        var ctx = contextResult.Value!;

        // 5. Chặn xem và tạo mã thanh toán xe người khác
        if (isCustomer)
        {
            if (!ctx.AccountId.HasValue || ctx.AccountId.Value != request.CallerAccountId)
            {
                return Result<CreateVnPayParkingFeePaymentResponse>.Failure(PayParkingFeeErrors.SessionAccessDenied);
            }
        }
        else if (isStaffOrOwner)
        {
            if (string.IsNullOrWhiteSpace(request.StaffLotId)
                || !Guid.TryParse(request.StaffLotId, out var parsedStaffLotId)
                || parsedStaffLotId != ctx.ParkingLotId)
            {
                return Result<CreateVnPayParkingFeePaymentResponse>.Failure(PayParkingFeeErrors.SessionAccessDenied);
            }
        }

        // 6. Giữ khóa yêu cầu chống xử lý song song
        var pendingRecord = PaymentIdempotencyRecord.CreatePending(
            idempotencyOwnerId, OperationName, request.IdempotencyKey, requestHash, nowUtc);

        var lockAcquired = await idempotencyStore.TryCreatePendingAsync(pendingRecord, ct);
        if (!lockAcquired)
        {
            return Result<CreateVnPayParkingFeePaymentResponse>.Failure(PayParkingFeeErrors.RequestInProgress);
        }

        try
        {
            // 7. Kiểm tra trạng thái phiên xe
            if (!string.Equals(ctx.Status, "Active", StringComparison.OrdinalIgnoreCase))
            {
                await idempotencyStore.ReleasePendingAsync(pendingRecord.Id, CancellationToken.None);
                return Result<CreateVnPayParkingFeePaymentResponse>.Failure(PayParkingFeeErrors.SessionNotActive);
            }

            var alreadyPaid = await paymentRepository.HasSucceededPaymentAsync(request.SessionId, ct);
            var isOverstayPayment = alreadyPaid && ctx.LastPaidAtUtc.HasValue;

            if (alreadyPaid && !isOverstayPayment)
            {
                await idempotencyStore.ReleasePendingAsync(pendingRecord.Id, CancellationToken.None);
                return Result<CreateVnPayParkingFeePaymentResponse>.Failure(PayParkingFeeErrors.SessionAlreadyPaid);
            }

            // Chặn tạo mã trùng khi bãi chưa kịp nhận tin mở cổng
            var paymentSequence = 1;
            if (isOverstayPayment)
            {
                var latestSucceeded = await paymentRepository.GetSucceededBySessionIdAsync(request.SessionId, ct);
                if (latestSucceeded != null)
                {
                    var latestPaidAtUtc = latestSucceeded.ConfirmedAtUtc ?? latestSucceeded.PaymentTimeUtc;
                    if (latestPaidAtUtc > ctx.LastPaidAtUtc!.Value.AddSeconds(1))
                    {
                        await idempotencyStore.ReleasePendingAsync(pendingRecord.Id, CancellationToken.None);
                        return Result<CreateVnPayParkingFeePaymentResponse>.Failure(PayParkingFeeErrors.SessionAlreadyPaid);
                    }

                    paymentSequence = latestSucceeded.PaymentSequence + 1;
                }
            }

            // Tính tiền gửi xe theo thời gian thực
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
                        ctx.AccountId.Value, ctx.BookingId.Value, ct);
                }

                actualAmountDue = Math.Max(0, totalFee - prepaidAmount);
            }

            if (actualAmountDue == 0)
            {
                await idempotencyStore.ReleasePendingAsync(pendingRecord.Id, CancellationToken.None);
                return Result<CreateVnPayParkingFeePaymentResponse>.Failure(PayParkingFeeErrors.NoAmountDue);
            }

            if (actualAmountDue != request.ExpectedAmountDue)
            {
                await idempotencyStore.ReleasePendingAsync(pendingRecord.Id, CancellationToken.None);
                return Result<CreateVnPayParkingFeePaymentResponse>.Failure(PayParkingFeeErrors.FeeChanged);
            }

            if (actualAmountDue < 5000 || actualAmountDue > 1000000000)
            {
                await idempotencyStore.ReleasePendingAsync(pendingRecord.Id, CancellationToken.None);
                return Result<CreateVnPayParkingFeePaymentResponse>.Failure(PayParkingFeeErrors.VnPayAmountNotSupported);
            }

            // 8. Tái sử dụng mã cũ nếu cùng đợt, còn hạn, cùng số tiền
            var existingPending = await paymentRepository.GetPendingBySessionIdAsync(request.SessionId, ct);
            if (existingPending != null
                && existingPending.PaymentSequence == paymentSequence
                && existingPending.ExpiresAtUtc.HasValue
                && existingPending.ExpiresAtUtc.Value > nowUtc
                && existingPending.AmountPaid == actualAmountDue
                && !string.IsNullOrWhiteSpace(existingPending.PaymentUrl))
            {
                var reusedResponse = new CreateVnPayParkingFeePaymentResponse(
                    existingPending.Id,
                    existingPending.PaymentUrl,
                    existingPending.AmountPaid,
                    existingPending.ExpiresAtUtc.Value,
                    existingPending.Status,
                    IsReused: true);

                await MarkSuccessAsync(pendingRecord, reusedResponse, ct);
                return Result<CreateVnPayParkingFeePaymentResponse>.Success(reusedResponse);
            }

            // 9. Tạo giao dịch VNPay mới
            var paymentId = Guid.NewGuid();
            var txnRef = $"PARK_{nowUtc:yyyyMMddHHmmss}_{paymentId.ToString()[..8]}";
            var expiryMinutes = vnpayOptions.Value.PaymentExpiryMinutes > 0 ? vnpayOptions.Value.PaymentExpiryMinutes : 15;
            var expiresAtUtc = nowUtc.AddMinutes(expiryMinutes);
            var orderInfo = $"Thanh toan phi do xe {request.SessionId.ToString()[..8]}";

            var parkingReturnUrl = !string.IsNullOrWhiteSpace(vnpayOptions.Value.ParkingReturnUrl)
                ? vnpayOptions.Value.ParkingReturnUrl
                : vnpayOptions.Value.ReturnUrl;

            var paymentUrl = vnpayGateway.BuildPaymentUrl(
                txnRef,
                actualAmountDue,
                orderInfo,
                nowUtc,
                expiresAtUtc,
                request.ClientIp ?? "127.0.0.1",
                parkingReturnUrl);

            var newPayment = ParkingFeePayment.CreateVnPayPayment(
                sessionId: request.SessionId,
                parkingLotId: ctx.ParkingLotId,
                accountId: ctx.AccountId,
                plateNumber: ctx.PlateNumber,
                vehicleType: ctx.VehicleType,
                totalFee: totalFee,
                prepaidAmount: isOverstayPayment ? 0 : prepaidAmount,
                amountPaid: actualAmountDue,
                feeCalculatedAtUtc: calculationTime,
                expiresAtUtc: expiresAtUtc,
                txnRef: txnRef,
                paymentUrl: paymentUrl,
                paymentSequence: paymentSequence);

            try
            {
                await paymentRepository.CreateOrReplacePendingAsync(newPayment, ct);
            }
            catch (Exception)
            {
                // Nạp lại mã chờ nếu cùng đợt, khớp tiền và còn hạn
                var conflictPending = await paymentRepository.GetPendingBySessionIdAsync(request.SessionId, CancellationToken.None);
                if (conflictPending != null
                    && conflictPending.PaymentSequence == paymentSequence
                    && conflictPending.AmountPaid == actualAmountDue
                    && (!conflictPending.ExpiresAtUtc.HasValue || conflictPending.ExpiresAtUtc.Value > nowUtc)
                    && !string.IsNullOrWhiteSpace(conflictPending.PaymentUrl))
                {
                    var fallbackResponse = new CreateVnPayParkingFeePaymentResponse(
                        conflictPending.Id,
                        conflictPending.PaymentUrl,
                        conflictPending.AmountPaid,
                        conflictPending.ExpiresAtUtc ?? expiresAtUtc,
                        conflictPending.Status,
                        IsReused: true);

                    await MarkSuccessAsync(pendingRecord, fallbackResponse, ct);
                    return Result<CreateVnPayParkingFeePaymentResponse>.Success(fallbackResponse);
                }

                var latest = await paymentRepository.GetSucceededBySessionIdAsync(request.SessionId, CancellationToken.None);
                if (latest != null && latest.PaymentSequence >= paymentSequence)
                {
                    await idempotencyStore.ReleasePendingAsync(pendingRecord.Id, CancellationToken.None);
                    return Result<CreateVnPayParkingFeePaymentResponse>.Failure(PayParkingFeeErrors.SessionAlreadyPaid);
                }

                if (conflictPending != null)
                {
                    await idempotencyStore.ReleasePendingAsync(pendingRecord.Id, CancellationToken.None);
                    return Result<CreateVnPayParkingFeePaymentResponse>.Failure(PayParkingFeeErrors.FeeChanged);
                }

                throw;
            }

            var response = new CreateVnPayParkingFeePaymentResponse(
                newPayment.Id,
                paymentUrl,
                actualAmountDue,
                expiresAtUtc,
                newPayment.Status,
                IsReused: false);

            await MarkSuccessAsync(pendingRecord, response, ct);
            return Result<CreateVnPayParkingFeePaymentResponse>.Success(response);
        }
        catch
        {
            await idempotencyStore.ReleasePendingAsync(pendingRecord.Id, CancellationToken.None);
            throw;
        }
    }

    private Task MarkSuccessAsync(PaymentIdempotencyRecord record, CreateVnPayParkingFeePaymentResponse response, CancellationToken ct)
    {
        record.MarkSuccess(JsonSerializer.Serialize(response), 200);
        return idempotencyStore.UpdateAsync(record, ct);
    }
}
