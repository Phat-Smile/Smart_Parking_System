using MediatR;
using ParkingSystem.AspNetCore;
using ParkingSystem.BuildingBlocks.Results;
using ParkingSystem.Payment.Application.Common.Constants;
using ParkingSystem.Payment.Application.Features.ParkingSessions.Queries.GetParkingSessionFee;
using ParkingSystem.Payment.WebAPI.Security;

namespace ParkingSystem.Payment.WebAPI.Endpoints;

internal static class ParkingFeeEndpoints
{
    public static IEndpointRouteBuilder MapParkingFeeEndpoints(
        this IEndpointRouteBuilder app)
    {
        // Xem trước phí đỗ xe trước khi thanh toán và ra cổng
        app.MapGet("/api/payment/parking-sessions/{sessionId}/fee", async (
                string sessionId,
                HttpContext httpContext,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                // Chặn trình duyệt lưu lại thông tin tính tiền
                httpContext.Response.Headers.CacheControl = "no-store";

                var callerAccountId = Guid.TryParse(
                    httpContext.User.FindFirst("sub")?.Value, out var id)
                    ? id : (Guid?)null;
                var callerRole = httpContext.User.FindFirst("role")?.Value
                    ?? string.Empty;

                var query = new GetParkingSessionFeeQuery(
                    sessionId, callerAccountId, callerRole);
                var result = await sender.Send(query, cancellationToken);

                return result.IsSuccess
                    ? Results.Ok(result.Value)
                    : BusinessProblem.Create(httpContext, StatusFor(result.Error!), result.Error!);
            })
            .RequireAuthorization(PaymentAuthorization.ViewSessionFeePolicy)
            .WithTags("Thanh toán phí đỗ (Parking Fee)")
            .WithName("GetParkingSessionFee")
            .WithSummary("Xem trước phí đỗ xe của phiên đỗ")
            .WithDescription(
                "Báo giá tạm tính trước khi thanh toán và ra cổng.")
            .Produces<ParkingSessionFeeResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

        return app;
    }

    // Ánh xạ lỗi nghiệp vụ sang mã trạng thái HTTP tương ứng
    private static int StatusFor(Error error)
    {
        if (error.Code == ParkingFeeErrors.SessionIdInvalid.Code)
            return StatusCodes.Status400BadRequest;
        if (error.Code == ParkingFeeErrors.SessionNotFound.Code)
            return StatusCodes.Status404NotFound;
        if (error.Code == ParkingFeeErrors.SessionAccessDenied.Code)
            return StatusCodes.Status403Forbidden;
        if (error.Code == ParkingFeeErrors.SessionNotActive.Code)
            return StatusCodes.Status409Conflict;
        if (error.Code == ParkingFeeErrors.ParkingServiceUnavailable.Code)
            return StatusCodes.Status503ServiceUnavailable;

        // Trường hợp lỗi khác ngoài dự kiến
        return StatusCodes.Status500InternalServerError;
    }
}
