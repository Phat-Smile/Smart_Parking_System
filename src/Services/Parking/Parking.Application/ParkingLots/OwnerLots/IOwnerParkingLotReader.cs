using ParkingSystem.Parking.Application.Common.Paging;
using ParkingSystem.Parking.Domain.ParkingLots;

namespace ParkingSystem.Parking.Application.ParkingLots.OwnerLots;

public interface IOwnerParkingLotReader
{
    Task<PagedResult<OwnerParkingLotItem>> ListByOwnerAsync(
        Guid ownerUserId,
        ParkingLotApprovalStatus? status,
        int page,
        int pageSize,
        CancellationToken cancellationToken);
}

public sealed record OwnerParkingLotItem(
    Guid Id,
    string Name,
    string Address,
    decimal Latitude,
    decimal Longitude,
    string Type,
    string ApprovalStatus,
    string? RejectionReason,
    bool HasOpeningHours,
    bool HasPricing);
