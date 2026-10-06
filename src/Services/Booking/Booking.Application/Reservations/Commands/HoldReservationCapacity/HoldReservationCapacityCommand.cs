using MediatR;
using ParkingSystem.Booking.Domain.Enums;
using ParkingSystem.BuildingBlocks.Results;

namespace ParkingSystem.Booking.Application.Reservations.Commands.HoldReservationCapacity;

public sealed record HoldReservationCapacityCommand(
    Guid ReservationId,
    Guid UserId,
    Guid VehicleId,
    string PlateNumber,
    Guid LotId,
    VehicleGroup VehicleGroup,
    ReservationType ReservationType,
    DateTimeOffset AppointmentTime,
    int? DurationMinutes,
    decimal PrepaidAmount,
    string? PriceSnapshot = null,
    ParkingLotCapacitySnapshot? CapacitySnapshot = null)
    : IRequest<Result<ReservationHoldResult>>;
