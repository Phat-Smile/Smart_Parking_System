using System.Text.Json;
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
        if (request.UserId == Guid.Empty)
        {
            return Result<DebitReservationDepositResult>.Failure(ReservationDepositErrors.UserIdInvalid);
        }

        if (request.ReservationId == Guid.Empty)
        {
            return Result<DebitReservationDepositResult>.Failure(ReservationDepositErrors.ReservationIdInvalid);
        }

        if (request.Amount <= 0m)
        {
            return Result<DebitReservationDepositResult>.Failure(ReservationDepositErrors.AmountInvalid);
        }

        var effectiveKey = string.IsNullOrWhiteSpace(request.IdempotencyKey)
            ? $"res-deposit-{request.ReservationId}"
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
        }

        // 2. Tạo bản ghi Pending Idempotency
        var pendingRecord = PaymentIdempotencyRecord.CreatePending(
            request.UserId, OperationName, effectiveKey, requestHash, nowUtc);

        var lockAcquired = await idempotencyStore.TryCreatePendingAsync(pendingRecord, cancellationToken);
        if (!lockAcquired)
        {
            return Result<DebitReservationDepositResult>.Failure(ReservationDepositErrors.RequestInProgress);
        }

        try
        {
            // 3. Tìm ví
            var wallet = await walletRepository.GetByUserIdAsync(request.UserId, cancellationToken);
            if (wallet is null)
            {
                await MarkFailedAsync(pendingRecord, ReservationDepositErrors.WalletNotFound, cancellationToken);
                return Result<DebitReservationDepositResult>.Failure(ReservationDepositErrors.WalletNotFound);
            }

            // 4. Kiểm tra số dư
            if (wallet.Balance < request.Amount)
            {
                logger.LogWarning("User {UserId} wallet balance {Balance} is insufficient for deposit {Amount}.",
                    request.UserId, wallet.Balance, request.Amount);
                await MarkFailedAsync(pendingRecord, ReservationDepositErrors.InsufficientBalance, cancellationToken);
                return Result<DebitReservationDepositResult>.Failure(ReservationDepositErrors.InsufficientBalance);
            }

            // 5. Trừ tiền và ghi nhận ledger
            try
            {
                wallet.AddEntry(
                    entryType: LedgerEntryType.ReservationDeposit,
                    amount: -request.Amount,
                    reason: $"Reservation deposit for {request.ReservationId}",
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
                await idempotencyStore.ReleasePendingAsync(pendingRecord.Id, cancellationToken);
                return Result<DebitReservationDepositResult>.Failure(ReservationDepositErrors.WalletBusy);
            }
        }
        catch
        {
            await idempotencyStore.ReleasePendingAsync(pendingRecord.Id, cancellationToken);
            throw;
        }
    }

    private async Task MarkFailedAsync(PaymentIdempotencyRecord record, Error error, CancellationToken ct)
    {
        record.MarkFailed(error.Message, 400);
        await idempotencyStore.UpdateAsync(record, ct);
    }
}
