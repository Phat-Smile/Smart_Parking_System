using MediatR;
using Microsoft.Extensions.Logging;
using ParkingSystem.Booking.Application.Common.Messaging.Outbox;
using ParkingSystem.Booking.Application.Payments;
using ParkingSystem.Booking.Domain.Reservations;
using ParkingSystem.BuildingBlocks.Results;
using ParkingSystem.BuildingBlocks.Time;
using ParkingSystem.Contracts.Events.Booking;

namespace ParkingSystem.Booking.Application.Reservations.Commands.CancelForNoSuitableSlot;

public sealed class CancelForNoSuitableSlotCommandHandler(
    IReservationRepository reservationRepository,
    IReservationPaymentClient paymentClient,
    IClock clock,
    ILogger<CancelForNoSuitableSlotCommandHandler> logger,
    IOutboxWriter outbox)
    : IRequestHandler<CancelForNoSuitableSlotCommand, Result<CancelForNoSuitableSlotResult>>
{
    public async Task<Result<CancelForNoSuitableSlotResult>> Handle(
        CancelForNoSuitableSlotCommand request,
        CancellationToken cancellationToken)
    {
        // 1. Check for active Confirmed reservation within valid arrival window
        var reservation = await reservationRepository.FindActiveConfirmedByLotAndPlateAsync(
            request.LotId,
            request.PlateNumber,
            request.ArrivedAt,
            cancellationToken);

        if (reservation is null)
        {
            // Idempotent repeat: check if already cancelled due to NoSuitableSlot
            var existingCancelled = await reservationRepository.FindCancelledNoSuitableSlotByLotAndPlateAsync(
                request.LotId,
                request.PlateNumber,
                request.ArrivedAt,
                cancellationToken);

            if (existingCancelled is not null)
            {
                logger.LogInformation(
                    "Reservation {ReservationId} was already cancelled due to NoSuitableSlot for lot {LotId}, plate {PlateNumber}.",
                    existingCancelled.Id, request.LotId, request.PlateNumber);

                return Result<CancelForNoSuitableSlotResult>.Success(
                    new CancelForNoSuitableSlotResult(true, existingCancelled.Id));
            }

            logger.LogInformation(
                "No active confirmed reservation found for lot {LotId}, plate {PlateNumber} at {ArrivedAt}.",
                request.LotId, request.PlateNumber, request.ArrivedAt);

            return Result<CancelForNoSuitableSlotResult>.Success(
                new CancelForNoSuitableSlotResult(false, null));
        }

        var now = clock.UtcNow;

        try
        {
            reservation.CancelForNoSuitableSlot(now);

            // Enqueue ReservationCancelledIntegrationEvent with refundAmount = PrepaidAmount, forfeitedAmount = 0
            outbox.Enqueue(
                new ReservationCancelledIntegrationEvent(
                    reservation.Id,
                    reservation.UserId,
                    reservation.LotId,
                    CancelReason.NoSuitableSlot.ToString(),
                    reservation.PrepaidAmount,
                    now,
                    forfeitedAmount: 0m),
                "booking.reservation-cancelled.v1",
                1,
                correlationId: reservation.Id);

            await reservationRepository.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex) when (ex.GetType().Name == "DbUpdateConcurrencyException")
        {
            logger.LogWarning(ex, "Concurrency conflict while cancelling reservation {ReservationId} due to no suitable slot. Reloading...", reservation.Id);
            reservationRepository.DiscardPendingChanges();
            var reloaded = await reservationRepository.FindByIdAsync(reservation.Id, cancellationToken);
            if (reloaded?.Status == ReservationStatus.Cancelled && reloaded.CancelReason == CancelReason.NoSuitableSlot)
            {
                return Result<CancelForNoSuitableSlotResult>.Success(
                    new CancelForNoSuitableSlotResult(true, reloaded.Id));
            }

            return Result<CancelForNoSuitableSlotResult>.Success(
                new CancelForNoSuitableSlotResult(false, reloaded?.Id));
        }

        logger.LogInformation(
            "Reservation {ReservationId} cancelled due to no suitable slot. PrepaidAmount: {PrepaidAmount}",
            reservation.Id, reservation.PrepaidAmount);

        // Immediate refund attempt with 1-second timeout
        if (reservation.PrepaidAmount > 0m)
        {
            try
            {
                using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

                var refundResult = await paymentClient.RefundAsync(
                    reservation.Id,
                    reservation.UserId,
                    (long)reservation.PrepaidAmount,
                    "NoSuitableSlot",
                    linkedCts.Token);

                if (refundResult.IsSuccess && refundResult.Value == ReservationDebitState.Refunded)
                {
                    reservation.RecordCancellationRefunded();
                    await reservationRepository.SaveChangesAsync(cancellationToken);
                }
                else
                {
                    logger.LogWarning(
                        "Refund call unconfirmed for reservation {ReservationId}: {Error}. Reconciliation worker will finish it.",
                        reservation.Id, refundResult.Error?.Message ?? "RefundNotConfirmed");
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Exception calling refund for reservation {ReservationId} due to no suitable slot. Reconciliation worker will finish it.", reservation.Id);
            }
        }

        return Result<CancelForNoSuitableSlotResult>.Success(
            new CancelForNoSuitableSlotResult(true, reservation.Id));
    }
}
