using MediatR;
using ParkingSystem.BuildingBlocks.Identity;
using ParkingSystem.Parking.Application.ParkingLots.OwnerLocation;
using ParkingSystem.Parking.WebAPI.Geocoding;
using ParkingSystem.Parking.WebAPI.OwnerLocation;
using ParkingSystem.Parking.WebAPI.Security;

namespace ParkingSystem.Parking.WebAPI.Endpoints;

internal static class OwnerLotLocationEndpoints
{
    public sealed record ConfirmEntranceRequest(
        string? Address, decimal? Latitude, decimal? Longitude, bool ConfirmedOnMap);

    public static IEndpointRouteBuilder MapOwnerLotLocationEndpoints(this IEndpointRouteBuilder app)
    {
        // Explicit submit, not autocomplete. The public Nominatim adapter has a shared per-process
        // 1 req/sec upstream gate and a cache; do not invoke it for every keypress.
        app.MapGet("/api/parking/owner/locations/resolve", async (
                string? address,
                ICurrentUser currentUser,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                if (!Guid.TryParse(currentUser.SubjectId, out var ownerId) || ownerId == Guid.Empty)
                    return Results.Forbid();

                try
                {
                    var result = await sender.Send(
                        new ResolveOwnerLotAddressQuery(address ?? string.Empty), cancellationToken);
                    return Results.Ok(result);
                }
                catch (GeocodingRateLimitedException)
                {
                    return Results.Problem(
                        title: "Address lookup rate limited.",
                        detail: "Try again later or manually pin the entrance.",
                        statusCode: StatusCodes.Status429TooManyRequests);
                }
                catch (GeocodingProviderUnavailableException)
                {
                    return Results.Problem(
                        title: "Address lookup temporarily unavailable.",
                        detail: "You can still confirm the entrance using a manual map pin.",
                        statusCode: StatusCodes.Status503ServiceUnavailable);
                }
            })
            .RequireAuthorization(ParkingAuthorization.ParkingLotOwnerPolicy)
            .WithName("ResolveOwnerParkingLotAddress")
            .WithSummary("Look up one approximate address for Owner; manual map pin remains supported")
            .WithDescription("On explicit user submission only (no autocomplete). A 200 with zero candidates is not an error; the Owner may select an entrance pin manually.")
            .Produces<OwnerLotAddressLookupResult>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status429TooManyRequests)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

        app.MapPost("/api/parking/owner/locations/confirm", (
                ConfirmEntranceRequest request,
                ICurrentUser currentUser,
                OwnerLotLocationConfirmationService confirmations) =>
            {
                if (!Guid.TryParse(currentUser.SubjectId, out var ownerId) || ownerId == Guid.Empty)
                    return Results.Forbid();

                var errors = OwnerLotLocationRules.ValidateConfirmation(
                    request.Address, request.Latitude, request.Longitude, request.ConfirmedOnMap);
                if (errors.Count > 0)
                    return Results.ValidationProblem(errors);

                var confirmation = confirmations.Issue(
                    ownerId, request.Address!.Trim(), request.Latitude!.Value, request.Longitude!.Value);
                return Results.Ok(confirmation);
            })
            .RequireAuthorization(ParkingAuthorization.ParkingLotOwnerPolicy)
            .WithName("ConfirmOwnerParkingLotEntrance")
            .WithSummary("Issue a 15-minute confirmation token bound to Owner, address, and entrance pin")
            .WithDescription("The frontend must require a deliberate map-pin confirmation. The backend can validate the request, but cannot independently observe a UI gesture.")
            .Produces<OwnerLotLocationConfirmation>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        return app;
    }
}
