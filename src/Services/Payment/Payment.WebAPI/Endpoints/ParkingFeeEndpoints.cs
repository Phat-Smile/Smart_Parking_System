using MediatR;
using ParkingSystem.AspNetCore;
using ParkingSystem.BuildingBlocks.Results;
using ParkingSystem.Payment.Application.Common.Constants;
using ParkingSystem.Payment.Application.Features.ParkingFeePayments.Queries.GetParkingFeePaymentStatus;
using ParkingSystem.Payment.Application.Features.ParkingSessions.Commands.ConfirmCashParkingFeePayment;
using ParkingSystem.Payment.Application.Features.ParkingSessions.Commands.CreateVnPayParkingFeePayment;
using ParkingSystem.Payment.Application.Features.ParkingSessions.Commands.PayParkingFeeByWallet;
using ParkingSystem.Payment.Application.Features.ParkingSessions.Queries.GetParkingFeeReceipt;
using ParkingSystem.Payment.Application.Features.ParkingSessions.Queries.GetParkingSessionFee;
using ParkingSystem.Payment.WebAPI.Security;

namespace ParkingSystem.Payment.WebAPI.Endpoints;

// Yêu cầu trừ tiền ví trả tiền gửi xe
public sealed record PayParkingFeeByWalletRequest(long ExpectedAmountDue);

// Yêu cầu thanh toán tiền gửi xe qua VNPay
public sealed record PayParkingFeeByVnPayRequest(long ExpectedAmountDue);

