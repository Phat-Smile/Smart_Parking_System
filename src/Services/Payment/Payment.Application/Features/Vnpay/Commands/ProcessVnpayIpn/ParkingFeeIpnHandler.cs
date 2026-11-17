using Microsoft.Extensions.Logging;
using ParkingSystem.BuildingBlocks.Time;
using ParkingSystem.Contracts.Events.Payment;
using ParkingSystem.Payment.Application.Common.Interfaces;
using ParkingSystem.Payment.Application.Common.Messaging.Outbox;
using ParkingSystem.Payment.Domain.Constants;
using ParkingSystem.Payment.Domain.Entities;
using ParkingSystem.Payment.Domain.Enums;

namespace ParkingSystem.Payment.Application.Features.Vnpay.Commands.ProcessVnpayIpn;

// Xử lý thông báo thanh toán tiền đỗ xe qua cổng VNPay
public sealed class ParkingFeeIpnHandler : IParkingFeeIpnHandler
{
    private readonly IParkingFeePaymentRepository _paymentRepository;
    private readonly IWalletRepository _walletRepository;
    private readonly IOutboxWriter _outboxWriter;
    private readonly IClock _clock;
    private readonly ILogger<ParkingFeeIpnHandler> _logger;

    public ParkingFeeIpnHandler(
        IParkingFeePaymentRepository paymentRepository,
        IWalletRepository walletRepository,
        IOutboxWriter outboxWriter,
        IClock clock,
        ILogger<ParkingFeeIpnHandler> logger)
    {
        _paymentRepository = paymentRepository;
        _walletRepository = walletRepository;
        _outboxWriter = outboxWriter;
        _clock = clock;
        _logger = logger;
    }

