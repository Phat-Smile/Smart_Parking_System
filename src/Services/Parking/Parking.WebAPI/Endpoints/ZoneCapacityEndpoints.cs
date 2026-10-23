using MediatR;
using ParkingSystem.AspNetCore;
using ParkingSystem.BuildingBlocks.Identity;
using ParkingSystem.Parking.Application.ParkingLayout.ZoneCapacity;
using ParkingSystem.Parking.WebAPI.Security;

namespace ParkingSystem.Parking.WebAPI.Endpoints;

// US-OWNER-003: the owner reads the declared capacity of the zones of their lot, with totals and the BR-02.f warning.
// Floors and zones are written through the layout API (/api/parking/owner/lots/{lotId}/layout, US-OWNER-007).
internal static class ZoneCapacityEndpoints
{
    private const string OwnerRoute = "/api/parking/owner/lots/{lotId:guid}/zones";

    public static IEndpointRouteBuilder MapZoneCapacityEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet(OwnerRoute, async (
                Guid lotId, ICurrentUser currentUser, HttpContext httpContext, ISender sender, CancellationToken cancellationToken) =>
            {
                if (!Guid.TryParse(currentUser.SubjectId, out var ownerId))
                    return Results.Forbid();
                var result = await sender.Send(new GetZoneCapacityQuery(lotId, ownerId), cancellationToken);
                return result.IsSuccess
                    ? Results.Ok(result.Value)
                    : BusinessProblem.Create(httpContext, StatusCodes.Status404NotFound, result.Error!);
            })
            .RequireAuthorization(ParkingAuthorization.OwnerPolicy)
            .WithName("GetZoneCapacity")
            .WithSummary("Get the declared capacity of every zone of a lot owned by the caller, with totals per vehicle group")
            .Produces<ZoneCapacityView>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }
}
