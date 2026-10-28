using MediatR;
using Microsoft.Extensions.Logging;
using ParkingSystem.Payment.Application.Common.Interfaces;
using ParkingSystem.Payment.Domain.Entities;
using ParkingSystem.Payment.Domain.Enums;

namespace ParkingSystem.Payment.Application.Features.Vnpay.Commands.ProcessVnpayIpn;

// Bộ điều phối tổng: kiểm tra tính hợp lệ của Webhook VNPay rồi bàn giao việc.
public class ProcessVnpayIpnCommandHandler : IRequestHandler<ProcessVnpayIpnCommand, VnpayIpnResponseDto>
{
    private readonly IPaymentGateway _paymentGateway;
    private readonly IVnpayTransactionRepository _transactionRepository;
    private readonly IEnumerable<IVnpayResultHandler> _resultHandlers;
    private readonly ILogger<ProcessVnpayIpnCommandHandler> _logger;

    public ProcessVnpayIpnCommandHandler(
        IPaymentGateway paymentGateway,
        IVnpayTransactionRepository transactionRepository,
        IEnumerable<IVnpayResultHandler> resultHandlers,
        ILogger<ProcessVnpayIpnCommandHandler> logger)
    {
        _paymentGateway = paymentGateway;
        _transactionRepository = transactionRepository;
        _resultHandlers = resultHandlers;
        _logger = logger;
    }

    public async Task<VnpayIpnResponseDto> Handle(ProcessVnpayIpnCommand request, CancellationToken cancellationToken)
    {
        var query = request.QueryParameters;

        // 1. Kiểm tra chữ ký bảo mật trước khi truy vấn Database.
        if (!_paymentGateway.VerifyIpnSignature(query))
        {
            _logger.LogWarning("VNPay IPN received with invalid signature");
            return VnpayIpnResponseDto.InvalidSignature();
        }

        // Chặn request nếu thiếu mã tham chiếu giao dịch.
        if (!query.TryGetValue("vnp_TxnRef", out var txnRef) || string.IsNullOrWhiteSpace(txnRef))
        {
            _logger.LogWarning("VNPay IPN missing vnp_TxnRef");
            return VnpayIpnResponseDto.OrderNotFound();
        }

        // 2. Tra cứu đơn hàng trong Database theo mã giao dịch.
        var transaction = await _transactionRepository.GetByTxnRefAsync(txnRef, cancellationToken);
        if (transaction == null)
        {
            _logger.LogWarning("VNPay IPN order not found: {TxnRef}", txnRef);
            return VnpayIpnResponseDto.OrderNotFound();
        }

        // 3. Kiểm tra số tiền thanh toán có khớp với số tiền đã tạo ban đầu.
        if (!query.TryGetValue("vnp_Amount", out var rawAmountStr) ||
            !long.TryParse(rawAmountStr, out var rawAmount))
        {
            _logger.LogWarning("VNPay IPN invalid amount format for {TxnRef}", txnRef);
            return VnpayIpnResponseDto.InvalidAmount();
        }

        var incomingAmount = (decimal)rawAmount / 100m;
        if (incomingAmount != transaction.Amount)
        {
            _logger.LogWarning("VNPay IPN amount mismatch for {TxnRef}. Expected {Expected}, got {Actual}",
                txnRef, transaction.Amount, incomingAmount);
            return VnpayIpnResponseDto.InvalidAmount();
        }

        // 4. Chống cộng tiền lặp: Đơn đã xử lý xong trước đó thì trả mã 02.
        if (transaction.Status != VnpayTransactionStatus.Pending)
        {
            _logger.LogInformation("VNPay IPN order already confirmed: {TxnRef}, current status: {Status}",
                txnRef, transaction.Status);
            return VnpayIpnResponseDto.AlreadyConfirmed();
        }

        // Đọc kết quả thành công hay thất bại từ VNPay gửi sang.
        var responseCode = query.GetValueOrDefault("vnp_ResponseCode") ?? string.Empty;
        var transactionStatus = query.GetValueOrDefault("vnp_TransactionStatus") ?? string.Empty;
        var vnpayTxnNo = query.GetValueOrDefault("vnp_TransactionNo") ?? string.Empty;
        var bankCode = query.GetValueOrDefault("vnp_BankCode");

        bool isPaymentSuccess = responseCode == "00" && transactionStatus == "00";

        // 5. Bàn giao đơn hàng cho đúng bộ phận phụ trách xử lý.
        var handler = _resultHandlers.FirstOrDefault(h => h.Purpose == transaction.Purpose);
        if (handler == null)
        {
            _logger.LogError("No result handler registered for purpose {Purpose} (TxnRef: {TxnRef})",
                transaction.Purpose, txnRef);
            return VnpayIpnResponseDto.UnknownError();
        }

        try
        {
            return await handler.HandleAsync(
                transaction,
                isPaymentSuccess,
                vnpayTxnNo,
                bankCode,
                responseCode,
                DateTimeOffset.UtcNow,
                cancellationToken);
        }
        catch (Exception ex)
        {
            // 6. Gặp sự cố bất ngờ thì trả mã 99 để VNPay gửi lại sau.
            _logger.LogError(ex, "Unexpected error processing VNPay IPN for {TxnRef}", txnRef);
            return VnpayIpnResponseDto.UnknownError();
        }
    }
}
