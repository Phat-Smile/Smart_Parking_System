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

namespace ParkingSystem.Payment.Application.Features.Wallets.Commands.DebitReservationDeposit;

public sealed class DebitReservationDepositCommandHandler(
    IWalletRepository walletRepository,
    IIdempotencyStore idempotencyStore,
    IClock clock,
    ILogger<DebitReservationDepositCommandHandler> logger)
    : IRequestHandler<DebitReservationDepositCommand, Result<DebitReservationDepositResult>>
{
    private const string OperationName = "debit_reservation_deposit";

    public async Task<Result<DebitReservationDepositResult>> Handle(
        DebitReservationDepositCommand request,
        CancellationToken cancellationToken)
    {
        // 1. Kiểm tra tham số và số tiền nguyên
        if (request.UserId == Guid.Empty)
            return Result<DebitReservationDepositResult>.Failure(ReservationDepositErrors.UserIdInvalid);

        if (request.ReservationId == Guid.Empty)
            return Result<DebitReservationDepositResult>.Failure(ReservationDepositErrors.ReservationIdInvalid);

        if (request.Amount <= 0m || request.Amount % 1m != 0m)
            return Result<DebitReservationDepositResult>.Failure(ReservationDepositErrors.AmountInvalid);

        // BƯỚC M1 & VÁ LỖ HỔNG 1: Kiểm tra chốt chặn Refund TOÀN CỤC trước khi Replay
        var existingLedgerEntries = await walletRepository.GetReservationLedgerEntriesAsync(
            request.ReservationId, userId: null, cancellationToken: cancellationToken);

        if (existingLedgerEntries.Any(e => e.EntryType == LedgerEntryType.Refund))
        {
            logger.LogWarning("Reservation {ReservationId} was already refunded. Rejecting debit.", request.ReservationId);
            return Result<DebitReservationDepositResult>.Failure(ReservationDepositErrors.AlreadyRefunded);
        }

        // Chặn trừ lại cọc nếu đặt chỗ đã bị tịch thu hoặc đã áp phí
        if (existingLedgerEntries.Any(e => e.EntryType is LedgerEntryType.PrepaymentForfeited or LedgerEntryType.PrepaymentApplied))
        {
            logger.LogWarning("Reservation {ReservationId} deposit already settled or forfeited. Rejecting debit.", request.ReservationId);
            return Result<DebitReservationDepositResult>.Failure(ReservationDepositErrors.AlreadyForfeited);
        }

        var effectiveKey = string.IsNullOrWhiteSpace(request.IdempotencyKey)
            ? $"res-deposit-{request.ReservationId}"
            : request.IdempotencyKey.Trim();

        var requestHash = $"{request.ReservationId}:{request.Amount}";
        var nowUtc = clock.UtcNow;

        // 2. Kiểm tra Idempotency Cache
        var existingRecord = await idempotencyStore.FindAsync(
            request.UserId, OperationName, effectiveKey, cancellationToken);

        if (existingRecord != null)
        {
            if (existingRecord.RequestHash != requestHash)
            {
                logger.LogWarning("Idempotency key {Key} reused with mismatched hash for user {UserId}.", effectiveKey, request.UserId);
                return Result<DebitReservationDepositResult>.Failure(ReservationDepositErrors.IdempotencyKeyReused);
            }

            if (existingRecord.Status == "Success" && !string.IsNullOrWhiteSpace(existingRecord.ResponsePayload))
            {
                var cached = JsonSerializer.Deserialize<DebitReservationDepositResult>(existingRecord.ResponsePayload);
                if (cached != null)
                {
                    logger.LogInformation("Replaying cached debit response for reservation {ReservationId}.", request.ReservationId);
                    return Result<DebitReservationDepositResult>.Success(cached);
                }
            }

            if (existingRecord.Status == "Pending" && existingRecord.LockedUntilUtc > nowUtc)
            {
                return Result<DebitReservationDepositResult>.Failure(ReservationDepositErrors.RequestInProgress);
            }

            // Bản ghi cũ đã thất bại hoặc quá hạn khóa: giải phóng để thử lại (P4)
            await idempotencyStore.ReleasePendingAsync(existingRecord.Id, CancellationToken.None);
        }

        // VÁ LỖ HỔNG 2: Lưới an toàn sổ cái chống trừ tiền 2 lần khi mất cache
        var existingDeposit = existingLedgerEntries.FirstOrDefault(e => e.EntryType == LedgerEntryType.ReservationDeposit);
        if (existingDeposit != null)
        {
            if (existingDeposit.CreatedBy.HasValue && existingDeposit.CreatedBy.Value != request.UserId)
            {
                logger.LogWarning("Reservation {ReservationId} already debited by another user {OwnerId}.",
                    request.ReservationId, existingDeposit.CreatedBy);
                return Result<DebitReservationDepositResult>.Failure(ReservationDepositErrors.IdempotencyKeyReused);
            }

            if (Math.Abs(existingDeposit.Amount) != request.Amount)
            {
                logger.LogWarning("Reservation {ReservationId} already debited with amount {OldAmt}, new amount {NewAmt}.",
                    request.ReservationId, Math.Abs(existingDeposit.Amount), request.Amount);
                return Result<DebitReservationDepositResult>.Failure(ReservationDepositErrors.IdempotencyKeyReused);
            }

            var cachedWallet = await walletRepository.GetByUserIdAsync(request.UserId, cancellationToken);
            return Result<DebitReservationDepositResult>.Success(new DebitReservationDepositResult(
                Success: true,
                TransactionId: existingDeposit.Id.ToString(),
                ErrorCode: null,
                ErrorMessage: null,
                RemainingBalance: cachedWallet != null ? (long)cachedWallet.Balance : 0L));
        }

        // 3. Tạo bản ghi Pending Idempotency
        var pendingRecord = PaymentIdempotencyRecord.CreatePending(
            request.UserId, OperationName, effectiveKey, requestHash, nowUtc);

        var lockAcquired = await idempotencyStore.TryCreatePendingAsync(pendingRecord, cancellationToken);
        if (!lockAcquired)
        {
            return Result<DebitReservationDepositResult>.Failure(ReservationDepositErrors.RequestInProgress);
        }

        try
        {
            // 4. Tìm ví
            var wallet = await walletRepository.GetByUserIdAsync(request.UserId, cancellationToken);
            if (wallet is null)
            {
                await idempotencyStore.ReleasePendingAsync(pendingRecord.Id, CancellationToken.None);
                return Result<DebitReservationDepositResult>.Failure(ReservationDepositErrors.WalletNotFound);
            }

            // Kiểm tra trạng thái hoạt động của ví (S3)
            if (wallet.Status != WalletStatus.Active)
            {
                await idempotencyStore.ReleasePendingAsync(pendingRecord.Id, CancellationToken.None);
                return Result<DebitReservationDepositResult>.Failure(ReservationDepositErrors.WalletNotActive);
            }

            // 5. Kiểm tra số dư (P4)
            if (wallet.Balance < request.Amount)
            {
                logger.LogWarning("User {UserId} wallet balance {Balance} is insufficient for deposit {Amount}.",
                    request.UserId, wallet.Balance, request.Amount);
                await idempotencyStore.ReleasePendingAsync(pendingRecord.Id, CancellationToken.None);
                return Result<DebitReservationDepositResult>.Failure(ReservationDepositErrors.InsufficientBalance);
            }

            // 6. Trừ tiền và ghi nhận sổ cái (P7)
            try
            {
                var reason = request.Purpose == ReservationPrepaymentPurpose.Scheduled
                    ? $"Scheduled reservation prepayment for {request.ReservationId}"
                    : $"Reservation deposit for {request.ReservationId}";

                wallet.AddEntry(
                    entryType: LedgerEntryType.ReservationDeposit,
                    amount: -request.Amount,
                    reason: reason,
                    referenceType: ReferenceType.Reservation,
                    referenceId: request.ReservationId.ToString(),
                    idempotencyKey: effectiveKey,
                    createdBy: request.UserId);

                var entryId = wallet.LedgerEntries.Last().Id;
                var remainingBalance = (long)wallet.Balance;

                var result = new DebitReservationDepositResult(
                    Success: true,
                    TransactionId: entryId.ToString(),
                    ErrorCode: null,
                    ErrorMessage: null,
                    RemainingBalance: remainingBalance);

                pendingRecord.MarkSuccess(JsonSerializer.Serialize(result), 200);
                await idempotencyStore.UpdateAsync(pendingRecord, cancellationToken);

                await walletRepository.SaveChangesAsync(cancellationToken);

                logger.LogInformation("Successfully debited {Amount} VND from user {UserId} for reservation {ReservationId}. Remaining: {Remaining}",
                    request.Amount, request.UserId, request.ReservationId, remainingBalance);

                return Result<DebitReservationDepositResult>.Success(result);
            }
            catch (Exception ex) when (ex.GetType().Name == "DbUpdateConcurrencyException")
            {
                await idempotencyStore.ReleasePendingAsync(pendingRecord.Id, CancellationToken.None);
                return Result<DebitReservationDepositResult>.Failure(ReservationDepositErrors.WalletBusy);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error debiting deposit for reservation {ReservationId}.", request.ReservationId);
            await idempotencyStore.ReleasePendingAsync(pendingRecord.Id, CancellationToken.None);
            throw;
        }
    }
}
