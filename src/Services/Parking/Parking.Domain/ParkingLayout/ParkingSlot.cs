namespace ParkingSystem.Parking.Domain.ParkingLayout;

// A car slot. Motorbike zones have no slots (BR-07.b).
// Each slot has three status layers written by different sources (BR-06.a):
// sensor (slot sensor events), override (staff or layout) and dispatch (gate-entry flow, BR-04).
public sealed class ParkingSlot
{
    public const int SlotCodeMaxLength = 20;

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
        EffectiveStatus = ComputeEffectiveStatus(SensorState, OverrideState, DispatchState);
        RowVersion++;
    }

    // ASSUMPTION (context 6, open point 21): priority Unavailable > Maintenance > NeedsVerification > Assigned
    // > Occupied > Available; the earlier state wins. If the mentor changes the order, only this method changes.
    public static SlotEffectiveStatus ComputeEffectiveStatus(SensorState? sensor, OverrideState? @override, DispatchState? dispatch)
    {
        if (@override == ParkingLayout.OverrideState.Unavailable)
            return SlotEffectiveStatus.Unavailable;
        if (@override == ParkingLayout.OverrideState.Maintenance)
            return SlotEffectiveStatus.Maintenance;
        if (dispatch == ParkingLayout.DispatchState.NeedsVerification)
            return SlotEffectiveStatus.NeedsVerification;
        if (dispatch == ParkingLayout.DispatchState.Assigned)
            return SlotEffectiveStatus.Assigned;
        if (sensor == ParkingLayout.SensorState.Occupied)
            return SlotEffectiveStatus.Occupied;
        return SlotEffectiveStatus.Available;
    }
}
