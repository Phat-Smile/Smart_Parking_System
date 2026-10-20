using Microsoft.EntityFrameworkCore;
using Npgsql;
using ParkingSystem.Parking.Application.ParkingLots;
using ParkingSystem.Parking.Application.ParkingLots.RegisterParkingLot;
using ParkingSystem.Parking.Domain.ParkingLots;

namespace ParkingSystem.Parking.Persistence.ParkingLots;

internal sealed class RegisterParkingLotRepository(ParkingDbContext dbContext) : IRegisterParkingLotRepository
{
    public Task<bool> ExistsForOwnerAsync(Guid ownerUserId, string name, CancellationToken cancellationToken)
        => dbContext.ParkingLots.AsNoTracking().AnyAsync(
            lot => lot.OwnerUserId == ownerUserId && lot.Name == name,
            cancellationToken);

    public async Task AddAsync(ParkingLot parkingLot, CancellationToken cancellationToken)
    {
        dbContext.ParkingLots.Add(parkingLot);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: ParkingLotConfiguration.OwnerNameUniqueIndex
        })
        {
            // Two concurrent requests might have both passed ExistsForOwnerAsync.
            throw new DuplicateParkingLotException();
        }
    }
}
