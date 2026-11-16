using MediatR;
using ParkingSystem.Parking.Application.ParkingLots.GetAuditLogs;
using ParkingSystem.Parking.WebAPI.Security;

namespace ParkingSystem.Parking.WebAPI.Endpoints;

internal static class ParkingAuditLogEndpoints
{
    public sealed record GetParkingAuditLogsRequest(
        int Page = 1,
        int PageSize = 20,
        string? ActionType = null,
        Guid? ParkingLotId = null,
        Guid? ActorId = null,
        DateTime? FromUtc = null,
        DateTime? ToUtc = null);

    public static IEndpointRouteBuilder MapParkingAuditLogEndpoints(this IEndpointRouteBuilder app)
    {
        // US-ADMIN-005 / NFR-16: Xem nhật ký kiểm toán hệ thống Parking (chỉ dành cho SystemAdmin có MFA)
        app.MapGet("/api/parking/admin/audit-logs", async (
                [AsParameters] GetParkingAuditLogsRequest request,
                HttpContext httpContext,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var query = new GetParkingAuditLogsQuery(
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
                    : Results.BadRequest(new { errorCode = result.Error!.Code, detail = result.Error.Message });
            })
            .RequireAuthorization(ParkingAuthorization.SystemAdminPolicy)
            .WithTags("4. Quản trị hệ thống (Administration)")
            .WithName("GetParkingAuditLogs")
            .WithSummary("Xem nhật ký kiểm toán Parking")
            .WithDescription("Dành riêng cho Quản trị viên hệ thống để tra cứu, lọc và phân trang nhật ký kiểm toán Parking.")
            .Produces<AuditLogsPagedDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        return app;
    }
}
