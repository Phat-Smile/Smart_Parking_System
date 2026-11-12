using MediatR;
using ParkingSystem.BuildingBlocks.Results;

namespace ParkingSystem.Payment.Application.Features.Wallets.Commands.RefundReservationDeposit;

public sealed record RefundReservationDepositResult(
    bool Success,
    string? TransactionId,
    string? ErrorCode,
    string? ErrorMessage,
    long RemainingBalance);

public sealed record RefundReservationDepositCommand(
    Guid UserId,
    Guid ReservationId,
    decimal Amount,
    string IdempotencyKey,
    string? Reason = null) : IRequest<Result<RefundReservationDepositResult>>;
