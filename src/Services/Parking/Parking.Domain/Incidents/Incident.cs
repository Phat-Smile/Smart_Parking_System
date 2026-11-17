using ParkingSystem.Parking.Domain.ParkingSessions;

namespace ParkingSystem.Parking.Domain.Incidents;

// ERD parking.incident_type: the cases the automatic flow cannot decide and staff must handle (MF-14 step 93).
public enum IncidentType
{
    LowConfidencePlate = 1,     // confidence < 0.9 or unreadable (BR-05.a)
    PlateOneCharMismatch = 2,   // BR-05.b
    VehicleTypeMismatch = 3,    // BR-05.c
    GateDeviceError = 4,
    LostOrInvalidQr = 5,        // BR-11.d
    ExitPlateMismatch = 6,      // BR-11.d
    ParkConfirmTimeout = 7,     // BR-04.e
    SensorConflict = 8,         // BR-06.b
    DeviceDisconnected = 9,
    NoSuitableSlot = 10         // BR-02.f, BR-04.h
}

// ERD parking.incident_status.
public enum IncidentStatus
{
    Open = 1,
    Resolved = 2
}

/// <summary>
/// ERD parking.incidents: one case waiting for staff (MF-14). Shared by every story that needs a manual decision;
/// US-PARK-001/002 use it for a plate the gate could not accept automatically.
/// </summary>
public sealed class Incident
{
    // ERD: plate_confidence numeric(3,2), resolution_reason varchar(500), device_id varchar(100).
    public const int ConfidenceDecimals = 2;
    public const int ResolutionReasonMaxLength = 500;
    public const int DeviceIdMaxLength = 100;

    private Incident()
    {
    }

    public Guid Id { get; private set; }
    public Guid LotId { get; private set; }
    public IncidentType IncidentType { get; private set; }
    public IncidentStatus Status { get; private set; }
    public Guid? SessionId { get; private set; }
    public Guid? ReservationId { get; private set; }
    public Guid? SlotId { get; private set; }
    public string? DeviceId { get; private set; }
    public string? ObservedPlate { get; private set; }
    public decimal? PlateConfidence { get; private set; }
    public VehicleType? ObservedVehicleType { get; private set; }

    // ERD: arrival time at the gate, so Booking does not count a vehicle waiting for staff as a no-show (BR-03.e).
    public DateTimeOffset OccurredAt { get; private set; }
    public Guid? ResolvedBy { get; private set; }
    public DateTimeOffset? ResolvedAt { get; private set; }
    public string? ResolutionReason { get; private set; }

    /// <summary>
    /// US-PARK-001: the gate read a plate it could not accept automatically (BR-05.a), so staff must confirm it.
    /// Confidence is stored with 2 decimals as in the ERD, rounded down so a value below 0.9 never becomes 0.90.
    /// </summary>
    public static Incident OpenLowConfidencePlate(
        Guid id, Guid lotId, string? observedPlate, decimal confidence, VehicleType observedVehicleType, DateTimeOffset occurredAt, string? deviceId = null)
    {
        if (confidence is < 0m or > 1m)
            throw new ArgumentOutOfRangeException(nameof(confidence), "Confidence must be between 0 and 1.");

        return new Incident
        {
            Id = id,
            LotId = lotId,
            IncidentType = IncidentType.LowConfidencePlate,
            Status = IncidentStatus.Open,
            DeviceId = deviceId,
            ObservedPlate = string.IsNullOrWhiteSpace(observedPlate) ? null : ParkingSession.NormalizePlate(observedPlate),
            PlateConfidence = Math.Floor(confidence * 100m) / 100m,
            ObservedVehicleType = observedVehicleType,
            OccurredAt = occurredAt
        };
    }

    /// <summary>
    /// US-PARK-005 (BR-04.e): no parking confirmation in time, so staff must check the slot. OccurredAt is when the car
    /// was given the slot at the gate.
    /// </summary>
    public static Incident OpenParkConfirmTimeout(Guid id, Guid lotId, Guid? sessionId, Guid slotId, DateTimeOffset occurredAt) =>
        new()
        {
            Id = id,
            LotId = lotId,
            IncidentType = IncidentType.ParkConfirmTimeout,
            Status = IncidentStatus.Open,
            SessionId = sessionId,
            SlotId = slotId,
            OccurredAt = occurredAt
        };

    /// <summary>
    /// US-PARK-004 (BR-02.f): a car with a reservation arrived and no suitable slot was left. Staff place it in another
    /// zone of the same vehicle group or, if they cannot, the prepaid amount is refunded. OccurredAt is the arrival time,
    /// so Booking does not count it as a no-show (BR-03.e).
    /// </summary>
    public static Incident OpenNoSuitableSlot(Guid id, Guid lotId, Guid reservationId, string observedPlate, DateTimeOffset occurredAt) =>
        new()
        {
            Id = id,
            LotId = lotId,
            IncidentType = IncidentType.NoSuitableSlot,
            Status = IncidentStatus.Open,
            ReservationId = reservationId,
            ObservedPlate = observedPlate,
            OccurredAt = occurredAt
        };

    // Not in the team ERD yet (lead review of #151, 2026-10-31): the plate and vehicle type staff confirmed for a
    // LowConfidencePlate incident, kept in columns so the next steps (open the session, reconciliation) can read them.
    public string? ConfirmedPlate { get; private set; }
    public VehicleType? ConfirmedVehicleType { get; private set; }

    /// <summary>
    /// US-PARK-002: staff confirm the plate and vehicle type they see on a LowConfidencePlate incident. The observed values
    /// stay as read. Only an Open incident can be confirmed.
    /// </summary>
    /// <returns>False when the incident is already resolved.</returns>
    public bool ConfirmPlate(Guid resolvedBy, DateTimeOffset resolvedAt, string plateNumber, VehicleType vehicleType)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(plateNumber);
        if (!Resolve(resolvedBy, resolvedAt, PlateConfirmedReason))
            return false;

        ConfirmedPlate = ParkingSession.NormalizePlate(plateNumber);
        ConfirmedVehicleType = vehicleType;
        return true;
    }

    // Staff do not type a reason when confirming a plate yet; the case itself says why it went to staff.
    public const string PlateConfirmedReason = "Plate and vehicle type confirmed by staff at the gate.";

    /// <summary>Staff handled the case. Only an Open incident can be resolved.</summary>
    /// <returns>False when the incident is already resolved.</returns>
    public bool Resolve(Guid resolvedBy, DateTimeOffset resolvedAt, string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        if (Status != IncidentStatus.Open)
            return false;

        Status = IncidentStatus.Resolved;
        ResolvedBy = resolvedBy;
        ResolvedAt = resolvedAt;
        ResolutionReason = reason.Length > ResolutionReasonMaxLength ? reason[..ResolutionReasonMaxLength] : reason;
        return true;
    }
}
