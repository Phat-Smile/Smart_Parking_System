using MediatR;
using ParkingSystem.Parking.Application.ParkingLots.OpeningHours;

namespace ParkingSystem.Parking.Application.ParkingLots.AdminLots;

// Unlike public lot detail, an administrator may review lots in any approval state.
public sealed record GetAdminParkingLotDetailQuery(Guid ParkingLotId) : IRequest<AdminParkingLotDetail?>;

public sealed record AdminParkingLotDetail(
    Guid Id,
    Guid? OwnerUserId,
    string Name,
    string Address,
    decimal Latitude,
    decimal Longitude,
    string Type,
    string ApprovalStatus,
    string? RejectionReason,
    WeeklyOpeningHours? OpeningHours);

public interface IAdminParkingLotDetailReader
{
    Task<AdminParkingLotDetail?> GetByIdAsync(Guid parkingLotId, CancellationToken cancellationToken);
}

public sealed class GetAdminParkingLotDetailQueryHandler(IAdminParkingLotDetailReader reader)
    : IRequestHandler<GetAdminParkingLotDetailQuery, AdminParkingLotDetail?>
{
    public Task<AdminParkingLotDetail?> Handle(
        GetAdminParkingLotDetailQuery request, CancellationToken cancellationToken) =>
        reader.GetByIdAsync(request.ParkingLotId, cancellationToken);
}
