using MediatR;
using ParkingSystem.Booking.Domain.Reservations;
using ParkingSystem.BuildingBlocks.Results;

namespace ParkingSystem.Booking.Application.Reservations.Commands.CreateTemporaryHold;

/// <summary>
/// Use case command for creating a temporary reservation hold across both HoldWhileDriving and Scheduled reservation types.
/// Enforces US-RES-005 capacity guard invariants atomically.
/// Does not process payments, does not charge wallets, and does not emit integration events.
/// </summary>
public sealed record CreateTemporaryHoldCommand(
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
    string? PriceSnapshot = null) : IRequest<Result<Reservation>>;

