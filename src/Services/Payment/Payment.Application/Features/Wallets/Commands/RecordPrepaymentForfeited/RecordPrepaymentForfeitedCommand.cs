using System;
using MediatR;
using ParkingSystem.BuildingBlocks.Results;

namespace ParkingSystem.Payment.Application.Features.Wallets.Commands.RecordPrepaymentForfeited;

// Lệnh ghi nhận khoản trả trước bị mất vào sổ cái
public sealed record RecordPrepaymentForfeitedCommand(
    Guid ReservationId,
    Guid UserId,
    Guid LotId,
    decimal ForfeitedAmount,
    string? CancelReason,
    DateTimeOffset CancelledAtUtc) : IRequest<Result<Guid>>;
