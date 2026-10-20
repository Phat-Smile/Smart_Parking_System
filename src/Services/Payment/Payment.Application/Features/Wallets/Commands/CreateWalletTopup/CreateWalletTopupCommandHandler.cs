using MediatR;
using Microsoft.Extensions.Options;
using ParkingSystem.BuildingBlocks.Results;
using ParkingSystem.Payment.Application.Common.Interfaces;
using ParkingSystem.Payment.Application.Common.Options;
using ParkingSystem.Payment.Domain.Entities;
using ParkingSystem.Payment.Domain.Enums;

namespace ParkingSystem.Payment.Application.Features.Wallets.Commands.CreateWalletTopup;

public class CreateWalletTopupCommandHandler : IRequestHandler<CreateWalletTopupCommand, Result<WalletTopupResultDto>>
{
    private readonly IWalletRepository _walletRepository;
    private readonly IVnpayTransactionRepository _transactionRepository;
    private readonly IPaymentGateway _paymentGateway;
    private readonly WalletOptions _walletOptions;
    private readonly VnpayOptions _vnpayOptions;

    public CreateWalletTopupCommandHandler(
        IWalletRepository walletRepository,
        IVnpayTransactionRepository transactionRepository,
        IPaymentGateway paymentGateway,
        IOptions<WalletOptions> walletOptions,
        IOptions<VnpayOptions> vnpayOptions)
    {
        _walletRepository = walletRepository;
        _transactionRepository = transactionRepository;
        _paymentGateway = paymentGateway;
        _walletOptions = walletOptions.Value;
        _vnpayOptions = vnpayOptions.Value;
    }

    public async Task<Result<WalletTopupResultDto>> Handle(
        CreateWalletTopupCommand request,
        CancellationToken cancellationToken)
    {
        if (request.UserId == Guid.Empty)
        {
            return Result<WalletTopupResultDto>.Failure(
                new Error("payment.invalid_user", "Invalid user identification in request."));
        }

        // 1. Lấy thông tin ví (nếu là lần đầu nạp thì khởi tạo ví mới)
        var wallet = await _walletRepository.GetOrCreateByUserIdAsync(request.UserId, cancellationToken);

        // 2. Kiểm tra Idempotency theo (WalletId, IdempotencyKey)
        var existingTxn = await _transactionRepository.GetByWalletIdAndRequestKeyAsync(
            wallet.Id,
            request.IdempotencyKey,
            cancellationToken);

        if (existingTxn != null)
        {
            // Cùng key nhưng khác số tiền -> Báo xung đột
            if (existingTxn.Amount != request.Amount)
            {
                return Result<WalletTopupResultDto>.Failure(
                    new Error("payment.idempotency_key_reused", "Idempotency-Key was already used with a different request."));
            }

            // Giao dịch đã nạp thành công -> không sinh URL thanh toán mới
            if (existingTxn.Status == VnpayTransactionStatus.Success)
            {
                return Result<WalletTopupResultDto>.Success(new WalletTopupResultDto
                {
                    TopupId = existingTxn.TxnRef,
                    Amount = existingTxn.Amount,
                    Status = existingTxn.Status.ToString(),
                    PaymentUrl = null,
                    CreatedAtUtc = existingTxn.CreatedAt,
                    ExpiresAtUtc = existingTxn.ExpiresAt,
                    IsReplay = true
                });
            }

            // Giao dịch đã hết hạn -> không sinh URL mới, báo lỗi
            var isExpired = existingTxn.Status == VnpayTransactionStatus.Expired
                || (existingTxn.Status == VnpayTransactionStatus.Pending && existingTxn.ExpiresAt <= DateTimeOffset.UtcNow);

            if (isExpired)
            {
                return Result<WalletTopupResultDto>.Failure(
                    new Error("payment.topup_expired", "The previous top-up request for this idempotency key has expired. Please initiate a new top-up request."));
            }

            // Giao dịch đang Pending và còn hạn -> Trả về URL thanh toán VNPay
            var existingUrl = _paymentGateway.CreatePaymentUrl(
                existingTxn.TxnRef,
                existingTxn.Amount,
                existingTxn.ExpiresAt,
                request.ClientIp);

            return Result<WalletTopupResultDto>.Success(new WalletTopupResultDto
            {
                TopupId = existingTxn.TxnRef,
                Amount = existingTxn.Amount,
                Status = existingTxn.Status.ToString(),
                PaymentUrl = existingUrl,
                CreatedAtUtc = existingTxn.CreatedAt,
                ExpiresAtUtc = existingTxn.ExpiresAt,
                IsReplay = true
            });
        }

        // 3. Kiểm tra giới hạn số dư tối đa của ví (BR-12.a)
        if (wallet.Balance + request.Amount > _walletOptions.MaxBalanceVnd)
        {
            return Result<WalletTopupResultDto>.Failure(
                new Error("payment.balance_limit_exceeded", "This top-up would exceed the maximum wallet balance."));
        }

        // 4. Tạo mã tham chiếu VNPay (32 ký tự hex) và hạn thanh toán
        var topupGuid = Guid.NewGuid();
        var txnRef = topupGuid.ToString("N");
        var createdAtUtc = DateTimeOffset.UtcNow;
        var expiresAtUtc = createdAtUtc.AddMinutes(_vnpayOptions.PaymentExpiryMinutes);

        // 5. Khởi tạo và lưu giao dịch nạp tiền
        var newTxn = VnpayTransaction.CreateTopup(
            id: topupGuid,
            txnRef: txnRef,
            walletId: wallet.Id,
            amount: request.Amount,
            clientRequestKey: request.IdempotencyKey,
            createdAt: createdAtUtc,
            expiresAt: expiresAtUtc);

        await _transactionRepository.AddAsync(newTxn, cancellationToken);
        await _transactionRepository.SaveChangesAsync(cancellationToken);

        // 6. Sinh URL thanh toán VNPay
        var paymentUrl = _paymentGateway.CreatePaymentUrl(
            txnRef: txnRef,
            amount: request.Amount,
            expiresAtUtc: expiresAtUtc,
            clientIp: request.ClientIp);

        return Result<WalletTopupResultDto>.Success(new WalletTopupResultDto
        {
            TopupId = txnRef,
            Amount = request.Amount,
            Status = newTxn.Status.ToString(),
            PaymentUrl = paymentUrl,
            CreatedAtUtc = createdAtUtc,
            ExpiresAtUtc = expiresAtUtc,
            IsReplay = false
        });
    }
}
