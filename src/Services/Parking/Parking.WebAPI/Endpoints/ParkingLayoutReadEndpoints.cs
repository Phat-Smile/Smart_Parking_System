using MediatR;
using ParkingSystem.AspNetCore;
using ParkingSystem.BuildingBlocks.Identity;
using ParkingSystem.Parking.Application.ParkingLayout.ManageLayout;
using ParkingSystem.Parking.WebAPI.Security;

namespace ParkingSystem.Parking.WebAPI.Endpoints;

internal static class ParkingLayoutReadEndpoints
{
    public static IEndpointRouteBuilder MapParkingLayoutReadEndpoints(this IEndpointRouteBuilder app)
    {
        // Shared, versioned geometry for both top-down 2D and Three.js 3D rendering.
        // The effective statuses are a snapshot, so never cache this response as immutable geometry.
        app.MapGet("/api/parking/lots/{lotId:guid}/layout", async (
            Guid lotId, HttpContext context, ISender sender, CancellationToken cancellationToken) =>
        {
            var result = await sender.Send(new GetPublicParkingLayoutQuery(lotId), cancellationToken);
            if (result.IsFailure)
                return BusinessProblem.Create(context, StatusCodes.Status404NotFound, result.Error!);
            context.Response.Headers.CacheControl = "no-store";
            return Results.Ok(result.Value);
        })
        .AllowAnonymous()
        .WithName("GetPublicParkingLayout")
        .WithSummary("Read 2D/3D layout geometry and current slot states of an approved lot")
        .Produces<ParkingLotLayout>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound);

        app.MapGet("/api/parking/owner/lots/{lotId:guid}/zone-utilization", async (
            Guid lotId, ICurrentUser user, HttpContext context, ISender sender, CancellationToken cancellationToken) =>
        {
            if (!Guid.TryParse(user.SubjectId, out var accountId) || accountId == Guid.Empty)
                return Results.Forbid();
            var result = await sender.Send(new GetOwnerZoneUtilizationQuery(lotId, accountId), cancellationToken);
            if (result.IsFailure)
            {
                var status = result.Error == ParkingLayoutErrors.LotNotFound
                    ? StatusCodes.Status404NotFound : StatusCodes.Status403Forbidden;
                return BusinessProblem.Create(context, status, result.Error!);
            }
            context.Response.Headers.CacheControl = "no-store";
            return Results.Ok(result.Value);
        })
        .RequireAuthorization(ParkingAuthorization.OwnerPolicy)
        .WithName("GetOwnerZoneUtilization")
        .WithSummary("Read actual slot and motorbike capacity utilization by zone for an assigned owner")
        .Produces<ParkingLotZoneUtilization>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }
}
