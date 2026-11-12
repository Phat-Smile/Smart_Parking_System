using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using ParkingSystem.Parking.Application.Common.Realtime;
using ParkingSystem.Parking.WebAPI.Hubs;

namespace ParkingSystem.Parking.WebAPI.Realtime;

public sealed class SignalRParkingRealtimeNotifier(
    IHubContext<ParkingHub, IParkingHubClient> hubContext,
    ILogger<SignalRParkingRealtimeNotifier> logger) : IParkingRealtimeNotifier
{
    public async Task NotifySlotStatusChangedAsync(
        Guid lotId,
        Guid slotId,
        string slotCode,
        string effectiveStatus,
        string previousStatus,
        DateTimeOffset timestamp,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var groupName = ParkingHub.GetOperationsLotGroupName(lotId);
            var message = new SlotStatusChangedMessage(lotId, slotId, slotCode, effectiveStatus, previousStatus, timestamp);
            await hubContext.Clients.Group(groupName).SlotStatusChanged(message);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to broadcast SlotStatusChanged for lot {LotId}", lotId);
        }
    }

    public async Task NotifySlotNeedsVerificationAsync(
        Guid lotId,
        Guid slotId,
        string slotCode,
        string stage,
        DateTimeOffset timestamp,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var groupName = ParkingHub.GetOperationsLotGroupName(lotId);
            var message = new SlotNeedsVerificationMessage(lotId, slotId, slotCode, stage, timestamp);
            await hubContext.Clients.Group(groupName).SlotNeedsVerification(message);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to broadcast SlotNeedsVerification for lot {LotId}", lotId);
        }
    }

    public async Task NotifyLotOccupancyChangedAsync(
        Guid lotId,
        int availableCarSlots,
        int availableMotorbikeSlots,
        DateTimeOffset timestamp,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var groupName = ParkingHub.GetPublicLotGroupName(lotId);
            var message = new LotOccupancyChangedMessage(lotId, availableCarSlots, availableMotorbikeSlots, timestamp);
            await hubContext.Clients.Group(groupName).LotOccupancyChanged(message);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to broadcast LotOccupancyChanged for lot {LotId}", lotId);
        }
    }
}
