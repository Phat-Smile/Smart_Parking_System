namespace ParkingSystem.Parking.Application.ParkingLots;

public interface IParkingLotCatalogReader
{
    Task<IReadOnlyList<ParkingLotCatalogItem>> ListApprovedAsync(CancellationToken cancellationToken);
}

public sealed record ParkingLotCatalogItem(
    Guid Id,
    string Name,
    string Address,
    decimal Latitude,
    decimal Longitude,
    string Type);
