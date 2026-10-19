using ParkingSystem.Parking.Domain.ParkingSessions;

namespace ParkingSystem.Parking.Application.ParkingLots;

public interface IParkingLotCatalogReader
{
    Task<IReadOnlyList<ParkingLotCatalogItem>> ListApprovedAsync(
        CancellationToken cancellationToken,
        VehicleType? vehicleType = null);
}

public sealed record ParkingLotCatalogItem(
    Guid Id,
    string Name,
    string Address,
    decimal Latitude,
    decimal Longitude,
    string Type);
