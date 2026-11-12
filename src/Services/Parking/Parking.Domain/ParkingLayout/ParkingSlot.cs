namespace ParkingSystem.Parking.Domain.ParkingLayout;

// A car slot. Motorbike zones have no slots (BR-07.b).
// Each slot has three status layers written by different sources (BR-06.a):
// sensor (slot sensor events), override (staff or layout) and dispatch (gate-entry flow, BR-04).
public sealed class ParkingSlot
{
    public const int SlotCodeMaxLength = 20;

    // BR-04.e: an assignment without a parking confirmation goes to NeedsVerification after 10 minutes,
    // and the Owner is told after 30 minutes in that state.
    public static readonly TimeSpan ParkConfirmTimeout = TimeSpan.FromMinutes(10);
    public static readonly TimeSpan OwnerNotifyDelay = TimeSpan.FromMinutes(30);

    private ParkingSlot()
    {
    }

    public Guid Id { get; private set; }
    public Guid LotId { get; private set; }
    public Guid ZoneId { get; private set; }

    // Also part of the sensor MQTT topic lot/{lotId}/slot/{slotCode} (context section 11).
    public string SlotCode { get; private set; } = null!;

    public SensorState? SensorState { get; private set; }
    public OverrideState? OverrideState { get; private set; }
    public DispatchState? DispatchState { get; private set; }

    // Set together with DispatchState.Assigned (BR-04.a).
    public Guid? AssignedSessionId { get; private set; }
    public DateTimeOffset? AssignedAt { get; private set; }

    // Lead decision 2026-11-06 (not in the team ERD yet): staff confirmed the car is parked. It is not sensor data, and the
    // dispatch layer is finished, so it has its own value; the session stays on AssignedSessionId until the car leaves.
    public DateTimeOffset? ManuallyParkedAt { get; private set; }

    // BR-04.e: start of the 30 minutes before the Owner is told.
    public DateTimeOffset? NeedsVerificationSince { get; private set; }

    // Not in the team ERD yet: the Owner is told once per NeedsVerification period.
    public DateTimeOffset? OwnerNotifiedAt { get; private set; }

    // Stored so counts and the atomic designation can filter on it in SQL; recomputed whenever a layer changes.
    public SlotEffectiveStatus EffectiveStatus { get; private set; }

    // Bumped on every state change (ERD row_version), so later EF updates can detect a concurrent change.
    public int RowVersion { get; private set; }

    public static ParkingSlot Create(Guid id, Guid lotId, Guid zoneId, string slotCode, OverrideState? declaredOverride = null)
    {
        var slot = new ParkingSlot
        {
            Id = id,
            LotId = lotId,
            ZoneId = zoneId,
            SlotCode = NormalizeSlotCode(slotCode),
            // BR-06.c: a slot declared Unavailable in the layout belongs to the override layer.
            OverrideState = declaredOverride
        };
        slot.EffectiveStatus = ComputeEffectiveStatus(slot.SensorState, slot.OverrideState, slot.DispatchState);
        return slot;
    }

    public static string NormalizeSlotCode(string slotCode) => slotCode.Trim().ToUpperInvariant();

    // Lead decision (review of PR #67/#68): when staff cancel a session (the car did not enter), its slot goes back
    // to the pool. Only the dispatch layer is cleared; sensor and override layers keep their own values.
    public void ReleaseAssignment()
    {
        DispatchState = null;
        AssignedSessionId = null;
        AssignedAt = null;
        ManuallyParkedAt = null;
        NeedsVerificationSince = null;
        OwnerNotifiedAt = null;
        Recompute();
    }

    /// <summary>
    /// US-PARK-005 (BR-04.c): staff confirm the car is parked. Possible at an Assigned slot of a zone without slot sensors,
    /// and at a NeedsVerification slot of any zone (transition table after BR-06).
    /// </summary>
    /// <returns>False when the slot is in no state staff can confirm.</returns>
    public bool ConfirmParked(bool zoneHasSlotSensor, DateTimeOffset confirmedAt)
    {
        var canConfirm = DispatchState == ParkingLayout.DispatchState.NeedsVerification
            || (DispatchState == ParkingLayout.DispatchState.Assigned && !zoneHasSlotSensor);
        if (!canConfirm)
            return false;

        DispatchState = null;
        ManuallyParkedAt = confirmedAt;
        NeedsVerificationSince = null;
        OwnerNotifiedAt = null;
        Recompute();
        return true;
    }

