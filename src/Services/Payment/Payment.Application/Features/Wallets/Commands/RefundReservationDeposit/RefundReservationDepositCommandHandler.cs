using System;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Logging;
using ParkingSystem.BuildingBlocks.Results;
using ParkingSystem.BuildingBlocks.Time;
using ParkingSystem.Payment.Application.Common.Constants;
using ParkingSystem.Payment.Application.Common.Interfaces;
using ParkingSystem.Payment.Domain.Entities;
using ParkingSystem.Payment.Domain.Enums;

namespace ParkingSystem.Payment.Application.Features.Wallets.Commands.RefundReservationDeposit;

public sealed class RefundReservationDepositCommandHandler(
    IWalletRepository walletRepository,
    IIdempotencyStore idempotencyStore,
    IClock clock,
    ILogger<RefundReservationDepositCommandHandler> logger)
    : IRequestHandler<RefundReservationDepositCommand, Result<RefundReservationDepositResult>>
{
    private const string OperationName = "refund_reservation_deposit";

    public async Task<Result<RefundReservationDepositResult>> Handle(
        RefundReservationDepositCommand request,
        CancellationToken cancellationToken)
    {
        // 1. Kiểm tra tham số và số tiền nguyên
        if (request.UserId == Guid.Empty)
            return Result<RefundReservationDepositResult>.Failure(ReservationDepositErrors.UserIdInvalid);

        if (request.ReservationId == Guid.Empty)
            return Result<RefundReservationDepositResult>.Failure(ReservationDepositErrors.ReservationIdInvalid);

        if (request.Amount <= 0m || request.Amount % 1m != 0m)
            return Result<RefundReservationDepositResult>.Failure(ReservationDepositErrors.AmountInvalid);

        var effectiveKey = string.IsNullOrWhiteSpace(request.IdempotencyKey)
            ? $"res-refund-{request.ReservationId}"
            : request.IdempotencyKey.Trim();

        var requestHash = $"{request.ReservationId}:{request.Amount}";
        var nowUtc = clock.UtcNow;

        // BƯỚC M2: Replay Success ĐẦU TIÊN (bảo vệ Booking worker khi retry)
        var existingRecord = await idempotencyStore.FindAsync(
            request.UserId, OperationName, effectiveKey, cancellationToken);

        if (existingRecord != null)
        {
            if (existingRecord.RequestHash != requestHash)
            {
                logger.LogWarning("Idempotency key {Key} reused with mismatched hash for refund user {UserId}.", effectiveKey, request.UserId);
                return Result<RefundReservationDepositResult>.Failure(ReservationDepositErrors.IdempotencyKeyReused);
            }

            if (existingRecord.Status == "Success" && !string.IsNullOrWhiteSpace(existingRecord.ResponsePayload))
            {
                var cached = JsonSerializer.Deserialize<RefundReservationDepositResult>(existingRecord.ResponsePayload);
                if (cached != null)
                {
                    logger.LogInformation("Replaying cached refund response for reservation {ReservationId}.", request.ReservationId);
                    return Result<RefundReservationDepositResult>.Success(cached);
                }
            }

            if (existingRecord.Status == "Pending" && existingRecord.LockedUntilUtc > nowUtc)
            {
                return Result<RefundReservationDepositResult>.Failure(ReservationDepositErrors.RequestInProgress);
            }

            // Bản ghi cũ đã thất bại hoặc quá hạn khóa: giải phóng để thử lại
            await idempotencyStore.ReleasePendingAsync(existingRecord.Id, CancellationToken.None);
        }

        // 2. Tạo bản ghi Pending Idempotency
        var pendingRecord = PaymentIdempotencyRecord.CreatePending(
            request.UserId, OperationName, effectiveKey, requestHash, nowUtc);

        var lockAcquired = await idempotencyStore.TryCreatePendingAsync(pendingRecord, cancellationToken);
        if (!lockAcquired)
        {
            return Result<RefundReservationDepositResult>.Failure(ReservationDepositErrors.RequestInProgress);
        }

        try
        {
            // 3. Tìm ví (CẤM GetOrCreate theo BR-12.b)
            var wallet = await walletRepository.GetByUserIdAsync(request.UserId, cancellationToken);
            if (wallet is null)
            {
                await idempotencyStore.ReleasePendingAsync(pendingRecord.Id, CancellationToken.None);
                return Result<RefundReservationDepositResult>.Failure(ReservationDepositErrors.WalletNotFound);
            }

            if (wallet.Status != WalletStatus.Active)
            {
                await idempotencyStore.ReleasePendingAsync(pendingRecord.Id, CancellationToken.None);
                return Result<RefundReservationDepositResult>.Failure(ReservationDepositErrors.WalletNotActive);
            }

            // 4. VÁ LỖ HỔNG 3: Kiểm tra lịch sử trừ cọc & chống IDOR
            var entries = await walletRepository.GetReservationLedgerEntriesAsync(
                request.ReservationId, userId: null, cancellationToken: cancellationToken);

            var depositEntries = entries
                .Where(e => e.EntryType is LedgerEntryType.ReservationDeposit or LedgerEntryType.Prepayment)
                .ToList();
            if (!depositEntries.Any())
            {
                await idempotencyStore.ReleasePendingAsync(pendingRecord.Id, CancellationToken.None);
                return Result<RefundReservationDepositResult>.Failure(ReservationDepositErrors.ReservationNotDebited);
            }

            // Chống hoàn tiền chéo: mã cọc phải thuộc đúng tài khoản yêu cầu
            if (depositEntries.Any(e => e.CreatedBy.HasValue && e.CreatedBy.Value != request.UserId))
            {
                logger.LogWarning("User {UserId} tried to refund reservation {ReservationId} owned by another user.",
                    request.UserId, request.ReservationId);
                await idempotencyStore.ReleasePendingAsync(pendingRecord.Id, CancellationToken.None);
                return Result<RefundReservationDepositResult>.Failure(ReservationDepositErrors.ReservationNotDebited);
            }

            // Trừ luôn phần đã tịch thu và đã áp phí khỏi mức được phép hoàn
            var totalDebited = depositEntries.Sum(e => Math.Abs(e.Amount));
            var totalRefunded = entries.Where(e => e.EntryType == LedgerEntryType.Refund).Sum(e => e.Amount);
            var totalForfeited = entries.Where(e => e.EntryType == LedgerEntryType.PrepaymentForfeited).Sum(e => e.Amount);
            var totalApplied = entries.Where(e => e.EntryType == LedgerEntryType.PrepaymentApplied).Sum(e => e.Amount);
            var remainingRefundable = totalDebited - totalRefunded - totalForfeited - totalApplied;

            if (remainingRefundable <= 0)
            {
                await idempotencyStore.ReleasePendingAsync(pendingRecord.Id, CancellationToken.None);
                if (totalForfeited > 0 && totalRefunded == 0)
                {
                    return Result<RefundReservationDepositResult>.Failure(ReservationDepositErrors.AlreadyForfeited);
                }

                if (totalApplied > 0 && totalRefunded == 0)
                {
                    return Result<RefundReservationDepositResult>.Failure(ReservationDepositErrors.PrepaymentAlreadySettled);
                }

                return Result<RefundReservationDepositResult>.Failure(ReservationDepositErrors.AlreadyRefunded);
            }

            if (request.Amount > remainingRefundable)
            {
                await idempotencyStore.ReleasePendingAsync(pendingRecord.Id, CancellationToken.None);
                return Result<RefundReservationDepositResult>.Failure(ReservationDepositErrors.RefundAmountExceedsDeposit);
            }

            // 5. Hoàn tiền và ghi nhận sổ cái
            try
            {
                wallet.AddEntry(
                    entryType: LedgerEntryType.Refund,
                    amount: request.Amount,
                    reason: request.Reason ?? $"Refund reservation deposit for {request.ReservationId}",
                    referenceType: ReferenceType.Reservation,
                    referenceId: request.ReservationId.ToString(),
                    idempotencyKey: effectiveKey,
                    createdBy: request.UserId);

                var entryId = wallet.LedgerEntries.Last().Id;
                var remainingBalance = (long)wallet.Balance;

                var result = new RefundReservationDepositResult(
                    Success: true,
                    TransactionId: entryId.ToString(),
                    ErrorCode: null,
                    ErrorMessage: null,
                    RemainingBalance: remainingBalance);

                pendingRecord.MarkSuccess(JsonSerializer.Serialize(result), 200);
                await idempotencyStore.UpdateAsync(pendingRecord, cancellationToken);

                await walletRepository.SaveChangesAsync(cancellationToken);

                logger.LogInformation("Successfully refunded {Amount} VND to user {UserId} for reservation {ReservationId}. Balance: {Balance}",
                    request.Amount, request.UserId, request.ReservationId, remainingBalance);

                return Result<RefundReservationDepositResult>.Success(result);
            }
            catch (Exception ex) when (ex.GetType().Name == "DbUpdateConcurrencyException")
            {
                await idempotencyStore.ReleasePendingAsync(pendingRecord.Id, cancellationToken);
                return Result<RefundReservationDepositResult>.Failure(ReservationDepositErrors.WalletBusy);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error refunding deposit for reservation {ReservationId}.", request.ReservationId);
            await idempotencyStore.ReleasePendingAsync(pendingRecord.Id, cancellationToken);
            throw;
        }
    }
}