// Yêu cầu xác nhận thu tiền mặt phí đỗ xe
public sealed record PayParkingFeeByCashRequest(long ExpectedAmountDue, string? Reason = null);

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

                // 3. Kiểm tra số tiền khách đồng ý trả (chấp nhận 0đ khi đã cọc đủ)
                if (body is null || body.ExpectedAmountDue < 0)
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

        // Tạo yêu cầu thanh toán tiền gửi xe qua VNPay
        app.MapPost("/api/payment/parking-sessions/{sessionId}/pay/vnpay", async (
                string sessionId,
                PayParkingFeeByVnPayRequest? body,
                HttpContext httpContext,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                httpContext.Response.Headers.CacheControl = "no-store";

                // 1. Kiểm tra khóa chống lặp yêu cầu
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

                // 3. Kiểm tra số tiền khách đồng ý trả (VNPay không nhận 0đ)
                if (body is null || body.ExpectedAmountDue <= 0)
                {
                    return BusinessProblem.Create(
                        httpContext, StatusCodes.Status400BadRequest, PayParkingFeeErrors.ExpectedAmountInvalid);
                }

                var callerAccountId = Guid.TryParse(
                    httpContext.User.FindFirst("sub")?.Value, out var id)
                    ? id : (Guid?)null;
                var callerRole = httpContext.User.FindFirst("role")?.Value
                    ?? string.Empty;
                var staffLotId = httpContext.User.FindFirst("parking_lot_id")?.Value;
                var clientIp = httpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";

                var command = new CreateVnPayParkingFeePaymentCommand(
                    SessionId: sessionGuid,
                    ExpectedAmountDue: body.ExpectedAmountDue,
                    IdempotencyKey: rawKey.ToString(),
                    CallerAccountId: callerAccountId,
                    CallerRole: callerRole,
                    StaffLotId: staffLotId,
                    ClientIp: clientIp);

                var result = await sender.Send(command, cancellationToken);

                if (result.IsSuccess)
                {
                    if (result.Value!.IsReplayed)
                    {
                        httpContext.Response.Headers["Idempotency-Replayed"] = "true";
                    }

                    var statusCode = result.Value.IsReused ? StatusCodes.Status200OK : StatusCodes.Status201Created;
                    return Results.Json(result.Value, statusCode: statusCode);
                }

                return MapPayVnPayError(result.Error!, httpContext);
            })
            .RequireAuthorization(PaymentAuthorization.PayVnPayPolicy)
            .WithTags("Thanh toán phí đỗ (Parking Fee)")
            .WithName("CreateVnPayParkingFeePayment")
            .WithSummary("Tạo yêu cầu thanh toán phí đỗ xe qua VNPay")
            .WithDescription("Sinh link thanh toán VNPay có chữ ký số để người dùng quét mã.")
            .Produces<CreateVnPayParkingFeePaymentResponse>(StatusCodes.Status201Created)
            .Produces<CreateVnPayParkingFeePaymentResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

        // Xác nhận thu tiền mặt phí đỗ xe tại quầy
        app.MapPost("/api/payment/parking-sessions/{sessionId}/pay/cash", async (
                string sessionId,
                PayParkingFeeByCashRequest? body,
                HttpContext httpContext,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                httpContext.Response.Headers.CacheControl = "no-store";

                // 1. Kiểm tra header chống ghi nhận tiền mặt lặp lại
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

                // 3. Kiểm tra số tiền nhân viên xác nhận thu (chấp nhận 0đ khi đã cọc đủ)
                if (body is null || body.ExpectedAmountDue < 0)
                {
                    return BusinessProblem.Create(
                        httpContext, StatusCodes.Status400BadRequest, PayParkingFeeErrors.ExpectedAmountInvalid);
                }

                // 4. Kiểm tra độ dài lý do ghi chú
                if (body.Reason is not null && body.Reason.Length > 500)
                {
                    return BusinessProblem.Create(
                        httpContext, StatusCodes.Status400BadRequest, PayParkingFeeErrors.ReasonTooLong);
                }

                var callerAccountId = Guid.TryParse(
                    httpContext.User.FindFirst("sub")?.Value, out var id)
                    ? id : Guid.Empty;
                var callerRole = httpContext.User.FindFirst("role")?.Value
                    ?? string.Empty;

                var command = new ConfirmCashParkingFeePaymentCommand(
                    sessionGuid,
                    callerAccountId,
                    callerRole,
                    rawKey.ToString(),
                    body.ExpectedAmountDue,
                    body.Reason);

                var result = await sender.Send(command, cancellationToken);

                if (result.IsSuccess)
                {
                    return Results.Ok(result.Value);
                }

                return MapPayError(result.Error!, httpContext);
            })
            .RequireAuthorization(PaymentAuthorization.ConfirmCashPaymentPolicy)
            .WithTags("Thanh toán phí đỗ (Parking Fee)")
            .WithName("ConfirmCashParkingFeePayment")
            .WithSummary("Nhân viên xác nhận thu tiền mặt phí đỗ xe")
            .WithDescription("Xác nhận tiền mặt tại bãi, ghi sổ thanh toán, lập audit log và phát sự kiện mở cổng ra.")
            .Produces<ConfirmCashParkingFeePaymentResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

        // Tra cứu trạng thái hóa đơn thanh toán tiền gửi xe
        app.MapGet("/api/payment/parking-fee-payments/{transactionId}", async (
                string transactionId,
                HttpContext httpContext,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                httpContext.Response.Headers.CacheControl = "no-store";

                if (!Guid.TryParse(transactionId, out var transactionGuid) || transactionGuid == Guid.Empty)
                {
                    return BusinessProblem.Create(
                        httpContext, StatusCodes.Status400BadRequest, ParkingFeePaymentErrors.TransactionIdInvalid);
                }

                var query = new GetParkingFeePaymentStatusQuery(transactionGuid);
                var result = await sender.Send(query, cancellationToken);

                if (result.IsSuccess)
                {
                    return Results.Ok(result.Value);
                }

                if (result.Error!.Code == ParkingFeePaymentErrors.TransactionNotFound.Code)
                {
                    return BusinessProblem.Create(httpContext, StatusCodes.Status404NotFound, result.Error);
                }

                return BusinessProblem.Create(httpContext, StatusCodes.Status400BadRequest, result.Error);
            })
            .RequireAuthorization(PaymentAuthorization.ViewFeePaymentPolicy)
            .WithTags("Thanh toán phí đỗ (Parking Fee)")
            .WithName("GetParkingFeePaymentStatus")
            .WithSummary("Tra cứu trạng thái thanh toán tiền gửi xe")
            .WithDescription("Kiểm tra trạng thái hóa đơn thanh toán sau khi quét mã VNPay.")
            .Produces<GetParkingFeePaymentStatusResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

        // Lấy hóa đơn điện tử quyết toán theo mã phiên đỗ xe (US-PAY-003)
        app.MapGet("/api/payment/parking-sessions/{sessionId}/receipt", async (
                string sessionId,
                HttpContext httpContext,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                httpContext.Response.Headers.CacheControl = "no-store";

                if (!Guid.TryParse(sessionId, out var sessionGuid) || sessionGuid == Guid.Empty)
                {
                    return BusinessProblem.Create(
                        httpContext, StatusCodes.Status400BadRequest, PayParkingFeeErrors.SessionIdInvalid);
                }

                var callerAccountId = Guid.TryParse(
                    httpContext.User.FindFirst("sub")?.Value, out var id)
                    ? id : (Guid?)null;
                var callerRole = httpContext.User.FindFirst("role")?.Value
                    ?? string.Empty;

                var query = new GetParkingFeeReceiptQuery(
                    sessionGuid, callerAccountId, callerRole);
                var result = await sender.Send(query, cancellationToken);

                if (result.IsSuccess)
                {
                    return Results.Ok(result.Value);
                }

                if (result.Error!.Code == ParkingFeePaymentErrors.TransactionNotFound.Code)
                {
                    return BusinessProblem.Create(httpContext, StatusCodes.Status404NotFound, result.Error);
                }

                if (result.Error!.Code == PayParkingFeeErrors.SessionAccessDenied.Code)
                {
                    return BusinessProblem.Create(httpContext, StatusCodes.Status403Forbidden, result.Error);
                }

                return BusinessProblem.Create(httpContext, StatusCodes.Status400BadRequest, result.Error);
            })
            .RequireAuthorization(PaymentAuthorization.ViewFeePaymentPolicy)
            .WithTags("Thanh toán phí đỗ (Parking Fee)")
            .WithName("GetParkingSessionReceipt")
            .WithSummary("Hóa đơn điện tử quyết toán phí đỗ xe theo phiên")
            .WithDescription("Lấy chi tiết bảng kê hóa đơn điện tử quyết toán sau khi thanh toán thành công (US-PAY-003).")
            .Produces<ParkingFeeReceiptResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

        // Lấy hóa đơn điện tử quyết toán theo mã giao dịch thanh toán (US-PAY-003)
        app.MapGet("/api/payment/parking-fee-payments/{paymentId}/receipt", async (
                string paymentId,
                HttpContext httpContext,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                httpContext.Response.Headers.CacheControl = "no-store";

                if (!Guid.TryParse(paymentId, out var paymentGuid) || paymentGuid == Guid.Empty)
                {
                    return BusinessProblem.Create(
                        httpContext, StatusCodes.Status400BadRequest, ParkingFeePaymentErrors.TransactionIdInvalid);
                }

                var callerAccountId = Guid.TryParse(
                    httpContext.User.FindFirst("sub")?.Value, out var id)
                    ? id : (Guid?)null;
                var callerRole = httpContext.User.FindFirst("role")?.Value
                    ?? string.Empty;

                var query = new GetParkingFeeReceiptQuery(
                    paymentGuid, callerAccountId, callerRole);
                var result = await sender.Send(query, cancellationToken);

                if (result.IsSuccess)
                {
                    return Results.Ok(result.Value);
                }

                if (result.Error!.Code == ParkingFeePaymentErrors.TransactionNotFound.Code)
                {
                    return BusinessProblem.Create(httpContext, StatusCodes.Status404NotFound, result.Error);
                }

                if (result.Error!.Code == PayParkingFeeErrors.SessionAccessDenied.Code)
                {
                    return BusinessProblem.Create(httpContext, StatusCodes.Status403Forbidden, result.Error);
                }

                return BusinessProblem.Create(httpContext, StatusCodes.Status400BadRequest, result.Error);
            })
            .RequireAuthorization(PaymentAuthorization.ViewFeePaymentPolicy)
            .WithTags("Thanh toán phí đỗ (Parking Fee)")
            .WithName("GetParkingPaymentReceipt")
            .WithSummary("Hóa đơn điện tử quyết toán phí đỗ xe theo mã thanh toán")
            .WithDescription("Lấy chi tiết bảng kê hóa đơn điện tử quyết toán theo mã giao dịch thanh toán (US-PAY-003).")
            .Produces<ParkingFeeReceiptResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

        return app;
    }

    // Ánh xạ lỗi khi tạo thanh toán VNPay sang ProblemDetails
    private static IResult MapPayVnPayError(Error error, HttpContext httpContext)
    {
        if (error.Code == PayParkingFeeErrors.RequestInProgress.Code)
        {
            httpContext.Response.Headers.RetryAfter = "2";
            return BusinessProblem.Create(httpContext, StatusCodes.Status409Conflict, error);
        }

        if (error.Code == PayParkingFeeErrors.ParkingServiceUnavailable.Code)
        {
            return BusinessProblem.Create(httpContext, StatusCodes.Status503ServiceUnavailable, error);
        }

        if (error.Code == PayParkingFeeErrors.RoleNotAllowedVNPay.Code ||
            error.Code == PayParkingFeeErrors.SessionAccessDenied.Code)
        {
            return BusinessProblem.Create(httpContext, StatusCodes.Status403Forbidden, error);
        }

        if (error.Code == PayParkingFeeErrors.SessionNotFound.Code)
        {
            return BusinessProblem.Create(httpContext, StatusCodes.Status404NotFound, error);
        }

        if (error.Code == PayParkingFeeErrors.SessionNotActive.Code ||
            error.Code == PayParkingFeeErrors.SessionAlreadyPaid.Code ||
            error.Code == PayParkingFeeErrors.NoAmountDue.Code ||
            error.Code == PayParkingFeeErrors.FeeChanged.Code ||
            error.Code == PayParkingFeeErrors.VnPayAmountNotSupported.Code ||
            error.Code == PayParkingFeeErrors.IdempotencyKeyReused.Code)
        {
            return BusinessProblem.Create(httpContext, StatusCodes.Status409Conflict, error);
        }

        return BusinessProblem.Create(httpContext, StatusCodes.Status400BadRequest, error);
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
            error.Code == PayParkingFeeErrors.RoleNotAllowedCash.Code ||
            error.Code == PayParkingFeeErrors.SessionAccessDenied.Code ||
            error.Code == PayParkingFeeErrors.LotAccessDenied.Code)
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
            error.Code == PayParkingFeeErrors.PaymentAlreadyInProgress.Code ||
            error.Code == PayParkingFeeErrors.NoAmountDue.Code ||
            error.Code == PayParkingFeeErrors.FeeChanged.Code ||
            error.Code == PayParkingFeeErrors.IdempotencyKeyReused.Code)
        {
            return BusinessProblem.Create(httpContext, StatusCodes.Status409Conflict, error);
        }

        if (error.Code == PayParkingFeeErrors.ReasonTooLong.Code)
        {
            return BusinessProblem.Create(httpContext, StatusCodes.Status400BadRequest, error);
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
