namespace ParkingSystem.Booking.Domain.Reservations;

/// <summary>
/// Domain policy for US-RES-005 — Prevent Overbooking (BR-02 capacity rules).
/// </summary>
public static class ReservationCapacityPolicy
{
    public const decimal DefaultReservationAllocationRatio = 0.50m;

    /// <summary>
    /// Calculates the reservation ceiling (BR-02.b):
    /// reservation ceiling = floor(valid capacity * reservation allocation ratio)
    /// </summary>
    public static int CalculateReservationCeiling(int validCapacity, decimal allocationRatio = DefaultReservationAllocationRatio)
    {
        if (validCapacity <= 0 || allocationRatio <= 0)
            return 0;

        return (int)Math.Floor(validCapacity * allocationRatio);
    }

    /// <summary>
    /// Evaluates if a new reservation is allowed per BR-02.c:
    /// Condition 1: overlapping active reservations &lt; reservation ceiling.
    /// Condition 2: if hold window has already started (evaluationTime &gt;= requestedWindow.Start),
    /// currently held reservations must be &lt; physical Available slots.
    /// </summary>
    public static CapacityEvaluationResult EvaluateCapacity(
        int validCapacity,
        decimal allocationRatio,
        int overlappingActiveReservationsCount,
        int currentlyHeldReservationsCount,
        int availablePhysicalSlotCount,
        ReservationTimeWindow requestedWindow,
        DateTimeOffset evaluationTime)
    {
        var ceiling = CalculateReservationCeiling(validCapacity, allocationRatio);

        // Condition 1: Must be below reservation ceiling
        if (overlappingActiveReservationsCount >= ceiling)
        {
            return CapacityEvaluationResult.CeilingExceeded(ceiling, overlappingActiveReservationsCount);
        }

        // Condition 2: If the hold window has already started, currently held reservations must be less than Available physical slots
        if (evaluationTime >= requestedWindow.Start)
        {
            if (currentlyHeldReservationsCount >= availablePhysicalSlotCount)
            {
                return CapacityEvaluationResult.PhysicalSlotsUnavailable(availablePhysicalSlotCount, currentlyHeldReservationsCount, ceiling);
            }
        }

        return CapacityEvaluationResult.Allowed(ceiling, overlappingActiveReservationsCount);
    }
}

public enum CapacityRejectionReason
{
    CeilingExceeded,
    PhysicalSlotsUnavailable
}

public readonly record struct CapacityEvaluationResult
{
    public bool IsAllowed { get; }
    public CapacityRejectionReason? Reason { get; }
    public string? RejectionReason { get; }
    public int ReservationCeiling { get; }
    public int OverlappingReservationsCount { get; }

    private CapacityEvaluationResult(bool isAllowed, CapacityRejectionReason? reason, string? rejectionReason, int reservationCeiling, int overlappingCount)
    {
        IsAllowed = isAllowed;
        Reason = reason;
        RejectionReason = rejectionReason;
        ReservationCeiling = reservationCeiling;
        OverlappingReservationsCount = overlappingCount;
    }

    public static CapacityEvaluationResult Allowed(int ceiling, int overlappingCount) =>
        new(true, null, null, ceiling, overlappingCount);

    public static CapacityEvaluationResult CeilingExceeded(int ceiling, int overlappingCount) =>
        new(false, CapacityRejectionReason.CeilingExceeded, $"Reservation ceiling reached ({overlappingCount}/{ceiling}).", ceiling, overlappingCount);

    public static CapacityEvaluationResult PhysicalSlotsUnavailable(int availableSlots, int heldCount, int ceiling) =>
        new(false, CapacityRejectionReason.PhysicalSlotsUnavailable, $"No available physical slots for immediate reservation hold ({heldCount} held >= {availableSlots} available).", ceiling, heldCount);
}
