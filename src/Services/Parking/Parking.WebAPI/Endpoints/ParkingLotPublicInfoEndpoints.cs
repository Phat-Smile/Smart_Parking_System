using MediatR;
using ParkingSystem.BuildingBlocks.Identity;
using ParkingSystem.Parking.Application.ParkingLots.PublicInfo;
using ParkingSystem.Parking.Domain.ParkingLots;
using ParkingSystem.Parking.WebAPI.Security;

namespace ParkingSystem.Parking.WebAPI.Endpoints;

internal static class ParkingLotPublicInfoEndpoints
{
    public static IEndpointRouteBuilder MapParkingLotPublicInfoEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPut("/api/parking/owner/lots/{lotId:guid}/public-info", async (
            Guid lotId, ParkingLotPublicInfo request, ICurrentUser user, ISender sender, CancellationToken ct) =>
        {
            if (!Guid.TryParse(user.SubjectId, out var ownerId) || ownerId == Guid.Empty)
                return Results.Forbid();
            var result = await sender.Send(new ConfigurePublicInfoCommand(lotId, ownerId, request), ct);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.NotFound();
        })
        .RequireAuthorization(ParkingAuthorization.OwnerPolicy)
        .WithName("ConfigureParkingLotPublicInfo")
        .WithSummary("Owner updates public lot description, rules and references to pre-published images")
        .Produces<ParkingLotPublicInfo>(StatusCodes.Status200OK)
        .ProducesValidationProblem()
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound);
        return app;
    }
}
