using MediatR;
using Microsoft.AspNetCore.Mvc;
using ParkingSystem.BuildingBlocks.Identity;
using ParkingSystem.Identity.Application.Accounts.OwnerApplications;
using ParkingSystem.Identity.Domain.Accounts;

namespace ParkingSystem.Identity.WebAPI.Endpoints;

public sealed record UpdateOwnerTypeRequest(string OwnerType);

internal static class OwnerDocumentEndpoints
{
    public static IEndpointRouteBuilder MapOwnerDocumentEndpoints(this IEndpointRouteBuilder app)
    {
        // API-ID-01: Lấy danh mục checklist giấy tờ yêu cầu theo loại chủ thể
        app.MapGet("/api/identity/owner-document-requirements", async (
                [FromQuery] string? ownerType,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                if (!OwnerTypeParser.TryParse(ownerType, out var parsedType))
                {
                    return Results.Problem(
                        statusCode: StatusCodes.Status400BadRequest,
                        title: "Loại chủ thể không hợp lệ.",
                        detail: "Loại chủ thể phải là Individual, HouseholdBusiness hoặc Enterprise.",
                        extensions: new Dictionary<string, object?> { ["errorCode"] = "request.validation_failed" });
                }

                var query = new GetOwnerDocumentRequirementsQuery(parsedType);
                var result = await sender.Send(query, cancellationToken);
                return Results.Ok(result.Value);
            })
            .RequireAuthorization()
            .WithTags("7. Hồ sơ Chủ bãi (Owner Applications)")
            .WithName("GetOwnerDocumentRequirements")
            .WithSummary("Lấy danh mục checklist giấy tờ yêu cầu theo loại chủ thể")
            .Produces<OwnerDocumentRequirementsDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized);

