using MediatR;
using Microsoft.Extensions.Logging;
using ParkingSystem.Booking.Application.Common.Messaging.Outbox;
using ParkingSystem.Booking.Application.Payments;
using ParkingSystem.Booking.Application.Reservations.Cancellation;
using ParkingSystem.Booking.Domain.Reservations;
using ParkingSystem.BuildingBlocks.Results;
using ParkingSystem.BuildingBlocks.Time;
using ParkingSystem.Contracts.Events.Booking;

namespace ParkingSystem.Booking.Application.Reservations.Commands.CancelReservation;

public sealed class CancelReservationCommandHandler(
    IReservationRepository reservationRepository,
    IReservationPaymentClient paymentClient,
    IClock clock,
    ILogger<CancelReservationCommandHandler> logger,
    IOutboxWriter outbox)
    : IRequestHandler<CancelReservationCommand, Result<CancelledReservationDto>>
{
    public async Task<Result<CancelledReservationDto>> Handle(
        CancelReservationCommand request,
        CancellationToken cancellationToken)
    {
        // 2.a. Load reservation
        var reservation = await reservationRepository.FindByIdAsync(request.ReservationId, cancellationToken);
        if (reservation is null)
        {
            logger.LogWarning("Reservation {ReservationId} not found.", request.ReservationId);
            return Result<CancelledReservationDto>.Failure(ReservationErrors.NotFound);
        }

        // Check ownership
        if (reservation.UserId != request.UserId)
        {
            logger.LogWarning("User {UserId} attempted to cancel reservation {ReservationId} belonging to user {OwnerId}.",
                request.UserId, request.ReservationId, reservation.UserId);
            return Result<CancelledReservationDto>.Failure(ReservationErrors.AccessDenied);
        }

        // Check CanTransition (Used/Cancelled/Expired -> CannotCancel)
        if (!Reservation.CanTransition(reservation.Status, ReservationStatus.Cancelled))
        {
            logger.LogWarning("Reservation {ReservationId} with status {Status} cannot be cancelled.",
                request.ReservationId, reservation.Status);
            return Result<CancelledReservationDto>.Failure(ReservationErrors.CannotCancel);
        }

        var now = clock.UtcNow;

        // 2.b. For Scheduled only: cannot cancel after appointment time (US-RES-007 will replace it).
        // For HoldWhileDriving: allow cancel after appointment time if not yet Used.
        if (reservation.ReservationType == ReservationType.Scheduled &&
            reservation.Status == ReservationStatus.Confirmed &&
            now > reservation.AppointmentTime)
        {
            logger.LogWarning("Scheduled reservation {ReservationId} cannot be cancelled after appointment time {AppointmentTime}.",
                request.ReservationId, reservation.AppointmentTime);
            return Result<CancelledReservationDto>.Failure(ReservationErrors.CannotCancel);
        }

        // 2.c. Decide with the policy
        var decision = CancellationRefundPolicy.Decide(reservation, now);

        PrepaidOutcome? initialOutcome = decision switch
        {
            CancellationRefundDecision.Forfeit => PrepaidOutcome.Forfeited,
            CancellationRefundDecision.Refund => null, // null means refund pending
            _ => null
        };

        decimal refundAmount = decision switch
        {
            CancellationRefundDecision.Refund refund => refund.Amount,
            _ => 0m
        };

        try
        {
            reservation.Cancel(now, CancelReason.CustomerCancelled, initialOutcome);

            // 2.d. Enqueue ReservationCancelledIntegrationEvent with refundAmount (0 when forfeited), then SaveChanges
            outbox.Enqueue(
                new ReservationCancelledIntegrationEvent(
                    reservation.Id,
                    reservation.UserId,
                    reservation.LotId,
                    CancelReason.CustomerCancelled.ToString(),
                    refundAmount,
                    now),
                "booking.reservation-cancelled.v1",
                1,
                correlationId: reservation.Id);

            await reservationRepository.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex) when (ex.GetType().Name == "DbUpdateConcurrencyException")
        {
            // 2.e. Catch DbUpdateConcurrencyException: reload; if Used or Cancelled, return CannotCancel (409).
            // Never call Payment in this path.
            logger.LogWarning(ex, "Concurrency conflict while cancelling reservation {ReservationId}. Reloading...", reservation.Id);
            reservationRepository.DiscardPendingChanges();
            var reloaded = await reservationRepository.FindByIdAsync(reservation.Id, cancellationToken);
            if (reloaded?.Status == ReservationStatus.Used || reloaded?.Status == ReservationStatus.Cancelled)
            {
                return Result<CancelledReservationDto>.Failure(ReservationErrors.CannotCancel);
            }

            return Result<CancelledReservationDto>.Failure(ReservationErrors.CannotCancel);
        }
        catch (ReservationDomainException ex)
        {
            logger.LogWarning(ex, "Domain exception cancelling reservation {ReservationId}.", request.ReservationId);
            return Result<CancelledReservationDto>.Failure(new Error("booking.reservation.transition_failed", ex.Message));
        }

        logger.LogInformation("Reservation {ReservationId} cancelled by user {UserId}. Outcome: {Outcome}, RefundAmount: {RefundAmount}",
            reservation.Id, reservation.UserId, reservation.PrepaidOutcome, refundAmount);

        // 2.f. Only after a successful save, if a refund is due, call RefundAsync.
        // On Refunded, call RecordCancellationRefunded() and save. On failure or exception, log and still return success.
        string message;
        if (decision is CancellationRefundDecision.Refund)
        {
            bool refundSucceeded = false;
            try
            {
                var refundResult = await paymentClient.RefundAsync(
                    reservation.Id,
                    reservation.UserId,
                    (long)refundAmount,
                    "CustomerCancelled",
                    cancellationToken);

                if (refundResult.IsSuccess && refundResult.Value == ReservationDebitState.Refunded)
                {
                    reservation.RecordCancellationRefunded();
                    await reservationRepository.SaveChangesAsync(cancellationToken);
                    refundSucceeded = true;
                }
                else
                {
                    logger.LogWarning("Refund call unconfirmed for reservation {ReservationId}: {Error}",
                        reservation.Id, refundResult.Error?.Message ?? "RefundNotConfirmed");
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Exception calling refund for reservation {ReservationId}. Background reconciliation worker will process it.", reservation.Id);
            }

            message = refundSucceeded
                ? "Đặt chỗ đã được hủy. Tiền cọc đã được hoàn về ví của bạn."
                : "Đặt chỗ đã được hủy. Tiền cọc đang được hoàn về ví.";
        }
        else if (decision is CancellationRefundDecision.Forfeit)
        {
            message = "Đặt chỗ đã được hủy. Bạn hủy sau 5 phút kể từ lúc xác nhận nên không được hoàn cọc.";
        }
        else
        {
            message = "Đặt chỗ đã được hủy.";
        }

        return Result<CancelledReservationDto>.Success(new CancelledReservationDto(
            reservation.Id,
            reservation.Status.ToString(),
            refundAmount,
            message));
    }
}
