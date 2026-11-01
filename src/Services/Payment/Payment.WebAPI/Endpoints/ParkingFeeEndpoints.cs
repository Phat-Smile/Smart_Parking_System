using MediatR;
using ParkingSystem.AspNetCore;
using ParkingSystem.BuildingBlocks.Results;
using ParkingSystem.Payment.Application.Common.Constants;
using ParkingSystem.Payment.Application.Features.ParkingSessions.Commands.PayParkingFeeByWallet;
using ParkingSystem.Payment.Application.Features.ParkingSessions.Queries.GetParkingSessionFee;
using ParkingSystem.Payment.WebAPI.Security;

namespace ParkingSystem.Payment.WebAPI.Endpoints;

// Yêu cầu trừ tiền ví trả tiền gửi xe
public sealed record PayParkingFeeByWalletRequest(long ExpectedAmountDue);

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
            .WithDescription("Báo giá tạm tính trước khi thanh toán và ra cổng.")
            .Produces<ParkingSessionFeeResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

        // Thanh toán phí đỗ xe bằng số dư ví
        app.MapPost("/api/payment/parking-sessions/{sessionId}/pay/wallet", async (
                string sessionId,
                PayParkingFeeByWalletRequest? body,
                HttpContext httpContext,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                // Chặn trình duyệt lưu lại kết quả thanh toán
                httpContext.Response.Headers.CacheControl = "no-store";

                // 1. Kiểm tra header chống trừ tiền lặp lại
                if (!httpContext.Request.Headers.TryGetValue("X-Idempotency-Key", out var rawKey) ||
                    string.IsNullOrWhiteSpace(rawKey) ||
                    !Guid.TryParse(rawKey, out _))
                {
                    return BusinessProblem.Create(
                        httpContext, StatusCodes.Status400BadRequest, PayParkingFeeErrors.IdempotencyKeyInvalid);
                }

                // 2. Kiểm tra định dạng mã phiên xe
                if (!Guid.TryParse(sessionId, out var sessionGuid) || sessionGuid == Guid.Empty)
                {
                    return BusinessProblem.Create(
                        httpContext, StatusCodes.Status400BadRequest, PayParkingFeeErrors.SessionIdInvalid);
                }

                // 3. Kiểm tra số tiền khách đồng ý trả
                if (body is null || body.ExpectedAmountDue <= 0)
                {
                    return BusinessProblem.Create(
                        httpContext, StatusCodes.Status400BadRequest, PayParkingFeeErrors.ExpectedAmountInvalid);
                }

                var callerAccountId = Guid.TryParse(
                    httpContext.User.FindFirst("sub")?.Value, out var id)
                    ? id : Guid.Empty;
                var callerRole = httpContext.User.FindFirst("role")?.Value
                    ?? string.Empty;

                var command = new PayParkingFeeByWalletCommand(
                    sessionGuid,
                    body.ExpectedAmountDue,
                    rawKey.ToString(),
                    callerAccountId,
                    callerRole);

                var result = await sender.Send(command, cancellationToken);

                if (result.IsSuccess)
                {
                    if (result.Value!.IsReplayed)
                    {
                        httpContext.Response.Headers["Idempotency-Replayed"] = "true";
                    }

                    return Results.Ok(result.Value);
                }

                return MapPayError(result.Error!, httpContext);
            })
            .RequireAuthorization(PaymentAuthorization.PaySessionFeePolicy)
            .WithTags("Thanh toán phí đỗ (Parking Fee)")
            .WithName("PayParkingFeeByWallet")
            .WithSummary("Thanh toán phí đỗ xe bằng số dư ví")
            .WithDescription("Khách hàng trừ tiền ví thanh toán phí đỗ xe để nâng thanh chắn cổng ra.")
            .Produces<PayParkingFeeByWalletResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

        return app;
    }

    // Ánh xạ lỗi khi thanh toán tiền gửi xe bằng ví sang ProblemDetails
    private static IResult MapPayError(Error error, HttpContext httpContext)
    {
        if (error.Code == PayParkingFeeErrors.RequestInProgress.Code)
        {
            httpContext.Response.Headers.RetryAfter = "2";
            return BusinessProblem.Create(httpContext, StatusCodes.Status409Conflict, error);
        }

        if (error.Code == PayParkingFeeErrors.WalletBusy.Code)
        {
            httpContext.Response.Headers.RetryAfter = "1";
            return BusinessProblem.Create(httpContext, StatusCodes.Status503ServiceUnavailable, error);
        }

        if (error.Code == PayParkingFeeErrors.ParkingServiceUnavailable.Code)
        {
            return BusinessProblem.Create(httpContext, StatusCodes.Status503ServiceUnavailable, error);
        }

        if (error.Code == PayParkingFeeErrors.RoleNotAllowed.Code ||
            error.Code == PayParkingFeeErrors.SessionAccessDenied.Code)
        {
            return BusinessProblem.Create(httpContext, StatusCodes.Status403Forbidden, error);
        }

        if (error.Code == PayParkingFeeErrors.SessionNotFound.Code ||
            error.Code == PayParkingFeeErrors.WalletNotFound.Code)
        {
            return BusinessProblem.Create(httpContext, StatusCodes.Status404NotFound, error);
        }

        if (error.Code == PayParkingFeeErrors.SessionNotActive.Code ||
            error.Code == PayParkingFeeErrors.SessionAlreadyPaid.Code ||
            error.Code == PayParkingFeeErrors.NoAmountDue.Code ||
            error.Code == PayParkingFeeErrors.FeeChanged.Code ||
            error.Code == PayParkingFeeErrors.IdempotencyKeyReused.Code)
        {
            return BusinessProblem.Create(httpContext, StatusCodes.Status409Conflict, error);
        }

        return BusinessProblem.Create(httpContext, StatusCodes.Status400BadRequest, error);
    }

    // Ánh xạ lỗi nghiệp vụ xem phí sang mã trạng thái HTTP tương ứng
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

        return StatusCodes.Status500InternalServerError;
    }
}
