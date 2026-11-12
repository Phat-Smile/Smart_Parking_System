using MediatR;
using ParkingSystem.BuildingBlocks.Results;

namespace ParkingSystem.Parking.Application.ParkingLayout.SetSlotMaintenance;

public sealed record SetSlotMaintenanceCommand(
    Guid LotId,
    Guid SlotId,
    Guid CallerAccountId,
    string CallerRole,
    bool UnderMaintenance,
    string? Reason = null) : IRequest<Result<SlotMaintenanceResponse>>;

public sealed record SlotMaintenanceResponse(
    Guid LotId,
    Guid SlotId,
    string SlotCode,
    string EffectiveStatus,
    string? OverrideState,
    DateTimeOffset UpdatedAtUtc);

public static class SlotMaintenanceErrors
{
    public static readonly Error SlotNotFound = new(
        "parking.slot.not_found", "The specified parking slot was not found in this lot.");

    public static readonly Error CannotMaintainActiveSlot = new(
        "parking.slot.cannot_maintain_active", "Cannot place an active or occupied slot under maintenance.");

    public static readonly Error LotNotFoundOrNotOwned = new(
        "parking.slots.lot_not_found", "The parking lot does not exist or you do not have permission to manage it.");
}
