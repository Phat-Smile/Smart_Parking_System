using FluentValidation;
using MediatR;
using ParkingSystem.BuildingBlocks.Identity;
using ParkingSystem.BuildingBlocks.Results;
using ParkingSystem.BuildingBlocks.Time;
using ParkingSystem.Contracts.Events.Parking;
using ParkingSystem.Parking.Application.Common.Auditing;
using ParkingSystem.Parking.Application.Common.Messaging.Outbox;
using ParkingSystem.Parking.Application.Common.Realtime;
using ParkingSystem.Parking.Application.ParkingLots;
using ParkingSystem.Parking.Application.ParkingSessions;
using ParkingSystem.Parking.Domain.Incidents;
using ParkingSystem.Parking.Domain.ParkingLayout;
using ParkingSystem.Parking.Domain.ParkingSessions;

namespace ParkingSystem.Parking.Application.ParkingLayout.SlotConfirmation;

// US-PARK-005 (BR-04.c, BR-04.e, MF-14): staff check a slot in person and say whether the car is parked there.
public sealed record ConfirmSlotParkedCommand(Guid SlotId, string? Reason, Guid ActorUserId) : IRequest<Result<ConfirmedSlot>>;

// Transition table after BR-06: NeedsVerification to Available needs a reason. Lead decision 2026-11-06: an open session
// stays open, loses the slot, and the incident stays open for staff.
public sealed record ConfirmSlotEmptyCommand(Guid SlotId, string Reason, Guid ActorUserId) : IRequest<Result<ConfirmedSlot>>;

public sealed record ConfirmedSlot(Guid SlotId, string SlotCode, string EffectiveStatus, DateTimeOffset ConfirmedAt);

public static class ConfirmSlotErrors
{
    public const int ReasonMaxLength = 500;

    public static readonly Error SlotNotFound =
        new("parking.slot.not_found", "The parking slot does not exist.");

    // Also a zone with slot sensors while the slot is only Assigned: there the sensor confirms (BR-04.c).
    public static readonly Error NotConfirmable =
        new("parking.slot.not_confirmable", "This parking slot is not waiting for a parking confirmation.");

    public static readonly Error NotNeedsVerification =
        new("parking.slot.not_needs_verification", "This parking slot is not waiting for verification.");

    // The slot changed while staff were confirming it, for example another staff member confirmed it first.
    public static readonly Error SlotChanged =
        new("parking.slot.changed", "This parking slot has just changed. Refresh and try again.");
}

public sealed class ConfirmSlotParkedCommandValidator : AbstractValidator<ConfirmSlotParkedCommand>
{
    public ConfirmSlotParkedCommandValidator()
    {
        RuleFor(command => command.SlotId).NotEmpty();
        RuleFor(command => command.ActorUserId).NotEmpty();
        RuleFor(command => command.Reason).MaximumLength(ConfirmSlotErrors.ReasonMaxLength);
    }
}

public sealed class ConfirmSlotEmptyCommandValidator : AbstractValidator<ConfirmSlotEmptyCommand>
{
    public ConfirmSlotEmptyCommandValidator()
    {
        RuleFor(command => command.SlotId).NotEmpty();
        RuleFor(command => command.ActorUserId).NotEmpty();
        RuleFor(command => command.Reason)
            .Must(reason => !string.IsNullOrWhiteSpace(reason))
            .MaximumLength(ConfirmSlotErrors.ReasonMaxLength);
    }
}

public sealed class ConfirmSlotParkedCommandHandler(
    ISlotConfirmationRepository slots,
    IParkingLotAccess lotAccess,
    IAuditLog auditLog,
    IOutboxWriter outbox,
    IClock clock,
    IParkingRealtimeNotifier? realtimeNotifier = null) : IRequestHandler<ConfirmSlotParkedCommand, Result<ConfirmedSlot>>
{
    public async Task<Result<ConfirmedSlot>> Handle(ConfirmSlotParkedCommand request, CancellationToken cancellationToken)
    {
        if (await slots.FindWithZoneAsync(request.SlotId, cancellationToken) is not { } found)
            return Result<ConfirmedSlot>.Failure(ConfirmSlotErrors.SlotNotFound);

        var slot = found.Slot;

        // TECH-12: the lot comes from the slot, not from the client.
        if (!await lotAccess.CanOperateAsync(request.ActorUserId, slot.LotId, cancellationToken))
            return Result<ConfirmedSlot>.Failure(ParkingLotAccessErrors.AccessDenied);

        var previousStatus = slot.EffectiveStatus;
        var now = clock.UtcNow;
        if (!slot.ConfirmParked(found.ZoneHasSlotSensor, now))
            return Result<ConfirmedSlot>.Failure(ConfirmSlotErrors.NotConfirmable);

        var reason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim();
        var incident = await slots.FindOpenTimeoutIncidentAsync(slot.Id, cancellationToken);
        incident?.Resolve(request.ActorUserId, now, reason ?? "Staff confirmed the car is parked in this slot.");

        SlotConfirmationEvents.RecordChange(auditLog, outbox, AuditActions.SlotParkedConfirmed, slot, previousStatus, request.ActorUserId, reason, now);

        if (!await slots.TrySaveChangesAsync(cancellationToken))
            return Result<ConfirmedSlot>.Failure(ConfirmSlotErrors.SlotChanged);

        await SlotConfirmationEvents.NotifyAsync(realtimeNotifier, slot, previousStatus, now, cancellationToken);
        return Result<ConfirmedSlot>.Success(new ConfirmedSlot(slot.Id, slot.SlotCode, slot.EffectiveStatus.ToString(), now));
    }
}

