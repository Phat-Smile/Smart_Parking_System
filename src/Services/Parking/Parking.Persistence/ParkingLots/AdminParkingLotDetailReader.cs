using Microsoft.EntityFrameworkCore;
using ParkingSystem.Parking.Application.ParkingLots.AdminLots;
using ParkingSystem.Parking.Application.ParkingLots.OpeningHours;

namespace ParkingSystem.Parking.Persistence.ParkingLots;

internal sealed class AdminParkingLotDetailReader(ParkingDbContext dbContext) : IAdminParkingLotDetailReader
{
    public async Task<AdminParkingLotDetail?> GetByIdAsync(Guid parkingLotId, CancellationToken cancellationToken)
    {
        var lot = await dbContext.ParkingLots.AsNoTracking()
            .Where(lot => lot.Id == parkingLotId)
            .Select(lot => new
            {
                lot.Id, lot.OwnerUserId, lot.Name, lot.Address, lot.Latitude, lot.Longitude,
                lot.Type, lot.ApprovalStatus, lot.RejectionReason, lot.OpeningHoursJson
            })
            .SingleOrDefaultAsync(cancellationToken);

        return lot is null ? null : new AdminParkingLotDetail(
            lot.Id, lot.OwnerUserId, lot.Name, lot.Address, lot.Latitude, lot.Longitude,
            lot.Type.ToString(), lot.ApprovalStatus.ToString(), lot.RejectionReason,
            OpeningHoursSchedule.Deserialize(lot.OpeningHoursJson));
    }
}
