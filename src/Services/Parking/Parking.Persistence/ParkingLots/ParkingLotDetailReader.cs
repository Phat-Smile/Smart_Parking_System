using Microsoft.EntityFrameworkCore;
using ParkingSystem.Parking.Application.ParkingLots;
using ParkingSystem.Parking.Application.ParkingLots.OpeningHours;
using ParkingSystem.Parking.Domain.ParkingLots;

namespace ParkingSystem.Parking.Persistence.ParkingLots;

internal sealed class ParkingLotDetailReader(ParkingDbContext dbContext) : IParkingLotDetailReader
{
    public async Task<ParkingLotDetail?> GetApprovedByIdAsync(Guid parkingLotId, CancellationToken cancellationToken)
    {
        var lot = await dbContext.ParkingLots.AsNoTracking()
            .Where(lot => lot.Id == parkingLotId && lot.ApprovalStatus == ParkingLotApprovalStatus.Approved)
            .Select(lot => new
            {
                lot.Id, lot.Name, lot.Address, lot.Latitude, lot.Longitude, lot.Type, lot.OpeningHoursJson
            })
            .SingleOrDefaultAsync(cancellationToken);

        return lot is null ? null : new ParkingLotDetail(
            lot.Id, lot.Name, lot.Address, lot.Latitude, lot.Longitude, lot.Type.ToString(),
            OpeningHoursSchedule.Deserialize(lot.OpeningHoursJson));
    }
}
