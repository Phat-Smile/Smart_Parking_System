using MediatR;
using ParkingSystem.BuildingBlocks.Results;
using ParkingSystem.Payment.Application.Common.Constants;
using ParkingSystem.Payment.Application.Features.ParkingSessions.Queries.GetParkingSessionFee;
using ParkingSystem.Payment.WebAPI.Common;
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

                if (result.IsSuccess)
                {
                    return Results.Ok(
                        ApiResponse<ParkingSessionFeeResponse>.Success(
                            result.Value,
                            ParkingFeeResponseMessages.CalculateFeeSuccessfully,
                            StatusCodes.Status200OK));
                }

                return MapError(result.Error!);
            })
            .RequireAuthorization(PaymentAuthorization.ViewSessionFeePolicy)
            .WithTags("Thanh toán phí đỗ (Parking Fee)")
            .WithName("GetParkingSessionFee")
            .WithSummary("Xem trước phí đỗ xe của phiên đỗ")
            .WithDescription(
                "Báo giá tạm tính trước khi thanh toán và ra cổng.")
            .Produces<ApiResponse<ParkingSessionFeeResponse>>(
                StatusCodes.Status200OK)
            .Produces<ApiResponse<object>>(StatusCodes.Status400BadRequest)
            .Produces<ApiResponse<object>>(StatusCodes.Status401Unauthorized)
            .Produces<ApiResponse<object>>(StatusCodes.Status403Forbidden)
            .Produces<ApiResponse<object>>(StatusCodes.Status404NotFound)
            .Produces<ApiResponse<object>>(StatusCodes.Status409Conflict)
            .Produces<ApiResponse<object>>(StatusCodes.Status503ServiceUnavailable);

        return app;
    }

    // Ánh xạ lỗi nghiệp vụ sang mã trạng thái mạng tương ứng
    private static IResult MapError(Error error)
    {
        if (error.Code == ParkingFeeErrors.SessionIdInvalid.Code)
            return Results.BadRequest(
                ApiResponse<object>.Failure(
                    error.Message, StatusCodes.Status400BadRequest));

        if (error.Code == ParkingFeeErrors.SessionNotFound.Code)
            return Results.NotFound(
                ApiResponse<object>.Failure(
                    error.Message, StatusCodes.Status404NotFound));

        if (error.Code == ParkingFeeErrors.SessionAccessDenied.Code)
            return Results.Json(
                ApiResponse<object>.Failure(
                    error.Message, StatusCodes.Status403Forbidden),
                statusCode: StatusCodes.Status403Forbidden);

        if (error.Code == ParkingFeeErrors.SessionNotActive.Code)
            return Results.Conflict(
                ApiResponse<object>.Failure(
                    error.Message, StatusCodes.Status409Conflict));

        if (error.Code == ParkingFeeErrors.ParkingServiceUnavailable.Code)
            return Results.Json(
                ApiResponse<object>.Failure(
                    error.Message, StatusCodes.Status503ServiceUnavailable),
                statusCode: StatusCodes.Status503ServiceUnavailable);

        // Trường hợp lỗi khác ngoài dự kiến
        return Results.Json(
            ApiResponse<object>.Failure(
                error.Message, StatusCodes.Status500InternalServerError),
            statusCode: StatusCodes.Status500InternalServerError);
    }
}
