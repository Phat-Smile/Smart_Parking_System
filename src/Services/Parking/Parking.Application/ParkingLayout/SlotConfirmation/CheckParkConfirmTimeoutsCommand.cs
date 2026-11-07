using MediatR;
using ParkingSystem.BuildingBlocks.Results;
using ParkingSystem.BuildingBlocks.Time;
using ParkingSystem.Contracts.Events.Parking;
using ParkingSystem.Parking.Application.Common.Messaging.Outbox;
using ParkingSystem.Parking.Application.Common.Realtime;
using ParkingSystem.Parking.Domain.Incidents;
using ParkingSystem.Parking.Domain.ParkingLayout;

namespace ParkingSystem.Parking.Application.ParkingLayout.SlotConfirmation;

// US-PARK-005 (BR-04.e): run on a timer. The system never gives a slot back by itself: it only moves the slot to
// NeedsVerification and tells people. Staff are told through an incident, Outbox and SignalR (lead decision 2026-11-06).
public sealed record CheckParkConfirmTimeoutsCommand : IRequest<Result<ParkConfirmTimeoutsChecked>>;

public sealed record ParkConfirmTimeoutsChecked(int MovedToNeedsVerification, int OwnersNotified);

public sealed class CheckParkConfirmTimeoutsCommandHandler(
    ISlotConfirmationRepository slots,
    IOutboxWriter outbox,
    IClock clock,
    IParkingRealtimeNotifier? realtimeNotifier = null) : IRequestHandler<CheckParkConfirmTimeoutsCommand, Result<ParkConfirmTimeoutsChecked>>
{
    public const string StaffStage = "Staff";
    public const string OwnerStage = "Owner";
    private const int BatchSize = 100;
    private const string EventType = "parking.slot-needs-verification.v1";

    public async Task<Result<ParkConfirmTimeoutsChecked>> Handle(CheckParkConfirmTimeoutsCommand request, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var moved = 0;
        var notified = 0;

        foreach (var slot in await slots.ListAssignedBeforeAsync(now - ParkingSlot.ParkConfirmTimeout, BatchSize, cancellationToken))
        {
            if (!await MoveToNeedsVerificationAsync(slot, now, cancellationToken))
                return Result<ParkConfirmTimeoutsChecked>.Success(new ParkConfirmTimeoutsChecked(moved, notified));

            moved++;
        }

        foreach (var slot in await slots.ListOwnerNotNotifiedBeforeAsync(now - ParkingSlot.OwnerNotifyDelay, BatchSize, cancellationToken))
        {
            if (!await NotifyOwnerAsync(slot, now, cancellationToken))
                break;

            notified++;
        }

        return Result<ParkConfirmTimeoutsChecked>.Success(new ParkConfirmTimeoutsChecked(moved, notified));
    }

    // One save per slot. A failed save means staff changed that slot meanwhile; it also clears the tracked slots, so the
    // run stops here and the next tick picks up the rest.
    private async Task<bool> MoveToNeedsVerificationAsync(ParkingSlot slot, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var previousStatus = slot.EffectiveStatus;
        slot.MarkNeedsVerification(now);
        var incident = Incident.OpenParkConfirmTimeout(Guid.NewGuid(), slot.LotId, slot.AssignedSessionId, slot.Id, slot.AssignedAt ?? now);
        slots.AddIncident(incident);
        SlotConfirmationEvents.EnqueueStatusChanged(outbox, slot, previousStatus, "System", now);
        EnqueueNeedsVerification(slot, incident.Id, StaffStage, now);

        if (!await slots.TrySaveChangesAsync(cancellationToken))
            return false;

        await SlotConfirmationEvents.NotifyAsync(realtimeNotifier, slot, previousStatus, now, cancellationToken);
        await NotifyNeedsVerificationAsync(slot, StaffStage, now, cancellationToken);
        return true;
    }

    private async Task<bool> NotifyOwnerAsync(ParkingSlot slot, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var incident = await slots.FindOpenTimeoutIncidentAsync(slot.Id, cancellationToken);
        slot.MarkOwnerNotified(now);
        EnqueueNeedsVerification(slot, incident?.Id, OwnerStage, now);

        if (!await slots.TrySaveChangesAsync(cancellationToken))
            return false;

        await NotifyNeedsVerificationAsync(slot, OwnerStage, now, cancellationToken);
        return true;
    }

    private void EnqueueNeedsVerification(ParkingSlot slot, Guid? incidentId, string stage, DateTimeOffset now) =>
        outbox.Enqueue(
            new ParkingSlotNeedsVerificationIntegrationEvent(slot.LotId, slot.Id, slot.SlotCode, slot.AssignedSessionId, incidentId, stage, now),
            EventType,
            1);

    private Task NotifyNeedsVerificationAsync(ParkingSlot slot, string stage, DateTimeOffset now, CancellationToken cancellationToken) =>
        realtimeNotifier?.NotifySlotNeedsVerificationAsync(slot.LotId, slot.Id, slot.SlotCode, stage, now, cancellationToken)
        ?? Task.CompletedTask;
}
