using Grpc.Core;
using MediatR;
using Microsoft.Extensions.Logging;
using ParkingSystem.Payment.Application.Common.Constants;
using ParkingSystem.Payment.Application.Features.Wallets.Commands.DebitReservationDeposit;
using SmartParking.Contracts.Payment.Reservation.V1;

namespace ParkingSystem.Payment.WebAPI.Grpc;

public sealed class ReservationWalletGrpcService(
    ISender sender,
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

    public override Task<GetReservationDebitResponse> GetReservationDebit(
        GetReservationDebitRequest request,
        ServerCallContext context)
    {
        return Task.FromResult(new GetReservationDebitResponse
        {
            State = DebitState.Debited,
            AmountVnd = 0
        });
    }

    public override Task<RefundReservationResponse> RefundReservation(
        RefundReservationRequest request,
        ServerCallContext context)
    {
        return Task.FromResult(new RefundReservationResponse
        {
            State = DebitState.Refunded,
            LedgerEntryId = Guid.NewGuid().ToString()
        });
    }
}
