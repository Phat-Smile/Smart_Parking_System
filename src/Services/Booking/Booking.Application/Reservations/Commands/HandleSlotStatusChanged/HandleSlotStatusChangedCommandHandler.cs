using MediatR;
using Microsoft.Extensions.Logging;
using ParkingSystem.Booking.Application.Reservations;
using ParkingSystem.Booking.Domain.Reservations;
using ParkingSystem.BuildingBlocks.Results;
using ParkingSystem.BuildingBlocks.Time;

namespace ParkingSystem.Booking.Application.Reservations.Commands.HandleSlotStatusChanged;

public sealed class HandleSlotStatusChangedCommandHandler(
    IReservationRepository reservationRepository,
    IClock clock,
    ILogger<HandleSlotStatusChangedCommandHandler> logger)
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
                var outcome = reservation.PrepaidAmount > 0 ? PrepaidOutcome.Refunded : (PrepaidOutcome?)null;
                reservation.Cancel(now, CancelReason.NoSuitableSlot, outcome);
                cancelledCount++;

                logger.LogWarning(
                    "Reservation {ReservationId} cancelled automatically because slot {SlotCode} entered {EffectiveStatus}. Prepaid outcome: {Outcome}.",
                    reservation.Id,
                    request.SlotCode,
                    request.EffectiveStatus,
                    outcome);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to cancel reservation {ReservationId} during slot maintenance.", reservation.Id);
            }
        }

        if (cancelledCount > 0)
        {
            await reservationRepository.SaveChangesAsync(cancellationToken);
        }

        return Result<SlotStatusChangedOutcomeDto>.Success(new SlotStatusChangedOutcomeDto(
            request.LotId,
            request.SlotId,
            request.SlotCode,
            request.EffectiveStatus,
            cancelledCount,
            $"Đã tự động xử lý hủy {cancelledCount} lượt giữ chỗ do chỗ đỗ {request.SlotCode} chuyển sang trạng thái {request.EffectiveStatus}."));
    }
}
