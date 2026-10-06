using ParkingSystem.Booking.Domain.Enums;

namespace ParkingSystem.Booking.Domain.Reservations;

/// <summary>
/// Domain model for booking.reservation_capacity counter table.
/// Locked (SELECT ... FOR UPDATE) while counting overlapping reservations and inserting a new reservation in the same local transaction (BR-02.c).
/// </summary>
public sealed class ReservationCapacity
{
    private ReservationCapacity()
    {
    }

    public Guid LotId { get; private set; }
    public VehicleGroup VehicleGroup { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public static ReservationCapacity Create(Guid lotId, VehicleGroup vehicleGroup, DateTimeOffset updatedAt)
    {
        if (lotId == Guid.Empty)
            throw new ArgumentException("Lot id cannot be empty.", nameof(lotId));

        return new ReservationCapacity
        {
            LotId = lotId,
            VehicleGroup = vehicleGroup,
            UpdatedAt = updatedAt
        };
    }

    public void Touch(DateTimeOffset updatedAt)
    {
        UpdatedAt = updatedAt;
    }
}
