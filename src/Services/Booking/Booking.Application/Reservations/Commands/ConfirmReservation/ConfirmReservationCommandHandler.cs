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
    private static readonly EventId PaymentNotAvailableEventId = new(1001, "PaymentNotAvailable");

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

        // Gate: Free HoldWhileDriving reservations (PrepaidAmount == 0) can be confirmed immediately without payment.
        // All others require Payment service (US-RES-003/004), which is not yet available.
        // [NOTE]: This gate will be removed once Payment Service integration is in place.
        var canConfirmWithoutPayment = reservation.ReservationType == ReservationType.HoldWhileDriving &&
                                       reservation.PrepaidAmount == 0m;

        if (!canConfirmWithoutPayment)
        {
            var reason = reservation.PrepaidAmount > 0m
                ? "PaymentRequired"
                : "AmountNotServerDerived";

            logger.LogError(
                PaymentNotAvailableEventId,
                "Cannot confirm reservation {ReservationId} because payment function is unavailable. UserId: {UserId}, LotId: {LotId}, ReservationType: {ReservationType}, Status: {Status}, PrepaidAmount: {PrepaidAmount}, TempHoldExpiresAt: {TempHoldExpiresAt}, Reason: {Reason}.",
                reservation.Id,
                reservation.UserId,
                reservation.LotId,
                reservation.ReservationType,
                reservation.Status,
                reservation.PrepaidAmount,
                reservation.TempHoldExpiresAt,
                reason);

            return Result<ConfirmedReservationDto>.Failure(ReservationErrors.PaymentNotAvailable);
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
