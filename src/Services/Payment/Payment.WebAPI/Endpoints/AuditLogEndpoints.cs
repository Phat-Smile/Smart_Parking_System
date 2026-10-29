using MediatR;
using ParkingSystem.AspNetCore;
using ParkingSystem.Payment.Application.Features.AuditLogs.Queries.GetAuditLogs;
using ParkingSystem.Payment.WebAPI.Security;

namespace ParkingSystem.Payment.WebAPI.Endpoints;

internal static class AuditLogEndpoints
{
    public sealed record GetAuditLogsRequest(
        int Page = 1,
        int PageSize = 20,
        string? ActionType = null,
        Guid? ParkingLotId = null,
        Guid? ActorId = null,
        DateTime? FromUtc = null,
        DateTime? ToUtc = null);

    public static IEndpointRouteBuilder MapAuditLogEndpoints(this IEndpointRouteBuilder app)
    {
        // US-ADMIN-005: Xem nhật ký kiểm toán hệ thống (chỉ dành cho SystemAdmin)
        app.MapGet("/api/payment/admin/audit-logs", async (
                [AsParameters] GetAuditLogsRequest request,
                HttpContext httpContext,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var query = new GetAuditLogsQuery(
                    request.Page,
                    request.PageSize,
                    request.ActionType,
                    request.ParkingLotId,
                    request.ActorId,
                    request.FromUtc,
                    request.ToUtc);

                var result = await sender.Send(query, cancellationToken);

                return result.IsSuccess
                    ? Results.Ok(result.Value)
                    : BusinessProblem.Create(httpContext, StatusCodes.Status400BadRequest, result.Error!);
            })
            .RequireAuthorization(PaymentAuthorization.AdminPolicy)
            .WithTags("Quản trị hệ thống (Administration)")
            .WithName("GetAuditLogs")
            .WithSummary("Xem nhật ký kiểm toán hệ thống")
            .WithDescription("Dành riêng cho Quản trị viên hệ thống để tra cứu, lọc và phân trang nhật ký kiểm toán.")
            .Produces<AuditLogsPagedDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        return app;
    }
}