    /// <summary>US-PARK-005: staff checked a NeedsVerification slot and no car is there, so the slot goes back to the pool.</summary>
    /// <returns>False when the slot is not NeedsVerification.</returns>
    public bool ConfirmEmpty()
    {
        if (DispatchState != ParkingLayout.DispatchState.NeedsVerification)
            return false;

        ReleaseAssignment();
        return true;
    }

    // ASSUMPTION (context 6, open point 21): priority Unavailable > Maintenance > NeedsVerification > Assigned
    // > Occupied > Available; the earlier state wins. If the mentor changes the order, only this method changes.
    public static SlotEffectiveStatus ComputeEffectiveStatus(
        SensorState? sensor, OverrideState? @override, DispatchState? dispatch, bool manuallyParked = false)
    {
        if (@override == ParkingLayout.OverrideState.Unavailable)
            return SlotEffectiveStatus.Unavailable;
        if (@override == ParkingLayout.OverrideState.Maintenance)
            return SlotEffectiveStatus.Maintenance;
        if (dispatch == ParkingLayout.DispatchState.NeedsVerification)
            return SlotEffectiveStatus.NeedsVerification;
        if (dispatch == ParkingLayout.DispatchState.Assigned)
            return SlotEffectiveStatus.Assigned;
        if (sensor == ParkingLayout.SensorState.Occupied || manuallyParked)
            return SlotEffectiveStatus.Occupied;
        return SlotEffectiveStatus.Available;
    }
    // Layout may change labels/declared override only when no live operation owns the slot.
    // Sensor and dispatch layers must never be overwritten by an Owner's design payload.
    public bool Reconfigure(string slotCode, OverrideState? declaredOverride)
    {
        var normalized = NormalizeSlotCode(slotCode);
        if (SlotCode == normalized && OverrideState == declaredOverride) return false;
        if (AssignedSessionId is not null || DispatchState is not null || SensorState == ParkingLayout.SensorState.Occupied)
            throw new InvalidOperationException("An active slot cannot be edited in the layout.");
        SlotCode = normalized;
        OverrideState = declaredOverride;
        EffectiveStatus = ComputeEffectiveStatus(SensorState, OverrideState, DispatchState);
        RowVersion++;
        return true;
    }

    public void SetMaintenance(bool underMaintenance)
    {
        var targetOverride = underMaintenance ? ParkingLayout.OverrideState.Maintenance : (OverrideState?)null;
        if (OverrideState == targetOverride) return;

        if (underMaintenance && (AssignedSessionId is not null || DispatchState is not null || SensorState == ParkingLayout.SensorState.Occupied))
            throw new InvalidOperationException("An active slot cannot be placed under maintenance.");

        OverrideState = targetOverride;
        EffectiveStatus = ComputeEffectiveStatus(SensorState, OverrideState, DispatchState, ManuallyParkedAt is not null);
        RowVersion++;
    }

    public void UpdateSensorState(SensorState? sensorState)
    {
        SensorState = sensorState;

        // BR-04.c, lead decision 2026-11-06: a car on the sensor of an Assigned slot ends the dispatch state.
        // The session stays on AssignedSessionId until the car leaves.
        if (sensorState == ParkingLayout.SensorState.Occupied && DispatchState == ParkingLayout.DispatchState.Assigned)
            DispatchState = null;

        Recompute();
    }

    public void AssignSession(Guid sessionId, DateTimeOffset assignedAt)
    {
        DispatchState = ParkingLayout.DispatchState.Assigned;
        AssignedSessionId = sessionId;
        AssignedAt = assignedAt;
        EffectiveStatus = ComputeEffectiveStatus(SensorState, OverrideState, DispatchState);
        RowVersion++;
    }

    public void MarkNeedsVerification(DateTimeOffset since)
    {
        DispatchState = ParkingLayout.DispatchState.NeedsVerification;
        NeedsVerificationSince = since;
        OwnerNotifiedAt = null;
        Recompute();
    }

    public void MarkOwnerNotified(DateTimeOffset notifiedAt)
    {
        OwnerNotifiedAt = notifiedAt;
        RowVersion++;
    }

    private void Recompute()
    {
        EffectiveStatus = ComputeEffectiveStatus(SensorState, OverrideState, DispatchState, ManuallyParkedAt is not null);
        RowVersion++;
    }
}
