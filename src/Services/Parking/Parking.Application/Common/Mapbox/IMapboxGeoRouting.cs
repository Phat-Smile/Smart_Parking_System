namespace ParkingSystem.Parking.Application.Common.Mapbox;

/// <summary>Provider-independent contract. Coordinates exposed in latitude/longitude order.</summary>
public interface IMapboxGeoRouting
{
    Task<MapboxGeocodedPlace?> GeocodeAsync(string address, CancellationToken cancellationToken = default);
    Task<MapboxRouteResult> DirectionsAsync(
        double originLatitude, double originLongitude,
        double destinationLatitude, double destinationLongitude,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MapboxRouteResult>> MatrixAsync(
        double originLatitude, double originLongitude,
        IReadOnlyList<(double Latitude, double Longitude)> destinations,
        CancellationToken cancellationToken = default);
}

public sealed record MapboxGeocodedPlace(string Name, double Latitude, double Longitude);
public sealed record MapboxRouteResult(bool Reachable, double? DurationSeconds, double? DistanceMeters);
