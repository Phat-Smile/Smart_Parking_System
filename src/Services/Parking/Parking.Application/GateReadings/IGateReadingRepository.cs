using ParkingSystem.Parking.Domain.GateReadings;

namespace ParkingSystem.Parking.Application.GateReadings;

public interface IGateReadingRepository
{
    void Add(GateReading reading);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
