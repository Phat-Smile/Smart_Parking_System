using MediatR;
using Microsoft.Extensions.Logging;
using ParkingSystem.Booking.Application.Common.Messaging.Outbox;
using ParkingSystem.Booking.Application.Payments;
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

        // US-RES-003: Cancellation of confirmed reservation must be done before appointment time.
        // Once appointment time has arrived, cancellation is disallowed (customer must check in or let it lapse to No-Show).
        if (reservation.Status == ReservationStatus.Confirmed && now > reservation.AppointmentTime)
        {
            logger.LogWarning("Reservation {ReservationId} cannot be cancelled after appointment time {AppointmentTime}.",
                request.ReservationId, reservation.AppointmentTime);
            return Result<CancelledReservationDto>.Failure(ReservationErrors.CannotCancel);
        }

        decimal refundAmount = 0m;
        PrepaidOutcome? outcome = null;

        if (reservation.Status == ReservationStatus.Confirmed && reservation.PrepaidAmount > 0m)
        {
            var refundResult = await paymentClient.RefundAsync(
                reservation.Id,
                reservation.UserId,
                (long)reservation.PrepaidAmount,
                "CustomerCancelled",
                cancellationToken);

            if (!refundResult.IsSuccess || refundResult.Value != ReservationDebitState.Refunded)
            {
                logger.LogError("Failed to refund reservation deposit for reservation {ReservationId}. Error: {Error}",
                    reservation.Id, refundResult.Error?.Message ?? "RefundNotConfirmed");
                return Result<CancelledReservationDto>.Failure(ReservationErrors.PaymentUnconfirmed);
            }

            refundAmount = reservation.PrepaidAmount;
            outcome = PrepaidOutcome.Refunded;
        }

        try
        {
            reservation.Cancel(now, CancelReason.CustomerCancelled, outcome);

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
        catch (ReservationDomainException ex)
        {
            logger.LogWarning(ex, "Domain exception cancelling reservation {ReservationId}.", request.ReservationId);
            return Result<CancelledReservationDto>.Failure(new Error("booking.reservation.transition_failed", ex.Message));
        }

        logger.LogInformation("Reservation {ReservationId} cancelled by user {UserId}. RefundAmount: {RefundAmount}",
            reservation.Id, reservation.UserId, refundAmount);

        var message = refundAmount > 0
            ? "Đặt chỗ đã được hủy thành công. Tiền cọc đã được hoàn về ví của bạn."
            : "Đặt chỗ đã được hủy thành công.";

        return Result<CancelledReservationDto>.Success(new CancelledReservationDto(
            reservation.Id,
            reservation.Status.ToString(),
            refundAmount,
            message));
    }
}
