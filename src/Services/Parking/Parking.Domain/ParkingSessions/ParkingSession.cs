namespace ParkingSystem.Parking.Domain.ParkingSessions;

public sealed class ParkingSession
{
    // Placeholder: the plate format is not specified in Context yet.
    public const int PlateNumberMaxLength = 20;

    // BR-11.b: 32 random bytes encode to 43 Base64Url characters.
    public const int QrTokenByteLength = 32;
    public const int QrTokenMaxLength = 64;

    public const int CancelReasonMaxLength = 500;

    // ERD audit_logs.reason is varchar(500); the reason of a QR reissue or a manual exit check is stored only there.
    public const int StaffActionReasonMaxLength = 500;

    // BR-09.d: after paying, the vehicle has 15 minutes to reach the exit gate.
    public static readonly TimeSpan ExitWindow = TimeSpan.FromMinutes(15);

    // Q-EXIT-WINDOW: Maximum time allowed between arriving at the gate and completing the barrier exit (15 minutes).
    // Lingering at the exit or turning around back into the lot voids the recorded arrival.
    public static readonly TimeSpan MaxGateDepartureWindow = TimeSpan.FromMinutes(15);

    // Statuses that still hold the plate in the lot (BR-11.a).
    public static readonly IReadOnlyList<ParkingSessionStatus> OpenStatuses =
        [ParkingSessionStatus.Active, ParkingSessionStatus.Paid];

    private ParkingSession()
    {
    }

    public Guid Id { get; private set; }
    public Guid LotId { get; private set; }
    public string PlateNumber { get; private set; } = null!;
    public VehicleType VehicleType { get; private set; }
    public ParkingSessionStatus Status { get; private set; }
    // Team decision (lead review, 2026-10-06): the time the system sends the barrier-open command.
    public DateTimeOffset EntryTime { get; private set; }
    public string QrToken { get; private set; } = null!;

    // BR-09.d: T, the last payment; the exit window starts here.
    public DateTimeOffset? LastPaidAt { get; private set; }

    // BR-11.e: set when the session is Completed.
    public DateTimeOffset? ExitTime { get; private set; }

    // Q-EXIT-WINDOW (BR-09.d): recorded when the vehicle first reaches the exit gate (camera reading or QR scan).
    public DateTimeOffset? ExitArrivedAt { get; private set; }

    // Q-EXIT-WINDOW: one-time 15-minute grace period extension granted by staff when the lot has an incident or exit delay.
    public bool HasExitWindowExtension { get; private set; }
    public DateTimeOffset? ExitWindowExtendedAt { get; private set; }
    public string? ExitWindowExtensionReason { get; private set; }

    public DateTimeOffset? CancelledAt { get; private set; }
    public Guid? CancelledBy { get; private set; }
    public string? CancelReason { get; private set; }

    // Ids owned by other services, stored without foreign keys. Null when the plate has no account (BR-05.e).
    public Guid? UserId { get; private set; }
    public Guid? VehicleId { get; private set; }
    public Guid? ReservationId { get; private set; }

    // US-PARK-008: the motorbike zone whose space this session took, so the exit flow can give it back.
    // Null for cars until slot designation (US-PARK-004/007) records their zone.
    public Guid? ZoneId { get; private set; }

    public CustomerType CustomerType =>
        ReservationId is not null ? CustomerType.Booking
        : UserId is not null ? CustomerType.RegisteredWalkIn
        : CustomerType.UnregisteredWalkIn;

    public static ParkingSession Open(
        Guid id,
        Guid lotId,
        string plateNumber,
        VehicleType vehicleType,
        DateTimeOffset entryTime,
        string qrToken,
        Guid? userId,
        Guid? vehicleId,
        Guid? zoneId = null,
        Guid? reservationId = null)
    {
        return new ParkingSession
        {
            Id = id,
            LotId = lotId,
            PlateNumber = NormalizePlate(plateNumber),
            VehicleType = vehicleType,
            Status = ParkingSessionStatus.Active, // BR-11.a: a new session is always Active.
            EntryTime = entryTime,
            QrToken = qrToken,
            UserId = userId,
            VehicleId = vehicleId,
            ZoneId = zoneId,
            ReservationId = reservationId
        };
    }

    /// <summary>
    /// Staff undo a session whose vehicle did not pass the barrier. Only an Active session can be cancelled;
    /// a cancelled session no longer blocks the plate and its QR is no longer valid.
    /// </summary>
    /// <returns>False when the session is not Active.</returns>
    public bool Cancel(Guid cancelledBy, string reason, DateTimeOffset cancelledAt)
    {
        if (Status != ParkingSessionStatus.Active)
            return false;

        Status = ParkingSessionStatus.Cancelled;
        CancelledBy = cancelledBy;
        CancelReason = reason.Trim();
        CancelledAt = cancelledAt;
        return true;
    }

    /// <summary>
    /// US-STAFF-004 (BR-11.d): staff give a new QR when the customer lost it. The old token is overwritten, so it stops
    /// matching the session before the new one is handed out. Only an Active session gets a new QR (decision 2026-10-24):
    /// a Paid session is already at the exit gate, and a closed session's QR is invalid for good (BR-11.e).
    /// </summary>
    /// <returns>False when the session is not Active.</returns>
    public bool ReissueQr(string newQrToken)
    {
        if (Status != ParkingSessionStatus.Active)
            return false;

        QrToken = newQrToken;
        return true;
    }