        // MOD-ID-01 / R2: Cập nhật loại chủ thể cho hồ sơ đang tạo / đang xét duyệt
        app.MapPut("/api/identity/accounts/me/owner-application", async (
                UpdateOwnerTypeRequest? request,
                ICurrentUser currentUser,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                if (!Guid.TryParse(currentUser.SubjectId, out var accountId))
                    return Results.Unauthorized();

                if (!OwnerTypeParser.TryParse(request?.OwnerType, out var parsedType))
                {
                    return Results.Problem(
                        statusCode: StatusCodes.Status400BadRequest,
                        title: "Loại chủ thể không hợp lệ.",
                        detail: "Loại chủ thể phải là Individual, HouseholdBusiness hoặc Enterprise.",
                        extensions: new Dictionary<string, object?> { ["errorCode"] = "request.validation_failed" });
                }

                var command = new UpdateOwnerTypeCommand(accountId, parsedType);
                var result = await sender.Send(command, cancellationToken);
                if (!result.IsSuccess)
                {
                    var statusCode = result.Error!.Code switch
                    {
                        "identity.owner_application_not_found" => StatusCodes.Status404NotFound,
                        "identity.owner_application_locked" => StatusCodes.Status409Conflict,
                        _ => StatusCodes.Status400BadRequest
                    };

                    return Results.Problem(
                        statusCode: statusCode,
                        title: result.Error.Message,
                        detail: result.Error.Message,
                        extensions: new Dictionary<string, object?> { ["errorCode"] = result.Error.Code });
                }

                return Results.Ok(result.Value);
            })
            .RequireAuthorization()
            .WithTags("7. Hồ sơ Chủ bãi (Owner Applications)")
            .WithName("UpdateOwnerType")
            .WithSummary("Cập nhật loại chủ thể cho hồ sơ đăng ký chủ bãi")
            .Produces<MyOwnerApplicationDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict);

        // API-ID-03: Lấy danh sách giấy tờ đã nộp của hồ sơ hiện tại
        app.MapGet("/api/identity/accounts/me/owner-application/documents", async (
                ICurrentUser currentUser,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                if (!Guid.TryParse(currentUser.SubjectId, out var accountId))
                    return Results.Unauthorized();

                var query = new ListMyOwnerDocumentsQuery(accountId);
                var result = await sender.Send(query, cancellationToken);
                if (!result.IsSuccess)
                {
                    return Results.Problem(
                        statusCode: StatusCodes.Status404NotFound,
                        title: result.Error!.Message,
                        detail: result.Error.Message,
                        extensions: new Dictionary<string, object?> { ["errorCode"] = result.Error.Code });
                }

                return Results.Ok(result.Value);
            })
            .RequireAuthorization()
            .WithTags("7. Hồ sơ Chủ bãi (Owner Applications)")
            .WithName("ListMyOwnerDocuments")
            .WithSummary("Lấy danh sách giấy tờ đã tải lên của chủ bãi")
            .Produces<IReadOnlyList<OwnerDocumentDto>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound);

        // API-ID-02: Tải lên giấy tờ cho hồ sơ chủ bãi
        app.MapPost("/api/identity/accounts/me/owner-application/documents", async (
                HttpRequest httpRequest,
                ICurrentUser currentUser,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                if (!Guid.TryParse(currentUser.SubjectId, out var accountId))
                    return Results.Unauthorized();

                if (!httpRequest.HasFormContentType)
                {
                    return Results.Problem(
                        statusCode: StatusCodes.Status415UnsupportedMediaType,
                        title: "Yêu cầu phải có định dạng multipart/form-data.",
                        detail: "Yêu cầu phải có định dạng multipart/form-data.",
                        extensions: new Dictionary<string, object?> { ["errorCode"] = "request.invalid_content_type" });
                }

                var form = await httpRequest.ReadFormAsync(cancellationToken);
                var documentType = form["documentType"].ToString();
                if (string.IsNullOrWhiteSpace(documentType))
                {
                    return Results.Problem(
                        statusCode: StatusCodes.Status400BadRequest,
                        title: "Loại giấy tờ không được để trống.",
                        detail: "Loại giấy tờ không được để trống.",
                        extensions: new Dictionary<string, object?> { ["errorCode"] = "identity.owner_document_type_required" });
                }

                var file = form.Files.GetFile("file") ?? (form.Files.Count > 0 ? form.Files[0] : null);
                if (file is null || file.Length == 0)
                {
                    return Results.Problem(
                        statusCode: StatusCodes.Status400BadRequest,
                        title: "Tệp tin đính kèm không được để trống.",
                        detail: "Tệp tin đính kèm không được để trống.",
                        extensions: new Dictionary<string, object?> { ["errorCode"] = "identity.owner_document_file_required" });
                }

                await using var stream = file.OpenReadStream();
                var command = new UploadOwnerDocumentCommand(accountId, documentType, file.FileName, stream, file.Length);
                var result = await sender.Send(command, cancellationToken);

                if (!result.IsSuccess)
                {
                    var statusCode = result.Error!.Code switch
                    {
                        "identity.owner_application_not_found" => StatusCodes.Status404NotFound,
                        "identity.owner_application_locked" => StatusCodes.Status409Conflict,
                        "identity.owner_document_limit_reached" => StatusCodes.Status409Conflict,
                        "identity.owner_document_too_large" => StatusCodes.Status413PayloadTooLarge,
                        "identity.owner_type_required" => StatusCodes.Status400BadRequest,
                        "identity.owner_document_invalid_type" => StatusCodes.Status400BadRequest,
                        "identity.owner_document_unsupported_content" => StatusCodes.Status400BadRequest,
                        _ => StatusCodes.Status400BadRequest
                    };

                    return Results.Problem(
                        statusCode: statusCode,
                        title: result.Error.Message,
                        detail: result.Error.Message,
                        extensions: new Dictionary<string, object?> { ["errorCode"] = result.Error.Code });
                }

                return Results.Created($"/api/identity/accounts/me/owner-application/documents/{result.Value!.DocumentType}", result.Value);
            })
            .RequireAuthorization()
            .DisableAntiforgery()
            .WithTags("7. Hồ sơ Chủ bãi (Owner Applications)")
            .WithName("UploadOwnerDocument")
            .WithSummary("Tải lên giấy tờ chứng minh cho hồ sơ chủ bãi")
            .Produces<OwnerDocumentDto>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict)
            .Produces(StatusCodes.Status413PayloadTooLarge);

        // API-ID-04: Xóa một giấy tờ đã nộp
        app.MapDelete("/api/identity/accounts/me/owner-application/documents/{documentType}", async (
                string documentType,
                ICurrentUser currentUser,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                if (!Guid.TryParse(currentUser.SubjectId, out var accountId))
                    return Results.Unauthorized();

                var command = new DeleteOwnerDocumentCommand(accountId, documentType);
                var result = await sender.Send(command, cancellationToken);

                if (!result.IsSuccess)
                {
                    var statusCode = result.Error!.Code switch
                    {
                        "identity.owner_application_not_found" => StatusCodes.Status404NotFound,
                        "identity.owner_document_not_found" => StatusCodes.Status404NotFound,
                        "identity.owner_application_locked" => StatusCodes.Status409Conflict,
                        _ => StatusCodes.Status400BadRequest
                    };

                    return Results.Problem(
                        statusCode: statusCode,
                        title: result.Error.Message,
                        detail: result.Error.Message,
                        extensions: new Dictionary<string, object?> { ["errorCode"] = result.Error.Code });
                }

                return Results.Ok(new { message = "Xóa giấy tờ thành công." });
            })
            .RequireAuthorization()
            .WithTags("7. Hồ sơ Chủ bãi (Owner Applications)")
            .WithName("DeleteOwnerDocument")
            .WithSummary("Xóa giấy tờ đã tải lên của hồ sơ chủ bãi")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict);

        return app;
    }
}
