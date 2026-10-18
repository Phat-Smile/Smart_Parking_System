using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using ParkingSystem.BuildingBlocks.Identity;
using ParkingSystem.Payment.Application.Common.Constants;
using ParkingSystem.Payment.Application.Features.Wallets.Commands.CreateWalletTopup;
using ParkingSystem.Payment.Application.Features.Wallets.Queries.GetMyWallet;
using ParkingSystem.Payment.Application.Features.Wallets.Queries.GetWalletTransactions;
using ParkingSystem.Payment.WebAPI.Common;
using ParkingSystem.Payment.WebAPI.Models;

namespace ParkingSystem.Payment.WebAPI.Endpoints;

public static class WalletEndpoints
{
    public static IEndpointRouteBuilder MapWalletEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/payment/wallets")
            .RequireAuthorization();

        // Tra cứu số dư ví của người dùng hiện tại
        group.MapGet("/me", async (
            ICurrentUser currentUser,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            if (!Guid.TryParse(currentUser.SubjectId, out var userId))
            {
                return Results.Unauthorized();
            }

            var query = new GetMyWalletQuery(userId);
            var result = await sender.Send(query, cancellationToken);
            return result.IsSuccess
                ? Results.Ok(ApiResponse<WalletBalanceDto>.Success(result.Value!, "Get wallet successfully", StatusCodes.Status200OK))
                : Results.BadRequest(ApiResponse<WalletBalanceDto>.Failure(result.Error!.Message, StatusCodes.Status400BadRequest));
        })
        .WithName("GetMyWallet")
        .WithSummary("Tra cứu số dư ví của tôi")
        .Produces<ApiResponse<WalletBalanceDto>>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized);

        // Yêu cầu nạp tiền vào ví qua VNPay
        group.MapPost("/top-up", async (
            HttpContext httpContext,
            ICurrentUser currentUser,
            CreateWalletTopupRequest request,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            if (!Guid.TryParse(currentUser.SubjectId, out var userId))
            {
                return Results.Unauthorized();
            }

            var idempotencyKey = httpContext.Request.Headers["Idempotency-Key"].ToString();
            var clientIp = httpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";

            var command = new CreateWalletTopupCommand(
                UserId: userId,
                Amount: request.Amount,
                IdempotencyKey: idempotencyKey,
                ClientIp: clientIp);

            var result = await sender.Send(command, cancellationToken);
            if (!result.IsSuccess)
            {
                var statusCode = result.Error!.Code.Contains("conflict", StringComparison.OrdinalIgnoreCase)
                    ? StatusCodes.Status409Conflict
                    : StatusCodes.Status400BadRequest;

                return Results.Json(
                    ApiResponse<object>.Failure(result.Error.Message, statusCode),
                    statusCode: statusCode);
            }

            var responseMessage = result.Value!.IsReplay
                ? "Top-up retrieved successfully"
                : "Create top-up successfully";

            var status = result.Value.IsReplay
                ? StatusCodes.Status200OK
                : StatusCodes.Status201Created;

            return Results.Json(
                ApiResponse<WalletTopupResultDto>.Success(result.Value, responseMessage, status),
                statusCode: status);
        })
        .WithName("CreateWalletTopup")
        .WithSummary("Yêu cầu nạp tiền vào ví qua cổng VNPay")
        .Produces<ApiResponse<WalletTopupResultDto>>(StatusCodes.Status201Created)
        .Produces<ApiResponse<WalletTopupResultDto>>(StatusCodes.Status200OK)
        .Produces<ApiResponse<object>>(StatusCodes.Status400BadRequest)
        .Produces<ApiResponse<object>>(StatusCodes.Status409Conflict)
        .Produces(StatusCodes.Status401Unauthorized);

        // Xem lịch sử giao dịch ví (US-WAL-003)
        group.MapGet("/transactions", async (
            string? page,
            string? pageSize,
            string? type,
            string? fromUtc,
            string? toUtc,
            HttpContext httpContext,
            ICurrentUser currentUser,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            if (!Guid.TryParse(currentUser.SubjectId, out var userId))
            {
                return Results.Unauthorized();
            }

            var role = httpContext.User.FindFirst("role")?.Value;

            var query = new GetWalletTransactionsQuery(
                UserId: userId,
                Role: role,
                RawPage: page,
                RawPageSize: pageSize,
                RawType: type,
                RawFromUtc: fromUtc,
                RawToUtc: toUtc
            );

            var result = await sender.Send(query, cancellationToken);
            if (!result.IsSuccess)
            {
                var statusCode = result.Error!.Code == PaymentErrors.Wallet.CustomerOnly.Code
                    ? StatusCodes.Status403Forbidden
                    : StatusCodes.Status400BadRequest;

                return Results.Json(
                    ApiResponse<object>.Failure(result.Error.Message, statusCode),
                    statusCode: statusCode);
            }

            return Results.Ok(ApiResponse<WalletTransactionsPagedDto>.Success(
                result.Value!,
                PaymentResponseMessages.GetWalletTransactionsSuccessfully,
                StatusCodes.Status200OK));
        })
        .WithName("GetWalletTransactions")
        .WithSummary("Xem lịch sử giao dịch ví của khách hàng")
        .Produces<ApiResponse<WalletTransactionsPagedDto>>(StatusCodes.Status200OK)
        .Produces<ApiResponse<object>>(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces<ApiResponse<object>>(StatusCodes.Status403Forbidden);

        return app;
    }
}
