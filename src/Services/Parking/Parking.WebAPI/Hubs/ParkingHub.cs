using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using ParkingSystem.BuildingBlocks.Identity;
using ParkingSystem.Parking.WebAPI.Security;

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

public sealed class ParkingHub(IParkingLotAccess lotAccess) : Hub<IParkingHubClient>
{
    public static string GetPublicLotGroupName(Guid lotId) => $"lot_{lotId}_public";

    public static string GetOperationsLotGroupName(Guid lotId) => $"lot_{lotId}_ops";

    public async Task JoinLotGroup(Guid lotId)
    {
        if (lotId != Guid.Empty)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, GetPublicLotGroupName(lotId), Context.ConnectionAborted);
        }
    }

    public async Task LeaveLotGroup(Guid lotId)
    {
        if (lotId != Guid.Empty)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, GetPublicLotGroupName(lotId), Context.ConnectionAborted);
        }
    }

    [Authorize]
    public async Task JoinLotOperationsGroup(Guid lotId)
    {
        if (lotId == Guid.Empty)
        {
            throw new HubException("Mã bãi xe không hợp lệ.");
        }

        var user = Context.User;
        if (user?.Identity?.IsAuthenticated != true)
        {
            throw new HubException("Chưa xác thực người dùng.");
        }

        // TokenValidationParameters.NameClaimType is not mapped to "sub" because MapInboundClaims = false,
        // so Context.UserIdentifier can be null; read the "sub" claim directly.
        var subjectValue = user.FindFirst("sub")?.Value;
        if (!Guid.TryParse(subjectValue, out var accountId) || accountId == Guid.Empty)
        {
            throw new HubException("Không xác định được tài khoản hợp lệ.");
        }

        if (!user.HasClaim(ParkingAuthorization.RoleClaimType, ParkingAuthorization.AdminRole))
        {
            var isOperator = user.HasClaim(ParkingAuthorization.RoleClaimType, ParkingAuthorization.StaffRole)
                || user.HasClaim(ParkingAuthorization.RoleClaimType, ParkingAuthorization.OwnerRole);
            if (!isOperator || !await lotAccess.CanOperateAsync(accountId, lotId, Context.ConnectionAborted))
            {
                throw new HubException("Không có quyền theo dõi vận hành bãi xe này.");
            }
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, GetOperationsLotGroupName(lotId), Context.ConnectionAborted);
    }

    [Authorize]
    public async Task LeaveLotOperationsGroup(Guid lotId)
    {
        if (lotId != Guid.Empty)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, GetOperationsLotGroupName(lotId), Context.ConnectionAborted);
        }
    }
}
