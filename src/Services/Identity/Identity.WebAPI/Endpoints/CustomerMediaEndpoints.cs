using Microsoft.AspNetCore.Http;
using ParkingSystem.BuildingBlocks.Identity;
using ParkingSystem.Identity.Application.Profiles.CustomerMedia;

namespace ParkingSystem.Identity.WebAPI.Endpoints;

internal static class CustomerMediaEndpoints
{
    public static void MapCustomerMediaEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/identity/accounts/me/media").RequireAuthorization()
            .WithTags("Customer profile media");

        group.MapPost("", async (HttpRequest request, ICurrentUser user, ICustomerMediaStore store,
            CancellationToken ct) =>
        {
            if (!Guid.TryParse(user.SubjectId, out var accountId)) return Results.Unauthorized();
            if (!request.HasFormContentType) return Results.StatusCode(StatusCodes.Status415UnsupportedMediaType);
            var form = await request.ReadFormAsync(ct);
            var file = form.Files.GetFile("file");
            if (file is null || file.Length == 0) return Results.BadRequest(new { errorCode = "identity.media_file_required" });
            if (file.Length > 5 * 1024 * 1024) return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
            var kind = form["kind"].ToString();
            try
            {
                await using var stream = file.OpenReadStream();
                var saved = await store.SaveAsync(accountId, kind, stream, file.Length, ct);
                return Results.Created(saved.Url, saved);
            }
            catch (ArgumentException error)
            {
                return Results.BadRequest(new { errorCode = "identity.invalid_customer_media", detail = error.Message });
            }
        }).DisableAntiforgery().WithName("UploadMyCustomerMedia");

        group.MapGet("/{mediaId:guid}", async (Guid mediaId, ICurrentUser user,
            ICustomerMediaStore store, CancellationToken ct) =>
        {
            if (!Guid.TryParse(user.SubjectId, out var accountId)) return Results.Unauthorized();
            var media = await store.OpenReadAsync(accountId, mediaId, ct);
            return media is null ? Results.NotFound() : Results.File(media.Value.Content, media.Value.ContentType);
        }).WithName("GetMyCustomerMedia");

        group.MapDelete("/{mediaId:guid}", async (Guid mediaId, ICurrentUser user,
            ICustomerMediaStore store, CancellationToken ct) =>
        {
            if (!Guid.TryParse(user.SubjectId, out var accountId)) return Results.Unauthorized();
            return await store.DeleteAsync(accountId, mediaId, ct) ? Results.NoContent() : Results.NotFound();
        }).WithName("DeleteMyCustomerMedia");
    }
}
