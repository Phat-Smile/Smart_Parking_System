using Microsoft.Extensions.Logging;
using ParkingSystem.Contracts.Events.Payment;
using ParkingSystem.Payment.Application.Common.Interfaces;
using ParkingSystem.Payment.Application.Common.Messaging.Outbox;
using ParkingSystem.Payment.Domain.Entities;
using ParkingSystem.Payment.Domain.Enums;

namespace ParkingSystem.Payment.Application.Features.Vnpay.Commands.ProcessVnpayIpn;

// Xử lý kết quả nạp ví từ VNPay và cộng tiền vào ví.
public class WalletTopUpResultHandler : IVnpayResultHandler
{
    private readonly IWalletRepository _walletRepository;
    private readonly IVnpayTransactionRepository _transactionRepository;
    private readonly IOutboxWriter _outboxWriter;
    private readonly ILogger<WalletTopUpResultHandler> _logger;

    public WalletTopUpResultHandler(
        IWalletRepository walletRepository,
        IVnpayTransactionRepository transactionRepository,
        IOutboxWriter outboxWriter,
        ILogger<WalletTopUpResultHandler> logger)
    {
        _walletRepository = walletRepository;
        _transactionRepository = transactionRepository;
        _outboxWriter = outboxWriter;
        _logger = logger;
    }

    // Đánh dấu class này chỉ chuyên nhận xử lý cho việc Nạp tiền vào ví
    public VnpayTransactionPurpose Purpose => VnpayTransactionPurpose.WalletTopUp;

    // Nhận kết quả từ VNPay để quyết định có cộng tiền vào ví hay không
    public async Task<VnpayIpnResponseDto> HandleAsync(
        VnpayTransaction transaction,   // Thông tin đơn hàng nạp tiền
        bool isPaymentSuccess,          // Cờ báo khách đã trả tiền thành công hay chưa
        string vnpayTxnNo,              // Mã giao dịch do chính ngân hàng VNPay cấp
        string? bankCode,               // Tên ngân hàng khách vừa dùng
        string responseCode,
        DateTimeOffset completedAt,
        CancellationToken cancellationToken)
    {
        // Thanh toán thành công: cập nhật giao dịch và ghi có vào ví
        if (isPaymentSuccess)
        {
            // Chặn lỗi nếu không tìm thấy ví thụ hưởng trong hệ thống
            var wallet = await _walletRepository.GetByIdAsync(transaction.WalletId, cancellationToken);
            if (wallet == null)
            {
                _logger.LogError("Wallet {WalletId} not found for top-up transaction {TxnRef}",
                    transaction.WalletId, transaction.TxnRef);
                return VnpayIpnResponseDto.UnknownError();
            }

            // Đổi trạng thái giao dịch cột status: Pending -> Success.
            // Lưu thêm mã vnpayTxnNo và bankCode để sau này đối soát tiền với ngân hàng
            transaction.MarkAsSuccess(vnpayTxnNo, bankCode, responseCode, completedAt);

            // Lịch sử biến động số dư
            wallet.AddEntry(
                entryType: LedgerEntryType.TopUp,               // Loại giao dịch
                amount: transaction.Amount,                     // Số tiền
                reason: $"Top-up via VNPay (TxnRef: {transaction.TxnRef})",
                referenceType: ReferenceType.VnpayTransaction,  // Nguồn gốc tiền từ đâu tới
                referenceId: transaction.TxnRef,                // Mã chứng từ gốc
                idempotencyKey: $"topup:{transaction.TxnRef}"   // Khóa chống trùng
            );

            // Ghi sự kiện vào Outbox để báo sang Notification
            _outboxWriter.Enqueue(
                new WalletTopUpCompletedIntegrationEvent(
                    wallet.Id,
                    wallet.UserId,
                    transaction.Amount,
                    wallet.Balance,
                    transaction.TxnRef,
                    completedAt),
                "payment.wallet-topup-completed.v1",
                1);

            _logger.LogInformation("Top-up {TxnRef} succeeded. Credited {Amount} VND to wallet {WalletId}",
                transaction.TxnRef, transaction.Amount, wallet.Id);
        }
        // Thanh toán thất bại: chỉ đổi trạng thái, không cộng tiền ví.
        else
        {
            // Đổi cột status: Pending -> Failed
            transaction.MarkAsFailed(vnpayTxnNo, bankCode, responseCode, completedAt);

            _logger.LogWarning("Top-up {TxnRef} failed at gateway. ResponseCode: {ResponseCode}",
                transaction.TxnRef, responseCode);
        }

        // Lưu nguyên tử cả giao dịch, ví và outbox trong một transaction.
        try
        {
            await _transactionRepository.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex) when (ex.GetType().Name == "DbUpdateConcurrencyException" || ex.GetType().Name == "DbUpdateException")
        {
            // Tránh cộng tiền lặp khi VNPay gửi webhook song song (xung đột version hoặc vi phạm unique key sổ cái)
            _logger.LogInformation(ex, "Concurrency conflict for top-up {TxnRef}, transaction already confirmed", transaction.TxnRef);
            return VnpayIpnResponseDto.AlreadyConfirmed();
        }

        return VnpayIpnResponseDto.Success();
    }
}
