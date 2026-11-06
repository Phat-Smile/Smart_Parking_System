using ParkingSystem.Parking.Application.Common.Mapbox;

namespace ParkingSystem.Parking.Application.ParkingLots.DrivingTime;

/// <summary>
/// A provider-confirmed driving estimate, not a straight-line ETA. Unreachable
/// routes have null duration/distance rather than a fabricated fallback.
/// </summary>
public sealed record ParkingLotDrivingTime(
    Guid LotId,
    string Name,
    string Address,
    decimal Latitude,
    decimal Longitude,
    bool Reachable,
    double? DurationSeconds,
    double? DistanceMeters,
    double? StraightLineDistanceKm);

public sealed class ParkingDrivingTimeEstimator(
    IParkingLotDetailReader lots,
    INearbyParkingLotReader nearbyLots,
    IMapboxGeoRouting routing)
{
    public async Task<ParkingLotDrivingTime?> GetForLotAsync(
        Guid lotId, double originLatitude, double originLongitude,
        CancellationToken cancellationToken = default)
    {
        ValidateOrigin(originLatitude, originLongitude);
        if (lotId == Guid.Empty) throw new ArgumentException("Lot ID must not be empty.", nameof(lotId));
        // The reader only returns Approved lots. Never call the routing provider
        // for Pending, Rejected or unknown lots.
        var lot = await lots.GetApprovedByIdAsync(lotId, cancellationToken);
        if (lot is null) return null;
        var route = await routing.DirectionsAsync(
            originLatitude, originLongitude, (double)lot.Latitude, (double)lot.Longitude,
            cancellationToken);
        return new ParkingLotDrivingTime(
            lot.Id, lot.Name, lot.Address, lot.Latitude, lot.Longitude,
            route.Reachable, route.Reachable ? route.DurationSeconds : null,
            route.Reachable ? route.DistanceMeters : null, null);
    }

    public async Task<IReadOnlyList<ParkingLotDrivingTime>> GetNearbyAsync(
        double originLatitude, double originLongitude,
        double radiusKm = 5, int limit = 10,
        CancellationToken cancellationToken = default)
    {
        ValidateOrigin(originLatitude, originLongitude);
        if (!double.IsFinite(radiusKm) || radiusKm <= 0 || radiusKm > 50)
            throw new ArgumentOutOfRangeException(nameof(radiusKm));
        // Mapbox Matrix supports at most ten destinations in our client.
        if (limit is < 1 or > 10)
            throw new ArgumentOutOfRangeException(nameof(limit));

        var candidates = await nearbyLots.SearchAsync(
            (decimal)originLatitude, (decimal)originLongitude, radiusKm, limit,
            cancellationToken);
        if (candidates.Count == 0) return [];
        if (candidates.Count > 10) throw new InvalidOperationException("Nearby reader exceeded the routing limit.");

        var routes = await routing.MatrixAsync(
            originLatitude, originLongitude,
            candidates.Select(x => ((double)x.Latitude, (double)x.Longitude)).ToArray(),
            cancellationToken);
        if (routes.Count != candidates.Count)
            throw new MapboxProviderException("invalid_payload");

        // Keep the provider's destination mapping by index before sorting by ETA.
        return candidates.Select((lot, i) => new ParkingLotDrivingTime(
                lot.Id, lot.Name, lot.Address, lot.Latitude, lot.Longitude,
                routes[i].Reachable,
                routes[i].Reachable ? routes[i].DurationSeconds : null,
                routes[i].Reachable ? routes[i].DistanceMeters : null,
                lot.DistanceKm))
            .OrderBy(x => x.Reachable && x.DurationSeconds.HasValue ? 0 : 1)
            .ThenBy(x => x.DurationSeconds ?? double.PositiveInfinity)
            .ThenBy(x => x.StraightLineDistanceKm)
            .ThenBy(x => x.LotId)
            .ToArray();
    }

    private static void ValidateOrigin(double latitude, double longitude)
    {
        if (!double.IsFinite(latitude) || !double.IsFinite(longitude)
            || latitude is < -90 or > 90 || longitude is < -180 or > 180)
            throw new ArgumentOutOfRangeException(nameof(latitude), "Invalid origin coordinates.");
    }
}
