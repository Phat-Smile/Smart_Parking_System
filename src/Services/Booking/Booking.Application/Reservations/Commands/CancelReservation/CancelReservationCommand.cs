using MediatR;
using ParkingSystem.BuildingBlocks.Results;

namespace ParkingSystem.Booking.Application.Reservations.Commands.CancelReservation;

public sealed record CancelledReservationDto(
    Guid ReservationId,
    string Status,
    decimal RefundAmount,
    string Message);

public sealed record CancelReservationCommand(
    Guid ReservationId,
    Guid UserId,
    string? Reason) : IRequest<Result<CancelledReservationDto>>;
