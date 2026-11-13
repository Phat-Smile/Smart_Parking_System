using System;
using System.Linq;
using System.Threading.Tasks;
using Grpc.Core;
using MediatR;
using Microsoft.Extensions.Logging;
using ParkingSystem.Payment.Application.Common.Constants;
using ParkingSystem.Payment.Application.Common.Interfaces;
using ParkingSystem.Payment.Application.Features.Wallets.Commands.DebitReservationDeposit;
using ParkingSystem.Payment.Application.Features.Wallets.Commands.RefundReservationDeposit;
using ParkingSystem.Payment.Domain.Enums;
using SmartParking.Contracts.Payment.Reservation.V1;

namespace ParkingSystem.Payment.WebAPI.Grpc;

public sealed class ReservationWalletGrpcService(
    ISender sender,
    IWalletRepository walletRepository,
    ILogger<ReservationWalletGrpcService> logger)
    : ReservationWalletContract.ReservationWalletContractBase
{
    public override async Task<DebitReservationResponse> DebitReservation(
        DebitReservationRequest request,
        ServerCallContext context)
    {
        logger.LogInformation("Received DebitReservation for reservation {ReservationId}, account {AccountId}, amount {Amount}.",
            request.ReservationId, request.AccountId, request.AmountVnd);

        if (!Guid.TryParse(request.AccountId, out var userId) ||
            !Guid.TryParse(request.ReservationId, out var reservationId))
        {
            return new DebitReservationResponse
            {
                Outcome = DebitOutcome.Unspecified,
                LedgerEntryId = string.Empty,
                BalanceAfterVnd = 0
            };
        }

        var purpose = request.Purpose == PrepaymentPurpose.Scheduled
            ? ReservationPrepaymentPurpose.Scheduled
            : ReservationPrepaymentPurpose.Deposit;

        var command = new DebitReservationDepositCommand(
            userId,
            reservationId,
            request.AmountVnd,
            $"res-deposit-{reservationId}",
            purpose);

        var result = await sender.Send(command, context.CancellationToken);

        if (result.IsSuccess)
        {
            return new DebitReservationResponse
            {
                Outcome = DebitOutcome.Succeeded,
                LedgerEntryId = result.Value?.TransactionId ?? string.Empty,
                BalanceAfterVnd = result.Value != null ? (long)result.Value.RemainingBalance : 0L
            };
        }

        // P6: Mapping chuẩn xác toàn bộ enum của Hợp đồng 5
        var outcome = result.Error?.Code switch
        {
            _ when result.Error?.Code == ReservationDepositErrors.InsufficientBalance.Code => DebitOutcome.InsufficientBalance,
            _ when result.Error?.Code == ReservationDepositErrors.WalletNotFound.Code => DebitOutcome.InsufficientBalance, // "no wallet counts as balance 0"
            _ when result.Error?.Code == ReservationDepositErrors.WalletNotActive.Code => DebitOutcome.WalletNotActive,
            _ when result.Error?.Code == ReservationDepositErrors.AlreadyRefunded.Code => DebitOutcome.AlreadyRefunded,
            _ when result.Error?.Code == ReservationDepositErrors.IdempotencyKeyReused.Code => DebitOutcome.AmountMismatch,
            _ => DebitOutcome.Unspecified
        };

        return new DebitReservationResponse
        {
            Outcome = outcome,
            LedgerEntryId = string.Empty,
            BalanceAfterVnd = 0
        };
    }

    public override async Task<GetReservationDebitResponse> GetReservationDebit(
        GetReservationDebitRequest request,
        ServerCallContext context)
    {
        logger.LogInformation("Received GetReservationDebit for reservation {ReservationId}, account {AccountId}.",
            request.ReservationId, request.AccountId);

        // S1 & P8: Bắt buộc AccountId phải là GUID hợp lệ, chống IDOR
        if (!Guid.TryParse(request.AccountId, out var userId) ||
            !Guid.TryParse(request.ReservationId, out var reservationId))
        {
            return new GetReservationDebitResponse
            {
                State = DebitState.Unspecified,
                AmountVnd = 0
            };
        }

        var entries = await walletRepository.GetReservationLedgerEntriesAsync(
            reservationId, userId, context.CancellationToken);

        if (entries.Any(e => e.EntryType == LedgerEntryType.Refund))
        {
            var debitEntry = entries.FirstOrDefault(e => e.EntryType == LedgerEntryType.ReservationDeposit);
            var amt = debitEntry != null ? (long)Math.Abs(debitEntry.Amount) : 0L;
            return new GetReservationDebitResponse
            {
                State = DebitState.Refunded,
                AmountVnd = amt
            };
        }

        var deposit = entries.FirstOrDefault(e => e.EntryType == LedgerEntryType.ReservationDeposit);
        if (deposit != null)
        {
            return new GetReservationDebitResponse
            {
                State = DebitState.Debited,
                AmountVnd = (long)Math.Abs(deposit.Amount)
            };
        }

        return new GetReservationDebitResponse
        {
            State = DebitState.NotDebited,
            AmountVnd = 0
        };
    }

    public override async Task<RefundReservationResponse> RefundReservation(
        RefundReservationRequest request,
        ServerCallContext context)
    {
        logger.LogInformation("Received RefundReservation for reservation {ReservationId}, account {AccountId}, amount {Amount}.",
            request.ReservationId, request.AccountId, request.AmountVnd);

        if (!Guid.TryParse(request.AccountId, out var userId) ||
            !Guid.TryParse(request.ReservationId, out var reservationId))
        {
            return new RefundReservationResponse
            {
                State = DebitState.Unspecified,
                LedgerEntryId = string.Empty
            };
        }

        var command = new RefundReservationDepositCommand(
            userId,
            reservationId,
            request.AmountVnd,
            $"res-refund-{reservationId}",
            request.Reason);

        var result = await sender.Send(command, context.CancellationToken);

        if (result.IsSuccess)
        {
            return new RefundReservationResponse
            {
                State = DebitState.Refunded,
                LedgerEntryId = result.Value!.TransactionId ?? string.Empty
            };
        }

        logger.LogWarning("RefundReservation failed for reservation {ReservationId}: {Error}",
            request.ReservationId, result.Error?.Message);

        // S2: Mapping lỗi phía Refund hỗ trợ Booking Saga
        var errorState = result.Error?.Code switch
        {
            _ when result.Error?.Code == ReservationDepositErrors.ReservationNotDebited.Code => DebitState.NotDebited,
            _ when result.Error?.Code == ReservationDepositErrors.AlreadyRefunded.Code => DebitState.Refunded,
            _ => DebitState.Unspecified
        };

        return new RefundReservationResponse
        {
            State = errorState,
            LedgerEntryId = string.Empty
        };
    }
}