    public async Task<VnpayIpnResponseDto> HandleAsync(
        IReadOnlyDictionary<string, string> query,
        string txnRef,
        CancellationToken cancellationToken)
    {
        // Kiểm tra xem hóa đơn có tồn tại không
        var payment = await _paymentRepository.GetByTxnRefAsync(txnRef, cancellationToken);
        if (payment == null)
        {
            _logger.LogWarning("Parking fee payment not found for TxnRef: {TxnRef}", txnRef);
            return VnpayIpnResponseDto.OrderNotFound();
        }

        // Tránh xử lý lặp khi hóa đơn đã xong
        if (payment.Status != PaymentStatus.Pending && payment.Status != PaymentStatus.Superseded)
        {
            _logger.LogInformation("Payment {TxnRef} already confirmed with status: {Status}", txnRef, payment.Status);
            return VnpayIpnResponseDto.AlreadyConfirmed();
        }

        // Kiểm tra số tiền có khớp đơn ban đầu không
        if (!query.TryGetValue("vnp_Amount", out var rawAmountStr) ||
            !long.TryParse(rawAmountStr, out var rawAmount) ||
            rawAmount % 100 != 0 ||
            (rawAmount / 100) != payment.AmountPaid)
        {
            _logger.LogWarning("VNPay IPN invalid amount for {TxnRef}. Expected {Expected}, got {RawAmount}",
                txnRef, payment.AmountPaid, rawAmountStr);
            return VnpayIpnResponseDto.InvalidAmount();
        }

        var responseCode = query.GetValueOrDefault("vnp_ResponseCode") ?? string.Empty;
        var transactionStatus = query.GetValueOrDefault("vnp_TransactionStatus") ?? string.Empty;
        var vnpayTxnNo = query.GetValueOrDefault("vnp_TransactionNo") ?? string.Empty;
        var nowUtc = _clock.UtcNow;

        bool isSuccess = responseCode == "00" && transactionStatus == "00";

        // Cập nhật thất bại nếu ngân hàng báo không thành công
        if (!isSuccess)
        {
            payment.MarkAsFailed(responseCode, nowUtc);
            try
            {
                await _paymentRepository.SaveChangesAsync(cancellationToken);
                _logger.LogWarning("Payment failed at gateway for {TxnRef}. ResponseCode: {ResponseCode}", txnRef, responseCode);
                return VnpayIpnResponseDto.Success();
            }
            catch (Exception ex) when (ex.GetType().Name is "DbUpdateConcurrencyException" or "PostgresException")
            {
                _logger.LogWarning(ex, "Concurrency conflict on failed payment {TxnRef}", txnRef);
                return await HandleConcurrencyConflictAsync(payment, txnRef, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error saving failed payment for {TxnRef}", txnRef);
                return VnpayIpnResponseDto.UnknownError();
            }
        }

        // Kiểm tra xe đã được thanh toán trước đó chưa
        var existingSucceeded = await _paymentRepository.GetSucceededBySessionIdAsync(payment.SessionId, cancellationToken);
        var isOverstayPayment = existingSucceeded != null
            && existingSucceeded.ExitWindowExpiresUtc.HasValue
            && (nowUtc > existingSucceeded.ExitWindowExpiresUtc.Value || payment.PaymentTimeUtc > existingSucceeded.ExitWindowExpiresUtc.Value);

        if (existingSucceeded != null && !isOverstayPayment)
        {
            if (payment.AccountId.HasValue)
            {
                // Trả tiền thừa vào ví nếu là khách có tài khoản
                var wallet = await _walletRepository.GetOrCreateByUserIdAsync(payment.AccountId.Value, cancellationToken);
                wallet.AddEntry(
                    entryType: LedgerEntryType.Refund,
                    amount: payment.AmountPaid,
                    reason: $"Refund surplus parking fee for session {payment.SessionId}",
                    referenceType: ReferenceType.ParkingSession,
                    referenceId: payment.SessionId.ToString(),
                    lotId: payment.ParkingLotId,
                    idempotencyKey: $"refund:vnpay_surplus:{payment.Id}");

                payment.MarkAsSurplus(vnpayTxnNo, "RefundedToWallet", nowUtc);
            }
            else
            {
                // Khách vãng lai cần nhân viên đối soát thủ công
                payment.MarkAsSurplus(vnpayTxnNo, "NeedsManualRefund", nowUtc);
            }
        }
        else if (isOverstayPayment && existingSucceeded != null)
        {
            // Cập nhật hóa đơn và gia hạn 15 phút mở barie
            var exitWindowExpiresUtc = nowUtc.AddMinutes(15);
            existingSucceeded.RecordOverstayPayment(payment.AmountPaid, nowUtc, exitWindowExpiresUtc);

            // Đánh dấu mã tạm thời thành Superseded để tránh đụng unique index
            payment.MarkAsSuperseded();

            // Gửi tin nhắn mở cổng bãi xe qua hòm thư
            var paidEvent = new ParkingSessionPaidIntegrationEvent(
                SessionId: payment.SessionId,
                PaymentId: existingSucceeded.Id,
                AmountPaid: payment.AmountPaid,
                PaidAtUtc: nowUtc,
                ExitWindowExpiresUtc: exitWindowExpiresUtc);

            _outboxWriter.Enqueue(
                integrationEvent: paidEvent,
                eventType: "payment.session-paid.v1",
                eventVersion: 1,
                correlationId: existingSucceeded.Id);
        }
        else
        {
            // Xác nhận thành công và tạo hạn 15 phút lái xe ra
            var exitWindowExpiresUtc = nowUtc.AddMinutes(15);
            payment.MarkAsSucceeded(vnpayTxnNo, nowUtc, exitWindowExpiresUtc);

            // Gửi tin nhắn mở cổng bãi xe qua hòm thư
            var paidEvent = new ParkingSessionPaidIntegrationEvent(
                SessionId: payment.SessionId,
                PaymentId: payment.Id,
                AmountPaid: payment.AmountPaid,
                PaidAtUtc: nowUtc,
                ExitWindowExpiresUtc: exitWindowExpiresUtc);

            _outboxWriter.Enqueue(
                integrationEvent: paidEvent,
                eventType: "payment.session-paid.v1",
                eventVersion: 1,
                correlationId: payment.Id);
        }

        // Lưu lại các thay đổi vào cơ sở dữ liệu
        try
        {
            await _paymentRepository.SaveChangesAsync(cancellationToken);
            return VnpayIpnResponseDto.Success();
        }
        catch (Exception ex) when (ex.GetType().Name is "DbUpdateConcurrencyException" or "PostgresException")
        {
            // Tránh lỗi khi hai tiến trình cùng cập nhật một lúc
            _logger.LogWarning(ex, "Concurrency conflict on payment {TxnRef}", txnRef);
            return await HandleConcurrencyConflictAsync(payment, txnRef, cancellationToken);
        }
        catch (Exception ex)
        {
            // Ghi nhận lỗi bất ngờ khi xử lý
            _logger.LogError(ex, "Unexpected error saving payment for {TxnRef}", txnRef);
            return VnpayIpnResponseDto.UnknownError();
        }
    }

    private async Task<VnpayIpnResponseDto> HandleConcurrencyConflictAsync(
        ParkingFeePayment payment,
        string txnRef,
        CancellationToken cancellationToken)
    {
        try
        {
            await _paymentRepository.ReloadAsync(payment, cancellationToken);
            if (payment.Status == PaymentStatus.Succeeded || payment.Status == PaymentStatus.Surplus)
            {
                _logger.LogInformation("Payment {TxnRef} already confirmed concurrently", txnRef);
                return VnpayIpnResponseDto.AlreadyConfirmed();
            }
        }
        catch (Exception reloadEx)
        {
            _logger.LogError(reloadEx, "Failed to reload payment {TxnRef} after concurrency exception", txnRef);
        }

        return VnpayIpnResponseDto.UnknownError();
    }
}
