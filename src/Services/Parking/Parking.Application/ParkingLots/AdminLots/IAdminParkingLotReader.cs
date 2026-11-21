using ParkingSystem.Parking.Application.Common.Paging;
using ParkingSystem.Parking.Domain.ParkingLots;

namespace ParkingSystem.Parking.Application.ParkingLots.AdminLots;

public interface IAdminParkingLotReader
{
    Task<PagedResult<AdminParkingLotItem>> ListForAdminAsync(
        ParkingLotApprovalStatus? status,
        DateTimeOffset? fromDate,
        DateTimeOffset? toDate,
        int page,
        int pageSize,
        CancellationToken cancellationToken);
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
    string? RejectionReason,
    DateTime SubmittedAtUtc);
