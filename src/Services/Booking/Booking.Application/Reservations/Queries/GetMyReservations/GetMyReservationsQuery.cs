using MediatR;
using ParkingSystem.BuildingBlocks.Results;

namespace ParkingSystem.Booking.Application.Reservations.Queries.GetMyReservations;

public sealed record ReservationSummaryDto(
    Guid ReservationId,
    Guid LotId,
    Guid VehicleId,
    string PlateNumber,
    string VehicleGroup,
    string ReservationType,
    string Status,
    DateTimeOffset AppointmentTime,
    int? DurationMinutes,
    DateTimeOffset? TempHoldExpiresAt,
    DateTimeOffset WindowStartAt,
    DateTimeOffset WindowEndAt,
    decimal PrepaidAmount,
    string? QrCodeToken,
    string? PriceSnapshot,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ConfirmedAt);

public sealed record GetMyReservationsQuery(
    Guid UserId,
    string? Status = null) : IRequest<Result<IReadOnlyList<ReservationSummaryDto>>>;
