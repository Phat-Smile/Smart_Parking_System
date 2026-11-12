using MediatR;
using Microsoft.Extensions.Logging;
using ParkingSystem.BuildingBlocks.Results;
using ParkingSystem.BuildingBlocks.Time;
using ParkingSystem.Contracts.Events.Parking;
using ParkingSystem.Parking.Application.Common.Messaging.Outbox;
using ParkingSystem.Parking.Application.Common.Realtime;
using ParkingSystem.Parking.Domain.ParkingLayout;

namespace ParkingSystem.Parking.Application.ParkingLayout.SetSlotMaintenance;

public sealed class SetSlotMaintenanceCommandHandler(
    ISlotMaintenanceRepository repository,
    IOutboxWriter outboxWriter,
    IClock clock,
    ILogger<SetSlotMaintenanceCommandHandler> logger,
    IParkingRealtimeNotifier? realtimeNotifier = null)
    : IRequestHandler<SetSlotMaintenanceCommand, Result<SlotMaintenanceResponse>>
{
    public async Task<Result<SlotMaintenanceResponse>> Handle(
        SetSlotMaintenanceCommand request,
        CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;

        // 1. Kiểm tra quyền sở hữu/quản lý bãi
        var canManage = await repository.CanManageLotAsync(
            request.LotId, request.CallerAccountId, request.CallerRole, cancellationToken);

        if (!canManage)
        {
            logger.LogWarning(
                "Account {AccountId} with role {Role} is not authorized to manage lot {LotId}",
                request.CallerAccountId, request.CallerRole, request.LotId);
            return Result<SlotMaintenanceResponse>.Failure(SlotMaintenanceErrors.LotNotFoundOrNotOwned);
        }

        // 2. Tra cứu vị trí đỗ
        var slot = await repository.FindSlotAsync(request.LotId, request.SlotId, cancellationToken);
        if (slot is null)
        {
            logger.LogWarning(
                "Slot {SlotId} not found in lot {LotId}", request.SlotId, request.LotId);
            return Result<SlotMaintenanceResponse>.Failure(SlotMaintenanceErrors.SlotNotFound);
        }

        // 3. Kiểm tra nếu ô đang có xe hoặc đang xử lý phiên
        if (request.UnderMaintenance && (slot.AssignedSessionId is not null ||
                                         slot.DispatchState is not null ||
                                         slot.SensorState == SensorState.Occupied))
        {
            logger.LogWarning(
                "Slot {SlotCode} ({SlotId}) in lot {LotId} is active and cannot be set to maintenance.",
                slot.SlotCode, slot.Id, request.LotId);
            return Result<SlotMaintenanceResponse>.Failure(SlotMaintenanceErrors.CannotMaintainActiveSlot);
        }

        var previousStatus = slot.EffectiveStatus;

        try
        {
            slot.SetMaintenance(request.UnderMaintenance);
        }
        catch (InvalidOperationException ex)
        {
            logger.LogWarning(ex, "Domain rejected maintenance transition for slot {SlotId}", slot.Id);
            return Result<SlotMaintenanceResponse>.Failure(SlotMaintenanceErrors.CannotMaintainActiveSlot);
        }

        // 4. Nếu trạng thái hiệu lực thay đổi: phát Outbox Event và SignalR Realtime Broadcast
        if (slot.EffectiveStatus != previousStatus)
        {
            outboxWriter.Enqueue(
                new SlotStatusChangedIntegrationEvent(
                    Guid.NewGuid(),
                    slot.LotId,
                    slot.ZoneId,
                    slot.Id,
                    slot.SlotCode,
                    slot.EffectiveStatus.ToString(),
                    previousStatus.ToString(),
                    "ManualMaintenance",
                    now,
                    slot.RowVersion),
                "parking.slot-status-changed.v1",
                1);

            if (realtimeNotifier is not null)
            {
                await realtimeNotifier.NotifySlotStatusChangedAsync(
                    slot.LotId,
                    slot.Id,
                    slot.SlotCode,
                    slot.EffectiveStatus.ToString(),
                    previousStatus.ToString(),
                    now,
                    cancellationToken);

                var availableCarSlots = await repository.GetAvailableCarSlotsAsync(slot.LotId, cancellationToken);
                await realtimeNotifier.NotifyLotOccupancyChangedAsync(
                    slot.LotId,
                    availableCarSlots,
                    0,
                    now,
                    cancellationToken);
            }
        }

        await repository.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Slot {SlotCode} ({SlotId}) in lot {LotId} maintenance state updated to {UnderMaintenance}. New status: {EffectiveStatus}",
            slot.SlotCode, slot.Id, request.LotId, request.UnderMaintenance, slot.EffectiveStatus);

        return Result<SlotMaintenanceResponse>.Success(new SlotMaintenanceResponse(
            LotId: slot.LotId,
            SlotId: slot.Id,
            SlotCode: slot.SlotCode,
            EffectiveStatus: slot.EffectiveStatus.ToString(),
            OverrideState: slot.OverrideState?.ToString(),
            UpdatedAtUtc: now));
    }
}
