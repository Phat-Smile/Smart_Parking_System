using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using MediatR;
using ParkingSystem.AspNetCore;
using ParkingSystem.BuildingBlocks.Identity;
using ParkingSystem.Parking.Application.ParkingLots.AssignLotMember;
using ParkingSystem.Parking.Application.ParkingLots.ListLotAssignments;
using ParkingSystem.Parking.Application.ParkingLots.RemoveLotMember;
using ParkingSystem.Parking.Domain.ParkingLots;
using ParkingSystem.Parking.WebAPI.Security;

namespace ParkingSystem.Parking.WebAPI.Endpoints;

// TECH-12: Admin gán / gỡ Chủ bãi, Nhân viên cho một bãi. Chủ bãi tự phân công sẽ thêm ở US-OWNER-009 (S5).
internal static class ParkingLotAssignmentEndpoints
{
    private const string ListRoute = "/api/parking/admin/lots/{lotId:guid}/assignments";
    private const string Route = ListRoute + "/{accountId:guid}";

    public sealed record AssignLotMemberRequest(
        [property: Required, AllowedValues(nameof(ParkingLotMemberRole.Owner), nameof(ParkingLotMemberRole.Staff))]
        [property: Description("Owner or Staff, case-insensitive.")]
        string? Role);

    public static IEndpointRouteBuilder MapParkingLotAssignmentEndpoints(this IEndpointRouteBuilder app)
    {
        // Bãi có nhưng chưa ai được gán thì trả [].
        app.MapGet(ListRoute, async (
                Guid lotId,
                HttpContext httpContext,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(new ListLotAssignmentsQuery(lotId), cancellationToken);
                return result.IsSuccess
                    ? Results.Ok(result.Value)
                    : BusinessProblem.Create(httpContext, StatusCodes.Status404NotFound, result.Error!);
            })
            .RequireAuthorization(ParkingAuthorization.AdminPolicy)
            .WithName("ListParkingLotAssignments")
            .WithSummary("List the accounts assigned to a parking lot (SystemAdmin)")
            .Produces<IReadOnlyList<LotAssignment>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        // Gọi lại cùng vai trò vẫn trả 200 và không ghi gì thêm.
        app.MapPut(Route, async (
                Guid lotId,
                Guid accountId,
                AssignLotMemberRequest request,
                ICurrentUser currentUser,
                HttpContext httpContext,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                if (!Guid.TryParse(currentUser.SubjectId, out var adminId))
                    return Results.Forbid();

                var result = await sender.Send(new AssignLotMemberCommand(lotId, accountId, request.Role ?? string.Empty, adminId), cancellationToken);
                return result.IsSuccess
                    ? Results.Ok(result.Value)
                    : BusinessProblem.Create(httpContext, StatusCodes.Status404NotFound, result.Error!);
            })
            .RequireAuthorization(ParkingAuthorization.AdminPolicy)
            .WithName("AssignParkingLotMember")
            .WithSummary("Assign an owner or staff account to a parking lot (SystemAdmin)")
            .Produces<LotMember>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        app.MapDelete(Route, async (
                Guid lotId,
                Guid accountId,
                ICurrentUser currentUser,
                HttpContext httpContext,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                if (!Guid.TryParse(currentUser.SubjectId, out var adminId))
                    return Results.Forbid();

                var result = await sender.Send(new RemoveLotMemberCommand(lotId, accountId, adminId), cancellationToken);
                return result.IsSuccess
                    ? Results.NoContent()
                    : BusinessProblem.Create(httpContext, StatusCodes.Status404NotFound, result.Error!);
            })
            .RequireAuthorization(ParkingAuthorization.AdminPolicy)
            .WithName("RemoveParkingLotMember")
            .WithSummary("Remove an account from a parking lot (SystemAdmin)")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }
}
