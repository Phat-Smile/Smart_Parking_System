using Microsoft.AspNetCore.SignalR;

namespace ParkingSystem.Parking.WebAPI.Hubs;

public interface IParkingHubClient
{
    Task SlotStatusChanged(SlotStatusChangedMessage message);
    Task LotOccupancyChanged(LotOccupancyChangedMessage message);
    Task SlotNeedsVerification(SlotNeedsVerificationMessage message);
}

public sealed record SlotStatusChangedMessage(
    Guid LotId,
    Guid SlotId,
    string SlotCode,
    string EffectiveStatus,
    string PreviousStatus,
    DateTimeOffset Timestamp);

public sealed record LotOccupancyChangedMessage(
    Guid LotId,
    int AvailableCarSlots,
    int AvailableMotorbikeSlots,
    DateTimeOffset Timestamp);

public sealed record SlotNeedsVerificationMessage(
    Guid LotId,
    Guid SlotId,
    string SlotCode,
    string Stage,
    DateTimeOffset Timestamp);

public sealed class ParkingHub : Hub<IParkingHubClient>
{
    public static string GetLotGroupName(Guid lotId) => $"lot_{lotId}";

    public async Task JoinLotGroup(Guid lotId)
    {
        if (lotId != Guid.Empty)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, GetLotGroupName(lotId));
        }
    }

    public async Task LeaveLotGroup(Guid lotId)
    {
        if (lotId != Guid.Empty)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, GetLotGroupName(lotId));
        }
    }
}
