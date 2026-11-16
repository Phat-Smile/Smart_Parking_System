using Microsoft.AspNetCore.Http;
using ParkingSystem.AspNetCore;
using ParkingSystem.BuildingBlocks.Identity;
using ParkingSystem.BuildingBlocks.Results;
using ParkingSystem.Notification.Application.CustomerNotifications;

namespace ParkingSystem.Notification.WebAPI;

public static class CustomerNotificationEndpoints
{
    public static void MapCustomerNotificationEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/notification/me").RequireAuthorization().WithTags("Customer notifications");

        group.MapGet("", async (HttpContext httpContext, ICurrentUser user, ICustomerNotificationStore store,
            bool? unreadOnly, int? page, int? pageSize, CancellationToken ct) =>
        {
            if (!Guid.TryParse(user.SubjectId, out var userId)) return Results.Unauthorized();
            var actualPage = page ?? 1;
            var actualSize = pageSize ?? 20;
            if (actualPage < 1 || actualSize is < 1 or > 100 || actualPage > int.MaxValue / actualSize)
                return BusinessProblem.Create(httpContext, StatusCodes.Status400BadRequest,
                    new Error("notification.invalid_paging", "Invalid paging parameters."));
            return Results.Ok(await store.ListAsync(userId, unreadOnly ?? false, actualPage, actualSize, ct));
        }).WithName("GetMyNotifications")
        .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapPatch("/{notificationId:guid}/read", async (Guid notificationId, ICurrentUser user,
            ICustomerNotificationStore store, CancellationToken ct) =>
        {
            if (!Guid.TryParse(user.SubjectId, out var userId)) return Results.Unauthorized();
            return await store.MarkReadAsync(userId, notificationId, DateTimeOffset.UtcNow, ct)
                ? Results.NoContent() : Results.NotFound();
        }).WithName("MarkMyNotificationRead");

        group.MapPatch("/read-all", async (ICurrentUser user, ICustomerNotificationStore store,
            CancellationToken ct) =>
        {
            if (!Guid.TryParse(user.SubjectId, out var userId)) return Results.Unauthorized();
            return Results.Ok(new { updated = await store.MarkAllReadAsync(userId, DateTimeOffset.UtcNow, ct) });
        }).WithName("MarkAllMyNotificationsRead");
    }
}
