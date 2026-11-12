using System.Text.Json;
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
        if (request.UserId == Guid.Empty)
        {
            return Result<RefundReservationDepositResult>.Failure(ReservationDepositErrors.UserIdInvalid);
        }

        if (request.ReservationId == Guid.Empty)
        {
            return Result<RefundReservationDepositResult>.Failure(ReservationDepositErrors.ReservationIdInvalid);
        }

        if (request.Amount <= 0m)
        {
            return Result<RefundReservationDepositResult>.Failure(ReservationDepositErrors.AmountInvalid);
        }

        var effectiveKey = string.IsNullOrWhiteSpace(request.IdempotencyKey)
            ? $"res-refund-{request.ReservationId}"
            : request.IdempotencyKey.Trim();

        var requestHash = $"{request.ReservationId}:{request.Amount}";
        var nowUtc = clock.UtcNow;

        // 1. Kiểm tra Idempotency
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
            // 3. Tìm hoặc tạo ví
            var wallet = await walletRepository.GetOrCreateByUserIdAsync(request.UserId, cancellationToken);

            // 4. Hoàn tiền và ghi nhận ledger
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
        catch
        {
            await idempotencyStore.ReleasePendingAsync(pendingRecord.Id, cancellationToken);
            throw;
        }
    }
}
