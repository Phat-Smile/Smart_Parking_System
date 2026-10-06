using ParkingSystem.Booking.Domain.Enums;
using ParkingSystem.Booking.Domain.Reservations;

namespace ParkingSystem.Booking.Application.Reservations.Commands.HoldReservationCapacity;

public sealed record ReservationHoldResult(
    Guid ReservationId,
    Guid UserId,
    Guid VehicleId,
    string PlateNumber,
    Guid LotId,
    VehicleGroup VehicleGroup,
    ReservationType ReservationType,
    ReservationStatus Status,
    DateTimeOffset AppointmentTime,
    int? DurationMinutes,
    DateTimeOffset TempHoldExpiresAt,
    decimal PrepaidAmount,
    ReservationTimeWindow Window);
