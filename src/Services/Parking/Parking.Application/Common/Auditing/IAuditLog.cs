namespace ParkingSystem.Parking.Application.Common.Auditing;

// NFR-16: sensitive staff actions record who, when and why. Each service keeps its own audit log.
public interface IAuditLog
{
    /// <summary>Adds an entry that is saved in the same transaction as the business change it describes.</summary>
    void Record(AuditEntry entry);
}

/// <param name="ActorUserId">Account id of the staff member (token "sub", identity.users.id).</param>
/// <param name="Details">Extra values such as the status change. Never put a full plate number here (NFR-15).</param>
public sealed record AuditEntry(
    string Action,
    Guid ActorUserId,
    Guid? LotId,
    string TargetType,
    string TargetId,
    string? Reason,
    DateTimeOffset OccurredAt,
    IReadOnlyDictionary<string, string>? Details = null);

public static class AuditActions
{
    public const string SessionCancelled = "session.cancelled";
    public const string SessionQrReissued = "session.qr_reissued";
    public const string SessionExitWindowExtended = "session.exit_window_extended";
    public const string SessionExitManuallyVerified = "session.exit_manually_verified";
    public const string LotMemberAssigned = "lot.member_assigned";
    public const string LotMemberRemoved = "lot.member_removed";
    public const string LotStaffAssigned = "lot.staff_assigned";
    public const string LotStaffRemoved = "lot.staff_removed";
    public const string ParkingLotApproved = "parking_lot.approved";
    public const string ParkingLotRejected = "parking_lot.rejected";
    public const string GateReadingConfirmed = "gate_reading.confirmed";
    public const string GateArrivalOverride = "gate_arrival.override_confirmed";
    public const string GateReadingAutoAccepted = "gate_reading.auto_accepted";
    public const string SlotParkedConfirmed = "slot.parked_confirmed";
    public const string SlotEmptyConfirmed = "slot.empty_confirmed";
    public const string ParkingLotOpeningHoursUpdated = "parking_lot.opening_hours_updated";
    public const string ParkingLotPublicInfoUpdated = "parking_lot.public_info_updated";
    public const string ParkingLayoutUpdated = "parking.layout.updated";
}
