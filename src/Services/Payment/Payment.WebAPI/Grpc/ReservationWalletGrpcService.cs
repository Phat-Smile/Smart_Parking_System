using Grpc.Core;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ParkingSystem.Payment.Application.Common.Constants;
using ParkingSystem.Payment.Application.Features.Wallets.Commands.DebitReservationDeposit;
using ParkingSystem.Payment.Application.Features.Wallets.Commands.RefundReservationDeposit;
using ParkingSystem.Payment.Domain.Enums;
using ParkingSystem.Payment.Persistence;
using SmartParking.Contracts.Payment.Reservation.V1;

namespace ParkingSystem.Payment.WebAPI.Grpc;

public sealed class ReservationWalletGrpcService(
    ISender sender,
    PaymentDbContext dbContext,
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

        var command = new DebitReservationDepositCommand(
            userId,
            reservationId,
            request.AmountVnd,
            $"res-deposit-{reservationId}");

        var result = await sender.Send(command, context.CancellationToken);

        if (result.IsSuccess)
        {
            return new DebitReservationResponse
            {
                Outcome = DebitOutcome.Succeeded,
                LedgerEntryId = result.Value!.TransactionId.ToString(),
                BalanceAfterVnd = (long)result.Value.RemainingBalance
            };
        }

        var outcome = result.Error?.Code == ReservationDepositErrors.InsufficientBalance.Code
            ? DebitOutcome.InsufficientBalance
            : result.Error?.Code == ReservationDepositErrors.WalletNotFound.Code
                ? DebitOutcome.InsufficientBalance
                : DebitOutcome.Unspecified;

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

        if (!Guid.TryParse(request.ReservationId, out var reservationId))
        {
            return new GetReservationDebitResponse
            {
                State = DebitState.Unspecified,
                AmountVnd = 0
            };
        }

        var idStr = reservationId.ToString();
        var idGuidStr = reservationId.ToString("D");
        var idCompactStr = reservationId.ToString("N");

        var entries = await dbContext.WalletLedgerEntries
            .AsNoTracking()
            .Where(e => e.ReferenceType == ReferenceType.Reservation
                     && (e.ReferenceId == idStr || e.ReferenceId == idGuidStr || e.ReferenceId == idCompactStr))
            .ToListAsync(context.CancellationToken);

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

        return new RefundReservationResponse
        {
            State = DebitState.Unspecified,
            LedgerEntryId = string.Empty
        };
    }
}
