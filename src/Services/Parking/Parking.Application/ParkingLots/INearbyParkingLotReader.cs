namespace ParkingSystem.Parking.Application.ParkingLots;

public interface INearbyParkingLotReader
{
    Task<IReadOnlyList<NearbyParkingLotItem>> SearchAsync(
        decimal latitude,
        decimal longitude,
        double radiusKm,
        int limit,
        CancellationToken cancellationToken,
        ParkingSystem.Parking.Domain.ParkingSessions.VehicleType? vehicleType = null);
}

public sealed record NearbyParkingLotItem(
    Guid Id,
    string Name,
    string Address,
    decimal Latitude,
    decimal Longitude,
    string Type,
    double DistanceKm);
