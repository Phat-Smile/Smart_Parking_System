namespace ParkingSystem.Parking.Application.ParkingLots.Availability;

// A session-derived, approximate history. This is NOT measured IoT occupancy.
public sealed record ArrivalAvailabilitySnapshot(Guid LotId, string VehicleType,
    DateTimeOffset CapturedAt, int AvailableSlots);

public interface IArrivalSnapshotReader
{
    Task<IReadOnlyList<ArrivalAvailabilitySnapshot>> GetSessionProxyAsync(
        Guid lotId, string vehicleType, DateTimeOffset arrivalAt, CancellationToken cancellationToken);
}
