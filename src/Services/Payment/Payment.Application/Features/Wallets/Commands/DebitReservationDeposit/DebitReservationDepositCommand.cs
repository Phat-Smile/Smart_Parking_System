using MediatR;
using ParkingSystem.BuildingBlocks.Results;

namespace ParkingSystem.Payment.Application.Features.Wallets.Commands.DebitReservationDeposit;

public sealed record DebitReservationDepositResult(
    bool Success,
    string? TransactionId,
    string? ErrorCode,
    string? ErrorMessage,
    long RemainingBalance);

public sealed record DebitReservationDepositCommand(
    Guid UserId,
    Guid ReservationId,
    decimal Amount,
    string IdempotencyKey) : IRequest<Result<DebitReservationDepositResult>>;
