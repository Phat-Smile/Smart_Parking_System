using ParkingSystem.Parking.Domain.ParkingLots;

namespace ParkingSystem.Parking.Application.ParkingLots.AdminLots;

public interface IAdminParkingLotReader
{
    Task<IReadOnlyList<AdminParkingLotItem>> ListForAdminAsync(ParkingLotApprovalStatus? status, CancellationToken cancellationToken);
}

public sealed record AdminParkingLotItem(
    Guid Id,
    Guid? OwnerUserId,
    string Name,
    string Address,
    decimal Latitude,
    decimal Longitude,
    string Type,
    string ApprovalStatus,
    string? RejectionReason);
