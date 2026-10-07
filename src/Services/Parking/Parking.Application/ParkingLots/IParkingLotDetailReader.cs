namespace ParkingSystem.Parking.Application.ParkingLots;

public interface IParkingLotDetailReader
{
    Task<ParkingLotDetail?> GetApprovedByIdAsync(Guid parkingLotId, CancellationToken cancellationToken);
}

public sealed record ParkingLotDetail(
    Guid Id,
    string Name,
    string Address,
    decimal Latitude,
    decimal Longitude,
    string Type);
