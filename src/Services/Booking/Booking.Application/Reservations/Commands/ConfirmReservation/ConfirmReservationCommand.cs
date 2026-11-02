using MediatR;
using ParkingSystem.BuildingBlocks.Results;

namespace ParkingSystem.Booking.Application.Reservations.Commands.ConfirmReservation;

public sealed record ConfirmedReservationDto(
    Guid ReservationId,
    string Status,
    DateTimeOffset ConfirmedAt,
    string QrCodeToken,
    decimal PrepaidAmount);

public sealed record ConfirmReservationCommand(
    Guid ReservationId,
    Guid UserId,
    string? PaymentMethod,
    decimal DepositAmount) : IRequest<Result<ConfirmedReservationDto>>;
