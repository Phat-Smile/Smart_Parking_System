using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using ParkingSystem.AspNetCore;
using ParkingSystem.BuildingBlocks.Results;
using ParkingSystem.Payment.Application.Common.Constants;
using ParkingSystem.Payment.Application.Features.RefundRequests.Commands.ConfirmRefundRequestByOwner;
using ParkingSystem.Payment.Application.Features.RefundRequests.Commands.CreateRefundRequest;
using ParkingSystem.Payment.Application.Features.RefundRequests.Commands.RejectRefundRequest;
using ParkingSystem.Payment.Application.Features.RefundRequests.Queries.ListRefundRequests;
using ParkingSystem.Payment.Domain.Enums;
using ParkingSystem.Payment.WebAPI.Security;

namespace ParkingSystem.Payment.WebAPI.Endpoints;

public static class RefundRequestEndpoints
{
    public sealed record CreateRefundRequestDto(
        Guid LotId,
        decimal Amount,
        string Reason,
        ReferenceType? ReferenceType = null,
        string? ReferenceId = null);

    public sealed record RejectRefundRequestDto(
        string Reason);

    public sealed record ListRefundRequestsParameters(
        RefundStatus? Status = null,
        Guid? LotId = null,
        int Page = 1,
        int PageSize = 20);

    public static IEndpointRouteBuilder MapRefundRequestEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/payment/refund-requests")
            .WithTags("Yêu cầu hoàn tiền (Refund Requests)");

        // POST /api/payment/refund-requests: Customer creates refund request
        group.MapPost("/", async (
            CreateRefundRequestDto request,
            HttpContext httpContext,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            var callerId = GetUserId(httpContext);
            if (callerId == Guid.Empty)
                return Results.Unauthorized();

            var callerRole = GetUserRole(httpContext);

            var command = new CreateRefundRequestCommand(
                CallerId: callerId,
                CallerRole: callerRole,
                LotId: request.LotId,
                Amount: request.Amount,
                Reason: request.Reason,
                ReferenceType: request.ReferenceType,
                ReferenceId: request.ReferenceId);

            var result = await sender.Send(command, cancellationToken);
            return result.IsSuccess
                ? Results.Created($"/api/payment/refund-requests/{result.Value!.Id}", result.Value)
                : ToProblem(httpContext, result.Error!);
        })
        .RequireAuthorization(PaymentAuthorization.CustomerPolicy)
        .WithName("CreateRefundRequest")
        .WithSummary("Tạo yêu cầu hoàn tiền do sự cố hệ thống")
        .Produces<CreateRefundRequestResponse>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound);

        // POST /api/payment/refund-requests/{id:guid}/owner-confirmation: Owner confirms refund figures
        group.MapPost("/{id:guid}/owner-confirmation", async (
            Guid id,
            HttpContext httpContext,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            var callerId = GetUserId(httpContext);
            if (callerId == Guid.Empty)
                return Results.Unauthorized();

            var callerRole = GetUserRole(httpContext);

            var command = new ConfirmRefundRequestByOwnerCommand(
                Id: id,
                CallerId: callerId,
                CallerRole: callerRole);

            var result = await sender.Send(command, cancellationToken);
            return result.IsSuccess
                ? Results.Ok(result.Value)
                : ToProblem(httpContext, result.Error!);
        })
        .RequireAuthorization(PaymentAuthorization.OwnerPolicy)
        .WithName("ConfirmRefundRequestByOwner")
        .WithSummary("Chủ bãi xác nhận số liệu yêu cầu hoàn tiền")
        .Produces<ConfirmRefundRequestByOwnerResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict)
        .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

        // POST /api/payment/refund-requests/{id:guid}/reject: Admin rejects refund request
        group.MapPost("/{id:guid}/reject", async (
            Guid id,
            RejectRefundRequestDto request,
            HttpContext httpContext,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            var adminId = GetUserId(httpContext);
            if (adminId == Guid.Empty)
                return Results.Unauthorized();

            var adminRole = GetUserRole(httpContext);

            var command = new RejectRefundRequestCommand(
                Id: id,
                AdminId: adminId,
                AdminRole: adminRole,
                Reason: request.Reason);

            var result = await sender.Send(command, cancellationToken);
            return result.IsSuccess
                ? Results.Ok(result.Value)
                : ToProblem(httpContext, result.Error!);
        })
        .RequireAuthorization(PaymentAuthorization.AdminPolicy)
        .WithName("RejectRefundRequest")
        .WithSummary("Quản trị viên từ chối yêu cầu hoàn tiền")
        .Produces<RejectRefundRequestResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict);

        // GET /api/payment/refund-requests: List refund requests scoped by role
        group.MapGet("/", async (
            [AsParameters] ListRefundRequestsParameters parameters,
            HttpContext httpContext,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            var callerId = GetUserId(httpContext);
            if (callerId == Guid.Empty)
                return Results.Unauthorized();

            var callerRole = GetUserRole(httpContext);

            var query = new ListRefundRequestsQuery(
                CallerId: callerId,
                CallerRole: callerRole,
                Status: parameters.Status,
                LotId: parameters.LotId,
                Page: parameters.Page,
                PageSize: parameters.PageSize);

            var result = await sender.Send(query, cancellationToken);
            return result.IsSuccess
                ? Results.Ok(result.Value)
                : ToProblem(httpContext, result.Error!);
        })
        .RequireAuthorization()
        .WithName("ListRefundRequests")
        .WithSummary("Danh sách yêu cầu hoàn tiền theo quyền hạn")
        .Produces<RefundRequestsPagedDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

        return app;
    }

    private static Guid GetUserId(HttpContext httpContext)
    {
        var sub = httpContext.User.FindFirst("sub")?.Value
            ?? httpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(sub, out var id) ? id : Guid.Empty;
    }

    private static string? GetUserRole(HttpContext httpContext)
    {
        return httpContext.User.FindFirst("role")?.Value
            ?? httpContext.User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;
    }

    private static IResult ToProblem(HttpContext httpContext, Error error)
    {
        var statusCode = error.Code switch
        {
            "payment.refund_request.wallet_not_found" or "payment.refund_request.not_found"
                => StatusCodes.Status404NotFound,
            "payment.customer_only" or "payment.forbidden_role" or "payment.creator_cannot_review" or "payment.forbidden_lot_access"
                => StatusCodes.Status403Forbidden,
            "payment.owner_not_confirmed" or "payment.invalid_status" or "payment.already_confirmed" or "payment.already_reviewed" or "payment.concurrency_conflict"
                => StatusCodes.Status409Conflict,
            "payment.parking_service_unavailable"
                => StatusCodes.Status503ServiceUnavailable,
            _ => StatusCodes.Status400BadRequest
        };

        return BusinessProblem.Create(httpContext, statusCode, error);
    }
}
