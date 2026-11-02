using MediatR;
using Microsoft.Extensions.Logging;
using ParkingSystem.Booking.Domain.Reservations;
using ParkingSystem.BuildingBlocks.Results;
using ParkingSystem.BuildingBlocks.Time;

namespace ParkingSystem.Booking.Application.Reservations.Commands.ConfirmReservation;

public sealed class ConfirmReservationCommandHandler(
    IReservationRepository reservationRepository,
    IClock clock,
    ILogger<ConfirmReservationCommandHandler> logger)
    : IRequestHandler<ConfirmReservationCommand, Result<ConfirmedReservationDto>>
{
    public async Task<Result<ConfirmedReservationDto>> Handle(
        ConfirmReservationCommand request,
        CancellationToken cancellationToken)
    {
        var reservation = await reservationRepository.FindByIdAsync(request.ReservationId, cancellationToken);
        if (reservation is null)
        {
            logger.LogWarning("Reservation {ReservationId} not found.", request.ReservationId);
            return Result<ConfirmedReservationDto>.Failure(ReservationErrors.NotFound);
        }

        if (reservation.UserId != request.UserId)
        {
            logger.LogWarning("User {UserId} attempted to confirm reservation {ReservationId} belonging to user {OwnerId}.",
                request.UserId, request.ReservationId, reservation.UserId);
            return Result<ConfirmedReservationDto>.Failure(ReservationErrors.AccessDenied);
        }

        if (reservation.Status == ReservationStatus.Confirmed)
        {
            var qrToken = $"RES-QR-{reservation.Id.ToString()[..8].ToUpperInvariant()}";
            return Result<ConfirmedReservationDto>.Success(new ConfirmedReservationDto(
                reservation.Id,
                reservation.Status.ToString(),
                reservation.ConfirmedAt ?? clock.UtcNow,
                qrToken,
                reservation.PrepaidAmount));
        }

        if (reservation.Status != ReservationStatus.TemporaryHold)
        {
            logger.LogWarning("Reservation {ReservationId} is in status {Status}, cannot confirm.",
                request.ReservationId, reservation.Status);
            return Result<ConfirmedReservationDto>.Failure(ReservationErrors.InvalidStatus);
        }

        var now = clock.UtcNow;
        if (reservation.TempHoldExpiresAt.HasValue && now > reservation.TempHoldExpiresAt.Value)
        {
            logger.LogWarning("Reservation {ReservationId} hold expired at {ExpiresAt}, current time {Now}.",
                request.ReservationId, reservation.TempHoldExpiresAt.Value, now);
            try
            {
                reservation.Expire();
                await reservationRepository.SaveChangesAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to mark expired reservation {ReservationId}.", request.ReservationId);
            }

            return Result<ConfirmedReservationDto>.Failure(ReservationErrors.HoldExpired);
        }

        try
        {
            reservation.Confirm(now);
            await reservationRepository.SaveChangesAsync(cancellationToken);
        }
        catch (ReservationDomainException ex)
        {
            logger.LogWarning(ex, "Domain exception confirming reservation {ReservationId}.", request.ReservationId);
            return Result<ConfirmedReservationDto>.Failure(new Error("booking.reservation.transition_failed", ex.Message));
        }

        logger.LogInformation("Reservation {ReservationId} confirmed for user {UserId}.", reservation.Id, reservation.UserId);

        var qrCodeToken = $"RES-QR-{reservation.Id.ToString()[..8].ToUpperInvariant()}";
        return Result<ConfirmedReservationDto>.Success(new ConfirmedReservationDto(
            reservation.Id,
            reservation.Status.ToString(),
            reservation.ConfirmedAt ?? now,
            qrCodeToken,
            reservation.PrepaidAmount));
    }
}
