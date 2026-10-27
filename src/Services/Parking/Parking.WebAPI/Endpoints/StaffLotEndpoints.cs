using MediatR;
using ParkingSystem.AspNetCore;
using ParkingSystem.BuildingBlocks.Identity;
using ParkingSystem.Parking.Application.ParkingLots.ListStaffLots;
using ParkingSystem.Parking.Application.ParkingSessions.ListLotParkingSessions;
using ParkingSystem.Parking.WebAPI.Security;

namespace ParkingSystem.Parking.WebAPI.Endpoints;

// Lead task S2 (2026-10-27): what a staff member or lot owner sees after signing in. Staff role in the token opens the
// route; the lot assignment decides which lots and sessions they see (TECH-12).
internal static class StaffLotEndpoints
{
    private const string Route = "/api/parking/staff/lots";

    public static IEndpointRouteBuilder MapStaffLotEndpoints(this IEndpointRouteBuilder app)
    {
        // Replaces the /staff/assigned-lot route the web app called before; an account with no lot gets [].
        app.MapGet(Route, async (ICurrentUser currentUser, ISender sender, CancellationToken cancellationToken) =>
            {
                if (!Guid.TryParse(currentUser.SubjectId, out var accountId))
                    return Results.Forbid();

                return Results.Ok(await sender.Send(new ListStaffLotsQuery(accountId), cancellationToken));
            })
            .RequireAuthorization(ParkingAuthorization.StaffPolicy)
            .WithName("ListStaffLots")
            .WithSummary("List the approved parking lots the caller is assigned to")
            .Produces<IReadOnlyList<StaffLot>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        // Missing page or pageSize take the defaults; out of range values and an unknown status return 400.
        app.MapGet($"{Route}/{{lotId:guid}}/sessions", async (
                Guid lotId,
                string? status,
                int? page,
                int? pageSize,
                ICurrentUser currentUser,
                HttpContext httpContext,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                if (!Guid.TryParse(currentUser.SubjectId, out var actorUserId))
                    return Results.Forbid();

                var result = await sender.Send(
                    new ListLotParkingSessionsQuery(
                        lotId,
                        status,
                        page ?? LotParkingSessionPaging.DefaultPage,
                        pageSize ?? LotParkingSessionPaging.DefaultPageSize,
                        actorUserId),
                    cancellationToken);

                // The only business error is a lot the caller is not assigned to.
                return result.IsSuccess
                    ? Results.Ok(result.Value)
                    : BusinessProblem.Create(httpContext, StatusCodes.Status403Forbidden, result.Error!);
            })
            .RequireAuthorization(ParkingAuthorization.StaffPolicy)
            .WithName("ListLotParkingSessions")
            .WithSummary("List the parking sessions of a lot the caller is assigned to")
            .Produces<LotParkingSessionPage>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        return app;
    }
}
