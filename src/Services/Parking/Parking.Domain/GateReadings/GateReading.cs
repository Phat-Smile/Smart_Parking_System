using ParkingSystem.Parking.Domain.ParkingSessions;

namespace ParkingSystem.Parking.Domain.GateReadings;

// US-PARK-001 (BR-05.a): what happened to one plate reading at the gate.
public enum GateReadingDecision
{
    // Confidence at least 0.9 and a valid Vietnamese plate: the vehicle is recognised without staff.
    AutoAccepted = 1,

    // Anything else (low confidence, wrong format, no plate read): staff must confirm the plate (US-PARK-002).
    PendingStaff = 2
}

/// <summary>
/// One plate reading sent by the gate device: plate, vehicle type and confidence (context 4, MF-08/09A/09B).
/// In Sprint 2 the reading is typed in to stand in for the device; from Sprint 3 the same reading arrives over MQTT.
/// A reading only records the recognition decision: opening the session stays a separate step (US-PARK-003), because
/// "which event means the vehicle has entered" is still MISSING SPECIFICATION (context 14).
/// </summary>
public sealed class GateReading
{
    // BR-05.a: "from 0.9" (context), not "above 0.90" as the older SRS wrote.
    public const decimal AutoAcceptConfidence = 0.9m;
    public const int RawPlateMaxLength = 32;

    private GateReading()
    {
    }

    public Guid Id { get; private set; }
    public Guid LotId { get; private set; }

    // As the device read it; empty when no plate could be read (MF-09B step 63). Kept for staff to compare.
    public string RawPlate { get; private set; } = null!;

    // Same normalization as parking sessions, so staff open the session with exactly this plate.
    public string PlateNumber { get; private set; } = null!;
    public VehicleType VehicleType { get; private set; }

    // One value from 0 to 1 for the whole plate (BR-05.a).
    public decimal Confidence { get; private set; }
    public GateReadingDecision Decision { get; private set; }
    public DateTimeOffset ReceivedAt { get; private set; }

    // Who sent the reading: the staff account typing it in Sprint 2.
    public Guid RecordedBy { get; private set; }

    public static GateReading Record(
        Guid id,
        Guid lotId,
        string? rawPlate,
        VehicleType vehicleType,
        decimal confidence,
        DateTimeOffset receivedAt,
        Guid recordedBy)
    {
        if (confidence is < 0m or > 1m)
            throw new ArgumentOutOfRangeException(nameof(confidence), "Confidence must be between 0 and 1.");

        var raw = rawPlate?.Trim() ?? string.Empty;
        var plate = raw.Length == 0 ? string.Empty : ParkingSession.NormalizePlate(raw);

        return new GateReading
        {
            Id = id,
            LotId = lotId,
            RawPlate = raw,
            PlateNumber = plate,
            VehicleType = vehicleType,
            Confidence = confidence,
            Decision = confidence >= AutoAcceptConfidence && VietnamPlateFormat.IsValid(plate)
                ? GateReadingDecision.AutoAccepted
                : GateReadingDecision.PendingStaff,
            ReceivedAt = receivedAt,
            RecordedBy = recordedBy
        };
    }
}
