using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Logging;
using ParkingSystem.BuildingBlocks.Results;
using ParkingSystem.Payment.Application.Common.Constants;
using ParkingSystem.Payment.Application.Common.Interfaces;
using ParkingSystem.Payment.Domain.Enums;

namespace ParkingSystem.Payment.Application.Features.Wallets.Commands.RecordPrepaymentForfeited;

// Xử lý ghi sổ cái mất tiền cọc không đổi số dư
public sealed class RecordPrepaymentForfeitedCommandHandler(
    IWalletRepository walletRepository,
    ILogger<RecordPrepaymentForfeitedCommandHandler> logger)
    : IRequestHandler<RecordPrepaymentForfeitedCommand, Result<Guid>>
{
    private const decimal MaxVndAmount = 999_999_999_999m;
    private const int MaxReasonLength = 500;

    public async Task<Result<Guid>> Handle(
        RecordPrepaymentForfeitedCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Không mất cọc thì bỏ qua bình thường
        if (request.ForfeitedAmount == 0m)
        {
            return Result<Guid>.Success(Guid.Empty);
        }

        // Chặn dữ liệu đầu vào sai trước khi chạm cơ sở dữ liệu
        if (request.ReservationId == Guid.Empty)
        {
            return Result<Guid>.Failure(ReservationDepositErrors.ReservationIdInvalid);
        }

        if (request.UserId == Guid.Empty)
        {
            return Result<Guid>.Failure(ReservationDepositErrors.UserIdInvalid);
        }

        if (request.LotId == Guid.Empty)
        {
            return Result<Guid>.Failure(ReservationDepositErrors.LotIdInvalid);
        }

        if (request.ForfeitedAmount < 0m
            || request.ForfeitedAmount % 1m != 0m
            || request.ForfeitedAmount > MaxVndAmount)
        {
            return Result<Guid>.Failure(ReservationDepositErrors.AmountInvalid);
        }

        // Tra cứu toàn cục sổ cái theo mã đặt chỗ
        var entries = await walletRepository.GetReservationLedgerEntriesAsync(
            request.ReservationId,
            userId: null,
            cancellationToken: cancellationToken);

        // Chống ghi trùng bút toán mất cọc
        var existingForfeit = entries.FirstOrDefault(e => e.EntryType == LedgerEntryType.PrepaymentForfeited);
        if (existingForfeit is not null)
        {
            logger.LogInformation(
                "PrepaymentForfeited already recorded for reservation {ReservationId} (EntryId: {EntryId}).",
                request.ReservationId,
                existingForfeit.Id);
            return Result<Guid>.Success(existingForfeit.Id);
        }

        // Tìm ví hiện có, tuyệt đối không tự tạo ví rỗng
        var wallet = await walletRepository.GetByUserIdAsync(request.UserId, cancellationToken);
        if (wallet is null)
        {
            logger.LogWarning(
                "Wallet not found for user {UserId} when forfeiting reservation {ReservationId}.",
                request.UserId,
                request.ReservationId);
            return Result<Guid>.Failure(ReservationDepositErrors.WalletNotFound);
        }

        // Kiểm tra đặt chỗ đã từng trừ cọc đúng trên ví này
        var depositEntries = entries
            .Where(e => e.EntryType is LedgerEntryType.ReservationDeposit or LedgerEntryType.Prepayment)
            .ToList();

        if (depositEntries.Count == 0 || depositEntries.Any(e => e.WalletId != wallet.Id))
        {
            logger.LogWarning(
                "No matching deposit entry for user {UserId} and reservation {ReservationId}.",
                request.UserId,
                request.ReservationId);
            return Result<Guid>.Failure(ReservationDepositErrors.ReservationNotDebited);
        }

        // Kiểm tra tiền cọc chưa bị hoàn hoặc đã trừ vào phí
        var totalDebited = depositEntries.Sum(e => Math.Abs(e.Amount));
        var totalRefunded = entries.Where(e => e.EntryType == LedgerEntryType.Refund).Sum(e => e.Amount);
        var totalApplied = entries.Where(e => e.EntryType == LedgerEntryType.PrepaymentApplied).Sum(e => e.Amount);
        var remainingUnsettled = totalDebited - totalRefunded - totalApplied;

        if (remainingUnsettled <= 0m)
        {
            logger.LogWarning(
                "Deposit already settled for reservation {ReservationId} (Refunded={Refunded}, Applied={Applied}).",
                request.ReservationId,
                totalRefunded,
                totalApplied);
            return Result<Guid>.Failure(ReservationDepositErrors.PrepaymentAlreadySettled);
        }

        if (request.ForfeitedAmount > remainingUnsettled)
        {
            logger.LogWarning(
                "ForfeitedAmount {Forfeited} exceeds unsettled deposit {Remaining} for reservation {ReservationId}.",
                request.ForfeitedAmount,
                remainingUnsettled,
                request.ReservationId);
            return Result<Guid>.Failure(ReservationDepositErrors.ForfeitAmountExceedsDeposit);
        }

        // Chuẩn hóa lý do không vượt quá 500 ký tự
        var cancelReason = string.IsNullOrWhiteSpace(request.CancelReason)
            ? "Unknown"
            : request.CancelReason.Trim();
        var rawReason = $"Prepayment forfeited for reservation {request.ReservationId} ({cancelReason})";
        var reason = rawReason.Length <= MaxReasonLength ? rawReason : rawReason[..MaxReasonLength];

        wallet.AddEntry(
            entryType: LedgerEntryType.PrepaymentForfeited,
            amount: request.ForfeitedAmount,
            reason: reason,
            referenceType: ReferenceType.Reservation,
            referenceId: request.ReservationId.ToString(),
            lotId: request.LotId,
            idempotencyKey: $"res-forfeit-{request.ReservationId}",
            createdBy: null,
            occurredAtUtc: request.CancelledAtUtc);

        // Để lỗi đụng độ ví thoát ra ngoài để hệ thống thử lại sạch
        await walletRepository.SaveChangesAsync(cancellationToken);

        var entryId = wallet.LedgerEntries.Last().Id;
        logger.LogInformation(
            "Recorded PrepaymentForfeited {EntryId} of {Amount} VND for reservation {ReservationId}, lot {LotId}.",
            entryId,
            request.ForfeitedAmount,
            request.ReservationId,
            request.LotId);

        return Result<Guid>.Success(entryId);
    }
}
