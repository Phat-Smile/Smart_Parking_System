namespace ParkingSystem.Parking.Application.ParkingLots.Recommendations;

/// <summary>Only verified routing-provider durations may be exposed as drive time.</summary>
public sealed record RecommendationRouteDestination(Guid LotId, decimal Latitude, decimal Longitude);

public interface IRecommendationDrivingEtaReader
{
    Task<IReadOnlyDictionary<Guid, double?>> GetDrivingMinutesAsync(
        decimal originLatitude,
        decimal originLongitude,
        IReadOnlyList<RecommendationRouteDestination> destinations,
        string vehicleGroup,
        CancellationToken cancellationToken);
}
