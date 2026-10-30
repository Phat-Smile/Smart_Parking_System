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
        Guid id, Guid lotId, string? observedPlate, decimal confidence, VehicleType observedVehicleType, DateTimeOffset occurredAt)
    {
        if (confidence is < 0m or > 1m)
            throw new ArgumentOutOfRangeException(nameof(confidence), "Confidence must be between 0 and 1.");

        return new Incident
        {
            Id = id,
            LotId = lotId,
            IncidentType = IncidentType.LowConfidencePlate,
            Status = IncidentStatus.Open,
            ObservedPlate = string.IsNullOrWhiteSpace(observedPlate) ? null : ParkingSession.NormalizePlate(observedPlate),
            PlateConfidence = Math.Floor(confidence * 100m) / 100m,
            ObservedVehicleType = observedVehicleType,
            OccurredAt = occurredAt
        };
    }

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
