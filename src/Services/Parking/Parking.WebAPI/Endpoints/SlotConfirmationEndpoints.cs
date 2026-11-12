using System.ComponentModel.DataAnnotations;
using MediatR;
using ParkingSystem.AspNetCore;
using ParkingSystem.BuildingBlocks.Identity;
using ParkingSystem.BuildingBlocks.Results;
using ParkingSystem.Parking.Application.ParkingLayout.SlotConfirmation;
using ParkingSystem.Parking.Application.ParkingLots;
using ParkingSystem.Parking.WebAPI.Security;

namespace ParkingSystem.Parking.WebAPI.Endpoints;

// US-PARK-005: staff or the lot owner say what they see at a slot (BR-04.c, BR-04.e, transition table after BR-06).
internal static class SlotConfirmationEndpoints
{
    private const string Route = "/api/parking/slots";

    public sealed record ConfirmSlotParkedRequest([property: StringLength(ConfirmSlotErrors.ReasonMaxLength)] string? Reason);

    public sealed record ConfirmSlotEmptyRequest(
        [property: Required, StringLength(ConfirmSlotErrors.ReasonMaxLength, MinimumLength = 1)] string? Reason);

    public static IEndpointRouteBuilder MapSlotConfirmationEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost($"{Route}/{{slotId:guid}}/confirm-parked", async (
                Guid slotId,
                ConfirmSlotParkedRequest request,
                ICurrentUser currentUser,
                HttpContext httpContext,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                if (!Guid.TryParse(currentUser.SubjectId, out var actorUserId))
                    return Results.Forbid();

                var result = await sender.Send(new ConfirmSlotParkedCommand(slotId, request.Reason, actorUserId), cancellationToken);
                return ToResponse(result, httpContext);
            })
            .RequireAuthorization(ParkingAuthorization.StaffPolicy)
            .WithName("ConfirmSlotParked")
            .WithSummary("Confirm the car is parked in the slot")
            .Produces<ConfirmedSlot>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status415UnsupportedMediaType);

        app.MapPost($"{Route}/{{slotId:guid}}/confirm-empty", async (
                Guid slotId,
                ConfirmSlotEmptyRequest request,
                ICurrentUser currentUser,
                HttpContext httpContext,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                if (!Guid.TryParse(currentUser.SubjectId, out var actorUserId))
                    return Results.Forbid();

                var result = await sender.Send(new ConfirmSlotEmptyCommand(slotId, request.Reason ?? string.Empty, actorUserId), cancellationToken);
                return ToResponse(result, httpContext);
            })
            .RequireAuthorization(ParkingAuthorization.StaffPolicy)
            .WithName("ConfirmSlotEmpty")
            .WithSummary("Confirm a slot waiting for verification is empty and give it back")
            .Produces<ConfirmedSlot>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status415UnsupportedMediaType);

        return app;
    }

    private static IResult ToResponse(Result<ConfirmedSlot> result, HttpContext httpContext)
    {
        if (result.IsSuccess)
            return Results.Ok(result.Value);

        var statusCode = result.Error == ConfirmSlotErrors.SlotNotFound
            ? StatusCodes.Status404NotFound
            : result.Error == ParkingLotAccessErrors.AccessDenied
                ? StatusCodes.Status403Forbidden
                : StatusCodes.Status409Conflict;
        return BusinessProblem.Create(httpContext, statusCode, result.Error!);
    }
}
