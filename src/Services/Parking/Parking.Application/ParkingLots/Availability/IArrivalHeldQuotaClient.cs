namespace ParkingSystem.Parking.Application.ParkingLots.Availability;

/// <summary>Reads the number of car reservations held at the requested arrival instant.
/// Null means Booking did not provide a trustworthy answer; zero is a confirmed count.</summary>
public interface IArrivalHeldQuotaClient
{
    Task<int?> GetHeldQuotaAsync(Guid lotId, DateTimeOffset arrivalAt, CancellationToken cancellationToken);
}
