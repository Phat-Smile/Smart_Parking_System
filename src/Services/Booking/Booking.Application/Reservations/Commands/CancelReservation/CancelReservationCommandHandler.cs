using MediatR;
using Microsoft.Extensions.Logging;
using ParkingSystem.Booking.Domain.Reservations;
using ParkingSystem.BuildingBlocks.Results;
using ParkingSystem.BuildingBlocks.Time;

namespace ParkingSystem.Booking.Application.Reservations.Commands.CancelReservation;

public sealed class CancelReservationCommandHandler(
    IReservationRepository reservationRepository,
    IClock clock,
    ILogger<CancelReservationCommandHandler> logger)
    : IRequestHandler<CancelReservationCommand, Result<CancelledReservationDto>>
{
    public async Task<Result<CancelledReservationDto>> Handle(
        CancelReservationCommand request,
        CancellationToken cancellationToken)
    {
        var reservation = await reservationRepository.FindByIdAsync(request.ReservationId, cancellationToken);
        if (reservation is null)
        {
            logger.LogWarning("Reservation {ReservationId} not found.", request.ReservationId);
            return Result<CancelledReservationDto>.Failure(ReservationErrors.NotFound);
        }

        if (reservation.UserId != request.UserId)
        {
            logger.LogWarning("User {UserId} attempted to cancel reservation {ReservationId} belonging to user {OwnerId}.",
                request.UserId, request.ReservationId, reservation.UserId);
            return Result<CancelledReservationDto>.Failure(ReservationErrors.AccessDenied);
        }

        if (!Reservation.CanTransition(reservation.Status, ReservationStatus.Cancelled))
        {
            logger.LogWarning("Reservation {ReservationId} with status {Status} cannot be cancelled.",
                request.ReservationId, reservation.Status);
            return Result<CancelledReservationDto>.Failure(ReservationErrors.CannotCancel);
        }

        var now = clock.UtcNow;
        try
        {
            reservation.Cancel(now, CancelReason.CustomerCancelled, PrepaidOutcome.Refunded);
            await reservationRepository.SaveChangesAsync(cancellationToken);
        }
        catch (ReservationDomainException ex)
        {
            logger.LogWarning(ex, "Domain exception cancelling reservation {ReservationId}.", request.ReservationId);
            return Result<CancelledReservationDto>.Failure(new Error("booking.reservation.transition_failed", ex.Message));
        }

        logger.LogInformation("Reservation {ReservationId} cancelled by user {UserId}.", reservation.Id, reservation.UserId);

        return Result<CancelledReservationDto>.Success(new CancelledReservationDto(
            reservation.Id,
            reservation.Status.ToString(),
            reservation.PrepaidAmount,
            "Đặt chỗ đã được hủy thành công. Tiền cọc đã được hoàn về ví của bạn."));
    }
}
