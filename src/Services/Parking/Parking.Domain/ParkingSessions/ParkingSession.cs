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

    /// <summary>US-PAY-006: the last moment the vehicle may leave without paying again; null until the first payment.</summary>
    public DateTimeOffset? ExitWindowEndsAt => LastPaidAt + ExitWindow;

    /// <summary>
    /// US-PAY-006 (MF-12 step 83): Payment reported the amount due as paid, so the 15-minute exit window starts at
    /// <paramref name="paidAt"/>. Only an Active session can be paid.
    /// </summary>
    /// <returns>False when the session is not Active.</returns>
    public bool MarkPaid(DateTimeOffset paidAt)
    {
        if (Status != ParkingSessionStatus.Active)
            return false;

        Status = ParkingSessionStatus.Paid;
        LastPaidAt = paidAt;
        return true;
    }

    /// <summary>
    /// BR-09.d, lead decision 2026-11-04: exactly T + 15:00 is still in time, T + 15:01 is late. Compared in whole seconds.
    /// </summary>
    public bool IsExitWindowOver(DateTimeOffset arrivedAt) =>
        LastPaidAt is { } paidAt && arrivedAt - paidAt >= ExitWindow + TimeSpan.FromSeconds(1);

    /// <summary>
    /// BR-09.d: the vehicle reached the gate after the exit window, so the session goes back to Active until the extra
    /// fee is paid. <see cref="LastPaidAt"/> stays: the extra fee is counted from it.
    /// </summary>
    /// <returns>False when the session is not Paid.</returns>
    public bool ReopenAfterExitWindow()
    {
        if (Status != ParkingSessionStatus.Paid)
            return false;

        Status = ParkingSessionStatus.Active;
        return true;
    }

    /// <summary>
    /// BR-11.e: the vehicle left. The QR is invalid for good because only open sessions match a QR. Only a Paid session
    /// can complete: the system has no debt (BR-11.c).
    /// </summary>
    /// <returns>False when the session is not Paid.</returns>
    public bool Complete(DateTimeOffset exitTime)
    {
        if (Status != ParkingSessionStatus.Paid)
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
