using ParkingSystem.Parking.Application.GateReadings;
using ParkingSystem.Parking.Domain.GateReadings;

namespace ParkingSystem.Parking.Persistence.GateReadings;

internal sealed class GateReadingRepository(ParkingDbContext dbContext) : IGateReadingRepository
{
    public void Add(GateReading reading) => dbContext.GateReadings.Add(reading);

    public Task SaveChangesAsync(CancellationToken cancellationToken) => dbContext.SaveChangesAsync(cancellationToken);
}
