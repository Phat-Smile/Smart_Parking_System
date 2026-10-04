using MediatR;
using ParkingSystem.Parking.Application.ParkingLots;
using ParkingSystem.Parking.Application.ParkingLots.ListParkingLots;

namespace ParkingSystem.Parking.WebAPI.Endpoints;

internal static class ParkingLotEndpoints
{
    public static IEndpointRouteBuilder MapParkingLotEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/parking/lots", async (ISender sender, CancellationToken cancellationToken) =>
            {
                var parkingLots = await sender.Send(new ListParkingLotsQuery(), cancellationToken);
                return Results.Ok(parkingLots);
            })
            .AllowAnonymous()
            .WithName("ListParkingLots")
            .WithSummary("List approved parking lots")
            .WithDescription("Returns only parking lots that have been approved by an administrator. Authentication is not required.")
            .Produces<IReadOnlyList<ParkingLotCatalogItem>>(StatusCodes.Status200OK);

        return app;
    }
}
