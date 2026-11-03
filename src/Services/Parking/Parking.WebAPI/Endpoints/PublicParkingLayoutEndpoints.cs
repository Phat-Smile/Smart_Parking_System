using MediatR;
using ParkingSystem.AspNetCore;
using ParkingSystem.Parking.Application.ParkingLayout.ManageLayout;

namespace ParkingSystem.Parking.WebAPI.Endpoints;

internal static class PublicParkingLayoutEndpoints
{
    public static IEndpointRouteBuilder MapPublicParkingLayoutEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/parking/lots/{lotId:guid}/layout", async (
                Guid lotId, HttpContext context, ISender sender, CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(new GetPublicParkingLayoutQuery(lotId), cancellationToken);
                // Do not cache operational status. Clients may poll until a realtime stream is available.
                context.Response.Headers["Cache-Control"] = "no-store";
                return result.IsSuccess
                    ? Results.Ok(result.Value)
                    : BusinessProblem.Create(context, StatusCodes.Status404NotFound, result.Error!);
            })
            .AllowAnonymous()
            .WithName("GetPublicParkingLayout")
            .WithSummary("Read the current approved-lot layout for 2D and 3D views")
            .Produces<PublicParkingLayout>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);
        return app;
    }
}
