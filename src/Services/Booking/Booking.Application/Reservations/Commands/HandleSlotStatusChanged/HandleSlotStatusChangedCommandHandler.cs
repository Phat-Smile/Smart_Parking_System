using MediatR;
using Microsoft.Extensions.Logging;
using ParkingSystem.Booking.Application.Common.Messaging.Outbox;
using ParkingSystem.Booking.Application.Payments;
using ParkingSystem.Booking.Application.Reservations;
using ParkingSystem.Booking.Domain.Reservations;
using ParkingSystem.BuildingBlocks.Results;
using ParkingSystem.BuildingBlocks.Time;
using ParkingSystem.Contracts.Events.Booking;

namespace ParkingSystem.Booking.Application.Reservations.Commands.HandleSlotStatusChanged;

public sealed class HandleSlotStatusChangedCommandHandler(
    IReservationRepository reservationRepository,
    IReservationPaymentClient paymentClient,
    IClock clock,
    ILogger<HandleSlotStatusChangedCommandHandler> logger,
    IOutboxWriter outbox)
    : IRequestHandler<HandleSlotStatusChangedCommand, Result<SlotStatusChangedOutcomeDto>>
{
    private static readonly HashSet<string> DisruptiveStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "Maintenance",
        "Unavailable"
    };

    public async Task<Result<SlotStatusChangedOutcomeDto>> Handle(
        HandleSlotStatusChangedCommand request,
        CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Booking received slot status change for Lot {LotId}, Slot {SlotId} ({SlotCode}): {PreviousStatus} -> {EffectiveStatus} (Source: {Source}, Version: {Version}).",
            request.LotId,
            request.SlotId,
            request.SlotCode,
            request.PreviousStatus,
            request.EffectiveStatus,
            request.Source,
            request.Version);

        // If the slot transitioned to Maintenance or Unavailable, active holds for this lot need verification.
        if (!DisruptiveStatuses.Contains(request.EffectiveStatus))
        {
            return Result<SlotStatusChangedOutcomeDto>.Success(new SlotStatusChangedOutcomeDto(
                request.LotId,
                request.SlotId,
                request.SlotCode,
                request.EffectiveStatus,
                0,
                $"Trạng thái '{request.EffectiveStatus}' của chỗ đỗ {request.SlotCode} không ảnh hưởng đến các lượt giữ chỗ hiện tại."));
        }

        var activeReservations = await reservationRepository.ListActiveByLotIdAsync(request.LotId, cancellationToken);
        if (activeReservations.Count == 0)
        {
            logger.LogInformation("No active reservations found for lot {LotId} when slot {SlotCode} became {EffectiveStatus}.",
                request.LotId, request.SlotCode, request.EffectiveStatus);

            return Result<SlotStatusChangedOutcomeDto>.Success(new SlotStatusChangedOutcomeDto(
                request.LotId,
                request.SlotId,
                request.SlotCode,
                request.EffectiveStatus,
                0,
                $"Không có lượt giữ chỗ nào bị ảnh hưởng tại bãi {request.LotId}."));
        }

        var now = clock.UtcNow;
        var cancelledCount = 0;
        bool? refundSucceeded = null;

        // When a slot is disabled, cancel temporary holds first before confirmed reservations
        var candidates = activeReservations
            .OrderBy(r => r.Status == ReservationStatus.TemporaryHold ? 0 : 1)
            .ThenBy(r => r.CreatedAt)
            .Take(1)
            .ToList();

        foreach (var reservation in candidates)
        {
            try
            {
                if (reservation.Status == ReservationStatus.Confirmed)
                {
                    reservation.CancelForNoSuitableSlot(now);

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
                    cancelledCount++;

                    if (reservation.PrepaidAmount > 0m)
                    {
                        refundSucceeded = false;
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
                                refundSucceeded = true;
                            }
                            else
                            {
                                logger.LogWarning("Refund call unconfirmed for reservation {ReservationId}: {Error}. Reconciliation worker will finish it.",
                                    reservation.Id, refundResult.Error?.Message ?? "RefundNotConfirmed");
                            }
                        }
                        catch (Exception ex)
                        {
                            logger.LogError(ex, "Exception calling refund for reservation {ReservationId} due to slot maintenance. Reconciliation worker will finish it.", reservation.Id);
                        }
                    }

                    logger.LogWarning(
                        "Confirmed reservation {ReservationId} cancelled automatically because slot {SlotCode} entered {EffectiveStatus}. Prepaid outcome: {Outcome}.",
                        reservation.Id,
                        request.SlotCode,
                        request.EffectiveStatus,
                        reservation.PrepaidOutcome);
                }
                else
                {
                    // TemporaryHold reservation
                    reservation.Cancel(now, CancelReason.NoSuitableSlot, null);

                    outbox.Enqueue(
                        new ReservationCancelledIntegrationEvent(
                            reservation.Id,
                            reservation.UserId,
                            reservation.LotId,
                            CancelReason.NoSuitableSlot.ToString(),
                            0m,
                            now,
                            forfeitedAmount: 0m),
                        "booking.reservation-cancelled.v1",
                        1,
                        correlationId: reservation.Id);

                    await reservationRepository.SaveChangesAsync(cancellationToken);
                    cancelledCount++;

                    logger.LogWarning(
                        "TemporaryHold reservation {ReservationId} cancelled automatically because slot {SlotCode} entered {EffectiveStatus}.",
                        reservation.Id,
                        request.SlotCode,
                        request.EffectiveStatus);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to cancel reservation {ReservationId} during slot maintenance.", reservation.Id);
            }
        }

        string outcomeMessage = refundSucceeded switch
        {
            true => $"Đã tự động xử lý hủy {cancelledCount} lượt giữ chỗ do chỗ đỗ {request.SlotCode} chuyển sang trạng thái {request.EffectiveStatus}. Tiền đã được hoàn về ví.",
            false => $"Đã tự động xử lý hủy {cancelledCount} lượt giữ chỗ do chỗ đỗ {request.SlotCode} chuyển sang trạng thái {request.EffectiveStatus}. Tiền hoàn đang được xử lý.",
            _ => $"Đã tự động xử lý hủy {cancelledCount} lượt giữ chỗ do chỗ đỗ {request.SlotCode} chuyển sang trạng thái {request.EffectiveStatus}."
        };

        return Result<SlotStatusChangedOutcomeDto>.Success(new SlotStatusChangedOutcomeDto(
            request.LotId,
            request.SlotId,
            request.SlotCode,
            request.EffectiveStatus,
            cancelledCount,
            outcomeMessage));
    }
}
