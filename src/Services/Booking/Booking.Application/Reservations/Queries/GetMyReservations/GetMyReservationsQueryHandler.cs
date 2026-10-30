using MediatR;
using ParkingSystem.Booking.Domain.Reservations;
using ParkingSystem.BuildingBlocks.Results;

namespace ParkingSystem.Booking.Application.Reservations.Queries.GetMyReservations;

public sealed class GetMyReservationsQueryHandler(IReservationRepository reservationRepository)
    : IRequestHandler<GetMyReservationsQuery, Result<IReadOnlyList<ReservationSummaryDto>>>
{
    public async Task<Result<IReadOnlyList<ReservationSummaryDto>>> Handle(
        GetMyReservationsQuery request,
        CancellationToken cancellationToken)
    {
        ReservationStatus? statusFilter = null;
        if (!string.IsNullOrWhiteSpace(request.Status) &&
            Enum.TryParse<ReservationStatus>(request.Status, ignoreCase: true, out var parsedStatus))
        {
            statusFilter = parsedStatus;
        }

        var reservations = await reservationRepository.ListByUserIdAsync(request.UserId, statusFilter, cancellationToken);

        var dtos = reservations.Select(r => new ReservationSummaryDto(
            r.Id,
            r.LotId,
            r.VehicleId,
            r.PlateNumber,
            r.VehicleGroup.ToString(),
            r.ReservationType.ToString(),
            r.Status.ToString(),
            r.AppointmentTime,
            r.DurationMinutes,
            r.TempHoldExpiresAt,
            r.WindowStartAt,
            r.WindowEndAt,
            r.PrepaidAmount,
            r.Status == ReservationStatus.Confirmed ? $"RES-QR-{r.Id.ToString()[..8].ToUpperInvariant()}" : null,
            r.PriceSnapshot,
            r.CreatedAt,
            r.ConfirmedAt)).ToList();

        return Result<IReadOnlyList<ReservationSummaryDto>>.Success(dtos);
    }
}
