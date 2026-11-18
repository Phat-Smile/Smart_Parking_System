using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using ParkingSystem.Parking.Application.ParkingLots.Recommendations;

namespace ParkingSystem.Parking.WebAPI.Endpoints;

public static class ParkingLotRecommendationEndpoints
{
    public static IEndpointRouteBuilder MapParkingLotRecommendationEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/parking/lots/recommendations", async (
            decimal? latitude,
            decimal? longitude,
            string? vehicleType,
            DateTimeOffset? arrivalAt,
            int? durationMinutes,
            double? radiusKm,
            int? topK,
            string? priority,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            if (latitude is null || longitude is null)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["coordinates"] = ["latitude and longitude are required query parameters."]
                });
            }

            var query = new GetParkingLotRecommendationsQuery(
                latitude.Value,
                longitude.Value,
                vehicleType ?? "Car",
                arrivalAt,
                durationMinutes ?? 120,
                radiusKm ?? 10.0,
                topK ?? 3,
                priority ?? "balanced");

            var result = await sender.Send(query, cancellationToken);
            return Results.Ok(result);
        })
        .AllowAnonymous()
        .WithName("GetParkingLotRecommendations")
        .WithSummary("Get Top-3 parking recommendations with configurable user priority")
        .WithDescription("AI Service chooses the order in prompting mode; Parking validates its response. When AI fails, a separately labelled non-AI nearby fallback is used.")
        .Produces<ParkingLotRecommendationResult>(StatusCodes.Status200OK)
        .ProducesValidationProblem();

        return app;
    }
}
