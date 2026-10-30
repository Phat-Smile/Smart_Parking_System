namespace ParkingSystem.Parking.Domain.GateReadings;

// US-PARK-001 (BR-05.a): what happened to one plate reading at the gate.
public enum GateReadingDecision
{
    // Confidence at least 0.9 and a valid Vietnamese plate: the vehicle is recognised without staff.
    AutoAccepted = 1,

    // Anything else (low confidence, wrong format, no plate read): a LowConfidencePlate incident is opened for staff (US-PARK-002).
    PendingStaff = 2,

    // US-PARK-002: staff looked at the vehicle and confirmed or corrected the plate and vehicle type (incident resolved).
    StaffConfirmed = 3
}

/// <summary>
/// The plate reading the gate device sends: plate, vehicle type and confidence (context 4, MF-08/09A/09B).
/// A reading is not stored on its own: only one that staff must confirm becomes a parking.incidents row (team ERD).
/// </summary>
public static class GateReadingRules
{
    // BR-05.a: "from 0.9" (context), not "above 0.90" as the older SRS wrote.
    public const decimal AutoAcceptConfidence = 0.9m;

    // Uses the confidence as sent, before the incident rounds it to the ERD's 2 decimals.
    public static GateReadingDecision Decide(string? normalizedPlate, decimal confidence) =>
        confidence >= AutoAcceptConfidence && VietnamPlateFormat.IsValid(normalizedPlate)
            ? GateReadingDecision.AutoAccepted
            : GateReadingDecision.PendingStaff;
}
