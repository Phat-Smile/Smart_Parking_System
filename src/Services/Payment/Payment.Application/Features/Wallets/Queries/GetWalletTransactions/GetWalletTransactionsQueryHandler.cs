using System.Globalization;
using MediatR;
using ParkingSystem.BuildingBlocks.Results;
using ParkingSystem.Payment.Application.Common.Constants;
using ParkingSystem.Payment.Application.Common.Interfaces;
using ParkingSystem.Payment.Domain.Enums;

namespace ParkingSystem.Payment.Application.Features.Wallets.Queries.GetWalletTransactions;

public class GetWalletTransactionsQueryHandler : IRequestHandler<GetWalletTransactionsQuery, Result<WalletTransactionsPagedDto>>
{
    private readonly IWalletRepository _walletRepository;
    private readonly IWalletTransactionQueryRepository _queryRepository;

    public GetWalletTransactionsQueryHandler(
        IWalletRepository walletRepository,
        IWalletTransactionQueryRepository queryRepository)
    {
        _walletRepository = walletRepository;
        _queryRepository = queryRepository;
    }

    public async Task<Result<WalletTransactionsPagedDto>> Handle(
        GetWalletTransactionsQuery request,
        CancellationToken cancellationToken)
    {
        // 1. Kiểm tra vai trò của caller: Chỉ khách hàng đăng ký (Customer) mới có ví
        if (!string.IsNullOrWhiteSpace(request.Role) &&
            !string.Equals(request.Role, "Customer", StringComparison.OrdinalIgnoreCase))
        {
            return Result<WalletTransactionsPagedDto>.Failure(PaymentErrors.Wallet.CustomerOnly);
        }

        // 2. Validate Page
        var page = 1;
        if (!string.IsNullOrWhiteSpace(request.RawPage))
        {
            if (!int.TryParse(request.RawPage, NumberStyles.None, CultureInfo.InvariantCulture, out page) || page < 1)
            {
                return Result<WalletTransactionsPagedDto>.Failure(PaymentErrors.Wallet.InvalidPage);
            }
        }

        // 3. Validate PageSize
        var pageSize = 20;
        if (!string.IsNullOrWhiteSpace(request.RawPageSize))
        {
            if (!int.TryParse(request.RawPageSize, NumberStyles.None, CultureInfo.InvariantCulture, out pageSize) ||
                pageSize < 1 || pageSize > 50)
            {
                return Result<WalletTransactionsPagedDto>.Failure(PaymentErrors.Wallet.InvalidPageSize);
            }
        }

        // 4. Validate Loại giao dịch (so sánh với danh sách tên Enum, chặn các chuỗi kết hợp bitwise)
        LedgerEntryType? entryType = null;
        if (!string.IsNullOrWhiteSpace(request.RawType))
        {
            var isDefinedName = Enum.GetNames<LedgerEntryType>()
                .Contains(request.RawType, StringComparer.OrdinalIgnoreCase);

            if (!isDefinedName)
            {
                return Result<WalletTransactionsPagedDto>.Failure(PaymentErrors.Wallet.InvalidEntryType);
            }

            entryType = Enum.Parse<LedgerEntryType>(request.RawType, ignoreCase: true);
        }

        // 5. Validate Date Range (phải là UTC và fromUtc < toUtc)
        DateTimeOffset? fromUtc = null;
        DateTimeOffset? toUtc = null;

        if (!string.IsNullOrWhiteSpace(request.RawFromUtc))
        {
            if (!DateTimeOffset.TryParse(request.RawFromUtc, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedFrom) ||
                parsedFrom.Offset != TimeSpan.Zero)
            {
                return Result<WalletTransactionsPagedDto>.Failure(PaymentErrors.Wallet.InvalidDateRange);
            }
            fromUtc = parsedFrom;
        }

        if (!string.IsNullOrWhiteSpace(request.RawToUtc))
        {
            if (!DateTimeOffset.TryParse(request.RawToUtc, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedTo) ||
                parsedTo.Offset != TimeSpan.Zero)
            {
                return Result<WalletTransactionsPagedDto>.Failure(PaymentErrors.Wallet.InvalidDateRange);
            }
            toUtc = parsedTo;
        }

        if (fromUtc.HasValue && toUtc.HasValue && fromUtc.Value >= toUtc.Value)
        {
            return Result<WalletTransactionsPagedDto>.Failure(PaymentErrors.Wallet.InvalidDateRange);
        }

        // 6. Tra cứu ví của người dùng
        var wallet = await _walletRepository.GetByUserIdAsync(request.UserId, cancellationToken);
        if (wallet == null)
        {
            // Theo spec: Khách chưa có ví thì trả về items rỗng với HTTP 200
            return Result<WalletTransactionsPagedDto>.Success(
                new WalletTransactionsPagedDto(Array.Empty<WalletTransactionItemDto>(), page, pageSize, 0, 0));
        }

        // 7. Truy vấn lịch sử giao dịch qua CQS Query Repository
        var filter = new WalletTransactionFilter(entryType, fromUtc, toUtc, page, pageSize);
        var (rawItems, totalCount) = await _queryRepository.GetTransactionsAsync(wallet.Id, filter, cancellationToken);

        var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling((double)totalCount / pageSize);

        var items = rawItems.Select(item =>
        {
            var affectsBalance = item.EntryType.AffectsBalance();
            var balanceDelta = affectsBalance ? (long)item.Amount : 0L;

            return new WalletTransactionItemDto(
                EntryId: item.Id,
                CreatedAtUtc: item.CreatedAt,
                EntryType: item.EntryType.ToString(),
                Amount: (long)item.Amount,
                BalanceDelta: balanceDelta,
                AffectsBalance: affectsBalance,
                BalanceAfter: (long)item.BalanceAfter,
                Reason: item.Reason,
                ReferenceType: item.ReferenceType?.ToString(),
                ReferenceId: item.ReferenceId
            );
        }).ToList();

        return Result<WalletTransactionsPagedDto>.Success(
            new WalletTransactionsPagedDto(items, page, pageSize, totalCount, totalPages));
    }
}
