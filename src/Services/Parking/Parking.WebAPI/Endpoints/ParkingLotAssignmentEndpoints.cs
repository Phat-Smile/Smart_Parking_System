using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using MediatR;
using ParkingSystem.AspNetCore;
using ParkingSystem.BuildingBlocks.Identity;
using ParkingSystem.Parking.Application.ParkingLots.AssignLotMember;
using ParkingSystem.Parking.Application.ParkingLots.AssignLotStaff;
using ParkingSystem.Parking.Application.ParkingLots.ListLotAssignments;
using ParkingSystem.Parking.Application.ParkingLots.RemoveLotMember;
using ParkingSystem.Parking.Application.ParkingLots.RemoveLotStaff;
using ParkingSystem.Parking.Domain.ParkingLots;
using ParkingSystem.Parking.WebAPI.Security;

namespace ParkingSystem.Parking.WebAPI.Endpoints;

// TECH-12 / US-OWNER-009: Admin & Chủ bãi phân công Nhân viên cho bãi đỗ xe.
internal static class ParkingLotAssignmentEndpoints
{
    private const string AdminListRoute = "/api/parking/admin/lots/{lotId:guid}/assignments";
    private const string AdminRoute = AdminListRoute + "/{accountId:guid}";

    private const string OwnerListRoute = "/api/parking/owner/lots/{lotId:guid}/assignments";
    private const string OwnerRoute = OwnerListRoute + "/{accountId:guid}";

    public sealed record AssignLotMemberRequest(
        [property: Required, AllowedValues(nameof(ParkingLotMemberRole.Owner), nameof(ParkingLotMemberRole.Staff))]
        [property: Description("Owner or Staff, case-insensitive.")]
        string? Role);

    public sealed record AssignOwnerStaffRequest(
        [property: Required, EmailAddress, MaxLength(254)]
        [property: Description("Exact email of the staff account to assign.")]
        string Email);

    public static IEndpointRouteBuilder MapParkingLotAssignmentEndpoints(this IEndpointRouteBuilder app)
    {
        // ------------------ ADMIN ROUTES ------------------

        // Bãi có nhưng chưa ai được gán thì trả [].
        app.MapGet(AdminListRoute, async (
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
        app.MapPut(AdminRoute, async (
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

        app.MapDelete(AdminRoute, async (
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

        // ------------------ OWNER ROUTES ------------------

        // Chủ bãi xem danh sách nhân viên thuộc bãi của mình
        app.MapGet(OwnerListRoute, async (
                Guid lotId,
                ICurrentUser currentUser,
                HttpContext httpContext,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                if (!Guid.TryParse(currentUser.SubjectId, out var ownerId))
                    return Results.Forbid();

                var result = await sender.Send(new ListOwnerLotAssignmentsQuery(lotId, ownerId), cancellationToken);
                if (result.IsFailure)
                {
                    var status = result.Error == AssignLotStaffErrors.NotLotOwner
                        ? StatusCodes.Status403Forbidden
                        : StatusCodes.Status404NotFound;
                    return BusinessProblem.Create(httpContext, status, result.Error!);
                }

                return Results.Ok(result.Value);
            })
            .RequireAuthorization(ParkingAuthorization.ParkingLotOwnerPolicy)
            .WithName("ListOwnerParkingLotAssignments")
            .WithSummary("List the accounts assigned to an owned parking lot (ParkingLotOwner)")
            .Produces<IReadOnlyList<LotAssignment>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        // Chủ bãi phân công nhân viên bằng email chính xác (rate-limited 10/min, idempotent)
        app.MapPost(OwnerListRoute, async (
                Guid lotId,
                AssignOwnerStaffRequest request,
                ICurrentUser currentUser,
                HttpContext httpContext,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                if (!Guid.TryParse(currentUser.SubjectId, out var ownerId))
                    return Results.Forbid();

                var result = await sender.Send(new AssignLotStaffByEmailCommand(lotId, request.Email, ownerId), cancellationToken);
                if (result.IsFailure)
                {
                    var status = result.Error == AssignLotStaffErrors.NotLotOwner
                        ? StatusCodes.Status403Forbidden
                        : StatusCodes.Status404NotFound;
                    return BusinessProblem.Create(httpContext, status, result.Error!);
                }

                return Results.Ok(result.Value);
            })
            .RequireAuthorization(ParkingAuthorization.ParkingLotOwnerPolicy)
            .RequireRateLimiting(OwnerStaffAssignmentRateLimiting.PolicyName)
            .WithName("AssignOwnerParkingLotStaff")
            .WithSummary("Assign a staff account to an owned parking lot by email (ParkingLotOwner)")
            .Produces<LotMember>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        // Chủ bãi gỡ nhân viên khỏi bãi của mình
        app.MapDelete(OwnerRoute, async (
                Guid lotId,
                Guid accountId,
                ICurrentUser currentUser,
                HttpContext httpContext,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                if (!Guid.TryParse(currentUser.SubjectId, out var ownerId))
                    return Results.Forbid();

                var result = await sender.Send(new RemoveLotStaffByOwnerCommand(lotId, accountId, ownerId), cancellationToken);
                if (result.IsFailure)
                {
                    var status = result.Error == AssignLotStaffErrors.NotLotOwner
                        ? StatusCodes.Status403Forbidden
                        : StatusCodes.Status404NotFound;
                    return BusinessProblem.Create(httpContext, status, result.Error!);
                }

                return Results.NoContent();
            })
            .RequireAuthorization(ParkingAuthorization.ParkingLotOwnerPolicy)
            .WithName("RemoveOwnerParkingLotStaff")
            .WithSummary("Remove a staff account from an owned parking lot (ParkingLotOwner)")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }
}