    /// <summary>US-PAY-006 & Q-EXIT-WINDOW: the last moment the vehicle may leave without paying again; null until the first payment.</summary>
    public DateTimeOffset? ExitWindowEndsAt =>
        LastPaidAt is { } paidAt
            ? paidAt + ExitWindow + (HasExitWindowExtension ? ExitWindow : TimeSpan.Zero)
            : null;

    /// <summary>
    /// Q-EXIT-WINDOW: records the moment the vehicle reaches the exit gate (camera reading or QR scan).
    /// Only allowed if the session is Paid and the exit window has not expired.
    /// Returns false if already recorded or not eligible.
    /// </summary>
    public bool RecordExitArrival(DateTimeOffset arrivedAt)
    {
        if (Status != ParkingSessionStatus.Paid)
            return false;

        if (ExitArrivedAt is not null)
            return false;

        if (IsExitWindowOver(arrivedAt))
            return false;

        ExitArrivedAt = arrivedAt;
        return true;
    }

    /// <summary>
    /// Q-EXIT-WINDOW: one-time 15-minute grace period extension granted by staff for a Paid session with mandatory reason.
    /// </summary>
    public bool ExtendExitWindow(DateTimeOffset extendedAt, string reason)
    {
        if (Status != ParkingSessionStatus.Paid || HasExitWindowExtension)
            return false;

        if (string.IsNullOrWhiteSpace(reason) || reason.Length > StaffActionReasonMaxLength)
            return false;

        HasExitWindowExtension = true;
        ExitWindowExtendedAt = extendedAt;
        ExitWindowExtensionReason = reason.Trim();
        return true;
    }

    /// <summary>
    /// US-PAY-006 (MF-12 step 83): Payment reported the amount due as paid, so the 15-minute exit window starts at
    /// <paramref name="paidAt"/>. Only an Active session can be paid.
    /// Resets any previously recorded exit arrival and extension to start a fresh cycle.
    /// </summary>
    /// <returns>False when the session is not Active.</returns>
    public bool MarkPaid(DateTimeOffset paidAt)
    {
        if (Status != ParkingSessionStatus.Active)
            return false;

        Status = ParkingSessionStatus.Paid;
        LastPaidAt = paidAt;
        ExitArrivedAt = null;
        HasExitWindowExtension = false;
        ExitWindowExtendedAt = null;
        ExitWindowExtensionReason = null;
        return true;
    }

    /// <summary>
    /// BR-09.d & Q-EXIT-WINDOW: arrival within T + 15:00 (or extended window) is in time.
    /// Uses ExitArrivedAt if recorded; otherwise the provided arrival timestamp.
    /// If ExitArrivedAt is recorded, elapsed time between arrivedAt and ExitArrivedAt cannot exceed 15 minutes
    /// (lingering at the gate or returning to the lot invalidates the arrival and is treated as overstay).
    /// </summary>
    public bool IsExitWindowOver(DateTimeOffset arrivedAt)
    {
        if (LastPaidAt is not { } paidAt)
            return false;

        var allowedWindow = ExitWindow + (HasExitWindowExtension ? ExitWindow : TimeSpan.Zero);

        if (ExitArrivedAt is { } exitArrival)
        {
            // Defense-in-depth: If exitArrival predates the last payment, it belongs to an old payment cycle
            if (exitArrival < paidAt)
                return true;

            // If the vehicle lingered at the exit or returned to the lot (> 15 minutes after arrival),
            // the recorded arrival is stale/expired and the vehicle must pay overstay.
            if (arrivedAt - exitArrival > MaxGateDepartureWindow)
                return true;

            return exitArrival - paidAt >= allowedWindow + TimeSpan.FromSeconds(1);
        }

        return arrivedAt - paidAt >= allowedWindow + TimeSpan.FromSeconds(1);
    }

    /// <summary>
    /// BR-09.d: the vehicle reached the gate after the exit window, so the session goes back to Active until the extra
    /// fee is paid. <see cref="LastPaidAt"/> stays: the extra fee is counted from it.
    /// Resets exit arrival and extension so old timestamps cannot be exploited.
    /// </summary>
    /// <returns>False when the session is not Paid.</returns>
    public bool ReopenAfterExitWindow()
    {
        if (Status != ParkingSessionStatus.Paid)
            return false;

        Status = ParkingSessionStatus.Active;
        ExitArrivedAt = null;
        HasExitWindowExtension = false;
        ExitWindowExtendedAt = null;
        ExitWindowExtensionReason = null;
        return true;
    }

    /// <summary>
    /// BR-11.e: the vehicle left. The QR is invalid for good because only open sessions match a QR. Only a Paid session
    /// can complete: the system has no debt (BR-11.c).
    /// If ExitArrivedAt is recorded, verifies departure timed within 15 minutes of arrival at gate.
    /// </summary>
    /// <returns>False when the session is not Paid or departure timed out.</returns>
    public bool Complete(DateTimeOffset exitTime)
    {
        if (Status != ParkingSessionStatus.Paid)
            return false;

        if (ExitArrivedAt is { } exitArrival && exitTime - exitArrival > MaxGateDepartureWindow)
            return false;

        Status = ParkingSessionStatus.Completed;
        ExitTime = exitTime;
        return true;
    }

    public void LinkReservation(Guid reservationId)
    {
        ReservationId = reservationId;
    }
    public static string NormalizePlate(string plateNumber) => plateNumber.Trim().ToUpperInvariant();
}
