namespace ParkingSystem.Booking.Domain.Reservations;

/// <summary>
/// Aggregate/entity representing the capacity concurrency coordinator row in booking schema.
/// A row (lot_id, vehicle_group) is locked via SELECT ... FOR UPDATE during atomic reservation hold checks.
/// Note: There is intentionally no 'remaining' counter column in this table per DBML design;
/// remaining capacity is computed dynamically from active overlapping reservations inside the transaction.
/// </summary>
public sealed class ReservationCapacity
{
    private ReservationCapacity()
    {
    }

    public ReservationCapacity(Guid lotId, VehicleGroup vehicleGroup, DateTimeOffset updatedAt)
    {
        if (lotId == Guid.Empty)
            throw new ArgumentException("Lot ID must not be empty.", nameof(lotId));
        if (!Enum.IsDefined(vehicleGroup))
            throw new ArgumentOutOfRangeException(nameof(vehicleGroup), $"Invalid vehicle group: {vehicleGroup}");

        LotId = lotId;
        VehicleGroup = vehicleGroup;
        UpdatedAt = updatedAt.ToUniversalTime();
    }

    public Guid LotId { get; private set; }
    public VehicleGroup VehicleGroup { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public void Touch(DateTimeOffset updatedAt)
    {
        UpdatedAt = updatedAt.ToUniversalTime();
    }
}

