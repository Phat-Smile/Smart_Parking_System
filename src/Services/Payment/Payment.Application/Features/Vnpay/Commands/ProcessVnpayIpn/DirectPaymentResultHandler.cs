using Microsoft.Extensions.Logging;
using ParkingSystem.Contracts.Events.Payment;
using ParkingSystem.Payment.Application.Common.Interfaces;
using ParkingSystem.Payment.Application.Common.Messaging.Outbox;
using ParkingSystem.Payment.Domain.Entities;
using ParkingSystem.Payment.Domain.Enums;

namespace ParkingSystem.Payment.Application.Features.Vnpay.Commands.ProcessVnpayIpn;

/// <summary>
/// Xử lý kết quả thanh toán trực tiếp phí đỗ xe tại cổng ra qua VNPay.
/// </summary>
public class DirectPaymentResultHandler : IVnpayResultHandler
{
    private readonly IVnpayTransactionRepository _transactionRepository;
    private readonly IOutboxWriter _outboxWriter;
    private readonly ILogger<DirectPaymentResultHandler> _logger;

    public DirectPaymentResultHandler(
        IVnpayTransactionRepository transactionRepository,
        IOutboxWriter outboxWriter,
        ILogger<DirectPaymentResultHandler> logger)
    {
        _transactionRepository = transactionRepository;
        _outboxWriter = outboxWriter;
        _logger = logger;
    }

    public VnpayTransactionPurpose Purpose => VnpayTransactionPurpose.DirectPayment;

    public async Task<VnpayIpnResponseDto> HandleAsync(
        VnpayTransaction transaction,
        bool isPaymentSuccess,
        string vnpayTxnNo,
        string? bankCode,
        string responseCode,
        DateTimeOffset completedAt,
        CancellationToken cancellationToken)
    {
        if (isPaymentSuccess)
        {
            transaction.MarkAsSuccess(vnpayTxnNo, bankCode, responseCode, completedAt);

            _outboxWriter.Enqueue(
                new DirectPaymentCompletedIntegrationEvent(
                    transaction.Id,
                    transaction.TxnRef,
                    transaction.WalletId,
                    transaction.Amount,
                    vnpayTxnNo,
                    bankCode,
                    completedAt),
                "payment.direct-payment-completed.v1",
                1);

            _logger.LogInformation(
                "Direct payment {TxnRef} succeeded for amount {Amount} VND. Enqueued barrier-open event.",
                transaction.TxnRef,
                transaction.Amount);
        }
        else
        {
            transaction.MarkAsFailed(vnpayTxnNo, bankCode, responseCode, completedAt);

            _logger.LogWarning(
                "Direct payment {TxnRef} failed at gateway. ResponseCode: {ResponseCode}",
                transaction.TxnRef,
                responseCode);
        }

        try
        {
            await _transactionRepository.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex) when (ex.GetType().Name == "DbUpdateConcurrencyException")
        {
            _logger.LogWarning(ex, "Concurrency conflict saving direct payment {TxnRef}", transaction.TxnRef);
            return VnpayIpnResponseDto.AlreadyConfirmed();
        }

        return VnpayIpnResponseDto.Success();
    }
}
