using MediatR;
using ParkingSystem.BuildingBlocks.Results;
using ParkingSystem.Payment.Application.Common.Constants;
using ParkingSystem.Payment.Application.Common.Interfaces;
using ParkingSystem.Payment.Domain.Enums;

namespace ParkingSystem.Payment.Application.Features.Wallets.Queries.GetWalletTopupStatus;

public class GetWalletTopupStatusQueryHandler : IRequestHandler<GetWalletTopupStatusQuery, Result<WalletTopupStatusDto>>
{
    private readonly IVnpayTransactionRepository _transactionRepository;

    public GetWalletTopupStatusQueryHandler(IVnpayTransactionRepository transactionRepository)
    {
        _transactionRepository = transactionRepository;
    }

    public async Task<Result<WalletTopupStatusDto>> Handle(GetWalletTopupStatusQuery request, CancellationToken cancellationToken)
    {
        // Lay thong tin giao dich nap vi tu co so du lieu
        var txn = await _transactionRepository.GetTopupByTxnRefAndUserIdAsync(request.TopupId, request.UserId, cancellationToken);
        if (txn == null)
        {
            return Result<WalletTopupStatusDto>.Failure(PaymentErrors.Topup.NotFound);
        }

        // Chuyen doi trang thai enum sang chuoi quy uoc
        string statusString = txn.Status switch
        {
            VnpayTransactionStatus.Pending when txn.ExpiresAt <= DateTimeOffset.UtcNow => TopupStatusNames.Expired,
            VnpayTransactionStatus.Pending => TopupStatusNames.Pending,
            VnpayTransactionStatus.Success => TopupStatusNames.Succeeded,
            VnpayTransactionStatus.Failed => TopupStatusNames.Failed,
            VnpayTransactionStatus.Expired => TopupStatusNames.Expired,
            _ => TopupStatusNames.Pending
        };

        var dto = new WalletTopupStatusDto(
            TopupId: txn.TxnRef,
            Amount: (long)txn.Amount,
            Status: statusString,
            CreatedAtUtc: txn.CreatedAt,
            CompletedAtUtc: txn.CompletedAt,
            ExpiresAtUtc: txn.ExpiresAt);

        return Result<WalletTopupStatusDto>.Success(dto);
    }
}
