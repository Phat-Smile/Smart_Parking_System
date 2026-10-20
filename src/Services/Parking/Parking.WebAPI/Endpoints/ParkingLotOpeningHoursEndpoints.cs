using MediatR;
using ParkingSystem.AspNetCore;
using ParkingSystem.BuildingBlocks.Identity;
using ParkingSystem.Parking.Application.ParkingLots.OpeningHours;
using ParkingSystem.Parking.WebAPI.Security;

namespace ParkingSystem.Parking.WebAPI.Endpoints;

internal static class ParkingLotOpeningHoursEndpoints
{
    private const string PublicRoute = "/api/parking/lots/{lotId:guid}/opening-hours";
    private const string OwnerRoute = "/api/parking/owner/lots/{lotId:guid}/opening-hours";

    public static IEndpointRouteBuilder MapParkingLotOpeningHoursEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet(PublicRoute, async (Guid lotId, ISender sender, CancellationToken cancellationToken) =>
            {
                var hours = await sender.Send(new GetOpeningHoursQuery(lotId), cancellationToken);
                return hours is null ? Results.NotFound() : Results.Ok(hours);
            })
            .AllowAnonymous()
            .WithName("GetPublicParkingLotOpeningHours")
            .WithSummary("Get opening hours for an approved parking lot")
            .Produces<OpeningHoursView>()
            .Produces(StatusCodes.Status404NotFound);

        app.MapGet(OwnerRoute, async (
                Guid lotId, ICurrentUser currentUser, ISender sender, CancellationToken cancellationToken) =>
            {
                if (!Guid.TryParse(currentUser.SubjectId, out var ownerId))
                    return Results.Forbid();
                var hours = await sender.Send(new GetOpeningHoursQuery(lotId, ownerId), cancellationToken);
                return hours is null ? Results.NotFound() : Results.Ok(hours);
            })
            .RequireAuthorization(ParkingAuthorization.OwnerPolicy)
            .WithName("GetOwnerParkingLotOpeningHours")
            .WithSummary("Get opening hours for a parking lot owned by the caller, including pending lots")
            .Produces<OpeningHoursView>()
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound);

        app.MapPut(OwnerRoute, async (
                Guid lotId,
                WeeklyOpeningHours schedule,
                ICurrentUser currentUser,
                HttpContext httpContext,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                if (!Guid.TryParse(currentUser.SubjectId, out var ownerId))
                    return Results.Forbid();
                var result = await sender.Send(
                    new ConfigureOpeningHoursCommand(lotId, ownerId, schedule), cancellationToken);
                return result.IsSuccess
                    ? Results.Ok(result.Value)
                    : BusinessProblem.Create(httpContext, StatusCodes.Status404NotFound, result.Error!);
            })
            .RequireAuthorization(ParkingAuthorization.OwnerPolicy)
            .WithName("ConfigureParkingLotOpeningHours")
            .WithSummary("Replace a lot's complete seven-day weekly opening schedule (owner only)")
            .Produces<OpeningHoursView>()
            .ProducesValidationProblem()
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }
}
