using Microsoft.EntityFrameworkCore;
using ParkingSystem.Parking.Application.ParkingLots;
using ParkingSystem.Parking.Domain.ParkingLots;

namespace ParkingSystem.Parking.Persistence.ParkingLots;

internal sealed class NearbyParkingLotReader(ParkingDbContext dbContext) : INearbyParkingLotReader
{
    private const double EarthRadiusKm = 6371.0088;
    private const double KmPerLatitudeDegree = 110.574;

    public async Task<IReadOnlyList<NearbyParkingLotItem>> SearchAsync(
        decimal latitude,
        decimal longitude,
        double radiusKm,
        int limit,
        CancellationToken cancellationToken,
        ParkingSystem.Parking.Domain.ParkingSessions.VehicleType? vehicleType = null)
    {
        // Narrow down on the DB server first; the final radial check uses Haversine.
        // Longitude is deliberately not pre-filtered to handle the +/-180 meridian and poles.
        var latitudePadding = radiusKm / KmPerLatitudeDegree;
        var minLatitude = (decimal)Math.Max(-90, (double)latitude - latitudePadding);
        var maxLatitude = (decimal)Math.Min(90, (double)latitude + latitudePadding);

        var candidates = await dbContext.ParkingLots
            .AsNoTracking()
            .Where(lot => lot.ApprovalStatus == ParkingLotApprovalStatus.Approved
                && lot.Latitude >= minLatitude
                && lot.Latitude <= maxLatitude)
            .Where(lot => !vehicleType.HasValue || dbContext.Floors.Any(floor =>
                floor.LotId == lot.Id && dbContext.Zones.Any(zone =>
                    zone.FloorId == floor.Id && zone.VehicleGroup == vehicleType.Value)))
            .Select(lot => new
            {
                lot.Id,
                lot.Name,
                lot.Address,
                lot.Latitude,
                lot.Longitude,
                lot.Type
            })
            .ToListAsync(cancellationToken);

        return candidates
            .Select(lot => new
            {
                Lot = lot,
                Distance = DistanceInKm((double)latitude, (double)longitude,
                    (double)lot.Latitude, (double)lot.Longitude)
            })
            .Where(item => item.Distance <= radiusKm)
            .OrderBy(item => item.Distance)
            .ThenBy(item => item.Lot.Name, StringComparer.Ordinal)
            .ThenBy(item => item.Lot.Id)
            .Take(limit)
            .Select(item => new NearbyParkingLotItem(
                item.Lot.Id,
                item.Lot.Name,
                item.Lot.Address,
                item.Lot.Latitude,
                item.Lot.Longitude,
                item.Lot.Type.ToString(),
                Math.Round(item.Distance, 3)))
            .ToList();
    }

    private static double DistanceInKm(double fromLatitude, double fromLongitude,
        double toLatitude, double toLongitude)
    {
        const double radiansPerDegree = Math.PI / 180;
        var deltaLatitude = (toLatitude - fromLatitude) * radiansPerDegree;
        var deltaLongitude = (toLongitude - fromLongitude) * radiansPerDegree;
        var sinLatitude = Math.Sin(deltaLatitude / 2);
        var sinLongitude = Math.Sin(deltaLongitude / 2);
        var a = sinLatitude * sinLatitude
            + Math.Cos(fromLatitude * radiansPerDegree)
            * Math.Cos(toLatitude * radiansPerDegree)
            * sinLongitude * sinLongitude;
        var clampedA = Math.Clamp(a, 0, 1);
        return 2 * EarthRadiusKm * Math.Atan2(Math.Sqrt(clampedA), Math.Sqrt(1 - clampedA));
    }
}
