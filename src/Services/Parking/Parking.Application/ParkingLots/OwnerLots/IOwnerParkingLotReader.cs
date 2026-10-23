namespace ParkingSystem.Parking.Application.ParkingLots.OwnerLots;

public interface IOwnerParkingLotReader
{
    Task<IReadOnlyList<OwnerParkingLotItem>> ListByOwnerAsync(Guid ownerUserId, CancellationToken cancellationToken);
}

public sealed record OwnerParkingLotItem(
    Guid Id,
    string Name,
    string Address,
    decimal Latitude,
    decimal Longitude,
    string Type,
    string ApprovalStatus,
    string? RejectionReason);