public sealed class ConfirmSlotEmptyCommandHandler(
    ISlotConfirmationRepository slots,
    IParkingSessionRepository sessions,
    IParkingLotAccess lotAccess,
    IAuditLog auditLog,
    IOutboxWriter outbox,
    IClock clock,
    IParkingRealtimeNotifier? realtimeNotifier = null) : IRequestHandler<ConfirmSlotEmptyCommand, Result<ConfirmedSlot>>
{
    public async Task<Result<ConfirmedSlot>> Handle(ConfirmSlotEmptyCommand request, CancellationToken cancellationToken)
    {
        if (await slots.FindWithZoneAsync(request.SlotId, cancellationToken) is not { } found)
            return Result<ConfirmedSlot>.Failure(ConfirmSlotErrors.SlotNotFound);

        var slot = found.Slot;

        // TECH-12: the lot comes from the slot, not from the client.
        if (!await lotAccess.CanOperateAsync(request.ActorUserId, slot.LotId, cancellationToken))
            return Result<ConfirmedSlot>.Failure(ParkingLotAccessErrors.AccessDenied);

        var sessionId = slot.AssignedSessionId;
        var assignedAt = slot.AssignedAt;
        var previousStatus = slot.EffectiveStatus;
        var now = clock.UtcNow;
        if (!slot.ConfirmEmpty())
            return Result<ConfirmedSlot>.Failure(ConfirmSlotErrors.NotNeedsVerification);

        var reason = request.Reason.Trim();
        var session = sessionId is { } id ? await sessions.FindByIdAsync(id, cancellationToken) : null;
        var sessionOpen = session is not null && ParkingSession.OpenStatuses.Contains(session.Status);
        var incident = await slots.FindOpenTimeoutIncidentAsync(slot.Id, cancellationToken);

        if (sessionOpen)
        {
            // The car is somewhere in the lot without a slot, so the case stays open for staff.
            if (incident is null)
                slots.AddIncident(Incident.OpenParkConfirmTimeout(Guid.NewGuid(), slot.LotId, sessionId, slot.Id, assignedAt ?? now));
        }
        else
        {
            incident?.Resolve(request.ActorUserId, now, reason);
        }

        SlotConfirmationEvents.RecordChange(auditLog, outbox, AuditActions.SlotEmptyConfirmed, slot, previousStatus, request.ActorUserId, reason, now);

        if (!await slots.TrySaveChangesAsync(cancellationToken))
            return Result<ConfirmedSlot>.Failure(ConfirmSlotErrors.SlotChanged);

        await SlotConfirmationEvents.NotifyAsync(realtimeNotifier, slot, previousStatus, now, cancellationToken);
        return Result<ConfirmedSlot>.Success(new ConfirmedSlot(slot.Id, slot.SlotCode, slot.EffectiveStatus.ToString(), now));
    }
}

internal static class SlotConfirmationEvents
{
    // NFR-16: who, when and why. Saved with the slot change, and the status event goes out through the same commit.
    public static void RecordChange(
        IAuditLog auditLog,
        IOutboxWriter outbox,
        string action,
        ParkingSlot slot,
        SlotEffectiveStatus previousStatus,
        Guid actorUserId,
        string? reason,
        DateTimeOffset now)
    {
        auditLog.Record(new AuditEntry(
            action,
            actorUserId,
            slot.LotId,
            nameof(ParkingSlot),
            slot.Id.ToString(),
            reason,
            now,
            new Dictionary<string, string>
            {
                ["fromStatus"] = previousStatus.ToString(),
                ["toStatus"] = slot.EffectiveStatus.ToString()
            }));

        EnqueueStatusChanged(outbox, slot, previousStatus, "Staff", now);
    }

    public static void EnqueueStatusChanged(IOutboxWriter outbox, ParkingSlot slot, SlotEffectiveStatus previousStatus, string source, DateTimeOffset now) =>
        outbox.Enqueue(
            new SlotStatusChangedIntegrationEvent(
                Guid.NewGuid(),
                slot.LotId,
                slot.ZoneId,
                slot.Id,
                slot.SlotCode,
                slot.EffectiveStatus.ToString(),
                previousStatus.ToString(),
                source,
                now,
                slot.RowVersion),
            "parking.slot-status-changed.v1",
            1);

    public static Task NotifyAsync(
        IParkingRealtimeNotifier? notifier, ParkingSlot slot, SlotEffectiveStatus previousStatus, DateTimeOffset now, CancellationToken cancellationToken) =>
        notifier?.NotifySlotStatusChangedAsync(
            slot.LotId, slot.Id, slot.SlotCode, slot.EffectiveStatus.ToString(), previousStatus.ToString(), now, cancellationToken)
        ?? Task.CompletedTask;
}
