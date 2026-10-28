namespace ParkingSystem.Parking.Application.ParkingLots;

// Port: the Application layer is not tied to an external geocoding provider.
public interface IAddressGeocoder
{
    Task<GeocodedDestination?> GeocodeAsync(string address, CancellationToken cancellationToken);
}

public sealed record GeocodedDestination(string DisplayName, decimal Latitude, decimal Longitude);
