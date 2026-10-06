using ParkingSystem.Booking.Domain.Enums;

namespace ParkingSystem.Booking.Application.Reservations;

/// <summary>
/// Cross-service abstraction port to retrieve capacity parameters owned by the Parking Service (BR-02.a, BR-02.b).
/// Decouples Booking from Parking's database schema.
/// </summary>
public interface IParkingLotCapacityProvider
{
    Task<ParkingLotCapacitySnapshot?> GetCapacitySnapshotAsync(
        Guid lotId,
        VehicleGroup vehicleGroup,
        DateTimeOffset evaluationTime,
        CancellationToken cancellationToken);
}

public sealed record ParkingLotCapacitySnapshot(
    int ValidCapacity,
    int AvailableSlotCount,
    decimal ReservationAllocationRatio = 0.50m,
    bool BookingEnabled = true);
