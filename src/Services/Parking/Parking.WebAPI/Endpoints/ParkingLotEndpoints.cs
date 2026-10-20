using MediatR;
using ParkingSystem.Parking.Application.ParkingLots;
using ParkingSystem.Parking.Application.ParkingLots.GetParkingLotDetail;
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

        app.MapGet("/api/parking/lots/{parkingLotId}", async (
                Guid parkingLotId,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var parkingLot = await sender.Send(
                    new GetParkingLotDetailQuery(parkingLotId),
                    cancellationToken);

                return parkingLot is null
                    ? Results.NotFound()
                    : Results.Ok(parkingLot);
            })
            .AllowAnonymous()
            .WithName("GetParkingLotDetail")
            .WithSummary("Get an approved parking lot")
            .WithDescription("Returns the public parking-lot master-data detail for an approved parking lot. Pending and rejected parking lots are not exposed.")
            .Produces<ParkingLotDetail>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }
}
