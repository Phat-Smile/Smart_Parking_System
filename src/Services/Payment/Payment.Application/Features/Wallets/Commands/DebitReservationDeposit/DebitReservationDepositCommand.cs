using System;
using MediatR;
using ParkingSystem.BuildingBlocks.Results;
using ParkingSystem.Payment.Domain.Enums;

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
    string IdempotencyKey,
    ReservationPrepaymentPurpose Purpose = ReservationPrepaymentPurpose.Deposit) : IRequest<Result<DebitReservationDepositResult>>;
