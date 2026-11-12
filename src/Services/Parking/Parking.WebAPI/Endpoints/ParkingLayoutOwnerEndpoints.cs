using MediatR;
using ParkingSystem.AspNetCore;
using ParkingSystem.BuildingBlocks.Identity;
using ParkingSystem.BuildingBlocks.Results;
using ParkingSystem.Parking.Application.ParkingLayout.ManageLayout;
using ParkingSystem.Parking.Application.ParkingLayout.SetSlotMaintenance;
using ParkingSystem.Parking.WebAPI.Security;

namespace ParkingSystem.Parking.WebAPI.Endpoints;

internal static class ParkingLayoutOwnerEndpoints
{
    private const string Route = "/api/parking/owner/lots/{lotId:guid}/layout";

    public sealed record SaveLayoutRequest(int ExpectedVersion, IReadOnlyList<LayoutFloorInput>? Floors);

    public sealed record SetSlotMaintenanceRequest(bool UnderMaintenance, string? Reason = null);

    public static IEndpointRouteBuilder MapParkingLayoutOwnerEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet(Route, async (Guid lotId, ICurrentUser user, HttpContext context,
            ISender sender, CancellationToken cancellationToken) =>
        {
            if (!Guid.TryParse(user.SubjectId, out var accountId)) return Results.Forbid();
            var result = await sender.Send(new GetParkingLayoutQuery(lotId, accountId), cancellationToken);
            return result.IsSuccess ? Results.Ok(result.Value) : ToProblem(result.Error!, context);
        })
        .RequireAuthorization(ParkingAuthorization.OwnerPolicy)
        .WithName("GetOwnerParkingLayout")
        .WithSummary("Read floors, zones and slots for an assigned owner")
        .Produces<ParkingLotLayout>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound);

        app.MapPut(Route, async (Guid lotId, SaveLayoutRequest request, ICurrentUser user,
            HttpContext context, ISender sender, CancellationToken cancellationToken) =>
        {
            if (!Guid.TryParse(user.SubjectId, out var accountId)) return Results.Forbid();
            var result = await sender.Send(new SaveParkingLayoutCommand(lotId, accountId, request.ExpectedVersion, request.Floors), cancellationToken);
            return result.IsSuccess ? Results.Ok(result.Value) : ToProblem(result.Error!, context);
        })
        .RequireAuthorization(ParkingAuthorization.OwnerPolicy)
        .WithName("SaveOwnerParkingLayout")
        .WithSummary("Save a layout without deleting or resetting existing operational slots")
        .Produces<ParkingLotLayout>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict);

        app.MapPut("/api/parking/owner/lots/{lotId:guid}/slots/{slotId:guid}/maintenance", async (
                Guid lotId,
                Guid slotId,
                SetSlotMaintenanceRequest request,
                ICurrentUser user,
                HttpContext context,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                if (!Guid.TryParse(user.SubjectId, out var accountId)) return Results.Forbid();
                var role = context.User.FindFirst("role")?.Value ?? "ParkingLotOwner";

                var command = new SetSlotMaintenanceCommand(
                    lotId, slotId, accountId, role, request.UnderMaintenance, request.Reason);

                var result = await sender.Send(command, cancellationToken);
                if (result.IsSuccess)
                {
                    return Results.Ok(result.Value);
                }

                var status = result.Error == SlotMaintenanceErrors.SlotNotFound ? StatusCodes.Status404NotFound
                    : result.Error == SlotMaintenanceErrors.LotNotFoundOrNotOwned ? StatusCodes.Status403Forbidden
                    : result.Error == SlotMaintenanceErrors.CannotMaintainActiveSlot ? StatusCodes.Status409Conflict
                    : StatusCodes.Status400BadRequest;

                return BusinessProblem.Create(context, status, result.Error!);
            })
            .RequireAuthorization(ParkingAuthorization.OwnerPolicy)
            .WithName("SetSlotMaintenance")
            .WithSummary("Bật/tắt trạng thái bảo trì cho từng ô đỗ (US-OWNER-003/008)")
            .WithDescription("Chuyển trạng thái ô đỗ sang UnderMaintenance (Bảo trì) khi ô hỏng hoặc có vật cản, tự động cập nhật sức chứa.")
            .Produces<SlotMaintenanceResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        return app;
    }

    private static IResult ToProblem(Error error, HttpContext context)
    {
        var status = error == ParkingLayoutErrors.LotNotFound ? StatusCodes.Status404NotFound
            : error == ParkingLayoutErrors.OwnerRequired ? StatusCodes.Status403Forbidden
            : error.Code == "parking.layout.invalid" ? StatusCodes.Status400BadRequest
            : StatusCodes.Status409Conflict;
        return BusinessProblem.Create(context, status, error);
    }
}
