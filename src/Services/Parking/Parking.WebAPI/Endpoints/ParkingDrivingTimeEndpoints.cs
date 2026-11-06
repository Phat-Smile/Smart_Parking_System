using ParkingSystem.Parking.Application.Common.Mapbox;
using ParkingSystem.Parking.Application.ParkingLots.DrivingTime;

namespace ParkingSystem.Parking.WebAPI.Endpoints;

internal static class ParkingDrivingTimeEndpoints
{
    public static IEndpointRouteBuilder MapParkingDrivingTimeEndpoints(this IEndpointRouteBuilder app)
    {
        // Search only approved lots within the straight-line radius, then rank
        // the up-to-ten candidates by provider-confirmed driving duration.
        app.MapGet("/api/parking/lots/driving-times", async (
                double? latitude,
                double? longitude,
                double? radiusKm,
                int? limit,
                ParkingDrivingTimeEstimator estimator,
                CancellationToken cancellationToken) =>
            {
                var invalidOrigin = ValidateOrigin(latitude, longitude);
                if (invalidOrigin is not null) return invalidOrigin;
                if (radiusKm.HasValue && (!double.IsFinite(radiusKm.Value)
                    || radiusKm.Value <= 0 || radiusKm.Value > 50))
                    return ValidationError("radiusKm", "radiusKm must be > 0 and <= 50.");
                if (limit.HasValue && (limit.Value < 1 || limit.Value > 10))
                    return ValidationError("limit", "limit must be from 1 to 10.");

                try
                {
                    var results = await estimator.GetNearbyAsync(latitude!.Value, longitude!.Value,
                        radiusKm ?? 5, limit ?? 10, cancellationToken);
                    return (IResult)Results.Ok(results);
                }
                catch (MapboxProviderException)
                {
                    return RoutingUnavailable();
                }
            })
            .AllowAnonymous()
            .WithName("GetNearbyParkingDrivingTimes")
            .WithSummary("Driving times to up to ten nearby approved parking lots")
            .WithDescription("Coordinates are latitude/longitude. radiusKm defaults to 5 (max 50), limit defaults to 10 (max 10). "
                + "Candidates are selected within a straight-line radius; reachable results are ranked by driving seconds, "
                + "and unreachable ones have null duration/distance. A missing routing provider returns 503, never a guessed ETA.")
            .Produces<IReadOnlyList<ParkingLotDrivingTime>>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

        app.MapGet("/api/parking/lots/{lotId:guid}/driving-time", async (
                Guid lotId,
                double? latitude,
                double? longitude,
                ParkingDrivingTimeEstimator estimator,
                CancellationToken cancellationToken) =>
            {
                var invalidOrigin = ValidateOrigin(latitude, longitude);
                if (invalidOrigin is not null) return invalidOrigin;
                if (lotId == Guid.Empty)
                    return ValidationError("lotId", "lotId must be a non-empty UUID.");

                try
                {
                    var result = await estimator.GetForLotAsync(
                        lotId, latitude!.Value, longitude!.Value, cancellationToken);
                    return result is null ? (IResult)Results.NotFound() : Results.Ok(result);
                }
                catch (MapboxProviderException)
                {
                    return RoutingUnavailable();
                }
            })
            .AllowAnonymous()
            .WithName("GetParkingLotDrivingTime")
            .WithSummary("Get a driving-time estimate for one approved parking lot")
            .WithDescription("Uses Mapbox Directions. Unknown, Pending or Rejected lots return 404. "
                + "No route returns reachable=false and null duration/distance; provider errors return 503.")
            .Produces<ParkingLotDrivingTime>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .Produces(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

        return app;
    }

    private static IResult? ValidateOrigin(double? latitude, double? longitude)
    {
        if (!latitude.HasValue || !double.IsFinite(latitude.Value) || latitude.Value is < -90 or > 90)
            return ValidationError("latitude", "latitude is required and must be between -90 and 90.");
        if (!longitude.HasValue || !double.IsFinite(longitude.Value) || longitude.Value is < -180 or > 180)
            return ValidationError("longitude", "longitude is required and must be between -180 and 180.");
        return null;
    }

    private static IResult ValidationError(string field, string message) =>
        Results.ValidationProblem(new Dictionary<string, string[]> { [field] = [message] });

    // Avoid exposing provider URLs and the access_token query parameter.
    private static IResult RoutingUnavailable() => Results.Problem(
        title: "Driving-time service temporarily unavailable.",
        statusCode: StatusCodes.Status503ServiceUnavailable);
}
