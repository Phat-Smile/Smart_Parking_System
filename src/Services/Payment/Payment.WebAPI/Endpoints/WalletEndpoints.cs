using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using ParkingSystem.AspNetCore;
using ParkingSystem.BuildingBlocks.Identity;
using ParkingSystem.Payment.Application.Common.Constants;
using ParkingSystem.Payment.Application.Features.Wallets.Commands.CreateWalletTopup;
using ParkingSystem.Payment.Application.Features.Wallets.Queries.GetMyWallet;
using ParkingSystem.Payment.Application.Features.Wallets.Queries.GetWalletTopupStatus;
using ParkingSystem.Payment.Application.Features.Wallets.Queries.GetWalletTransactions;
using ParkingSystem.Payment.WebAPI.Models;
using ParkingSystem.Payment.WebAPI.Security;

namespace ParkingSystem.Payment.WebAPI.Endpoints;

// Success returns the DTO itself; a business error returns ProblemDetails with errorCode (docs/api/Payment-Wallet-Contract.md).
public static class WalletEndpoints
{
    public static IEndpointRouteBuilder MapWalletEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/payment/wallets")
            .RequireAuthorization();

        // Tra cứu số dư ví của người dùng hiện tại
        group.MapGet("/me", async (
            HttpContext httpContext,
            ICurrentUser currentUser,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            if (!Guid.TryParse(currentUser.SubjectId, out var userId))
            {
                return Results.Unauthorized();
            }

            var result = await sender.Send(new GetMyWalletQuery(userId), cancellationToken);
            return result.IsSuccess
                ? Results.Ok(result.Value)
                : BusinessProblem.Create(httpContext, StatusCodes.Status400BadRequest, result.Error!);
        })
        .WithName("GetMyWallet")
        .WithSummary("Tra cứu số dư ví của tôi")
        .Produces<WalletBalanceDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized);

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
                // The same Idempotency-Key cannot start a different or an expired top-up.
                var statusCode = result.Error!.Code is "payment.idempotency_key_reused" or "payment.topup_expired"
                    ? StatusCodes.Status409Conflict
                    : StatusCodes.Status400BadRequest;
                return BusinessProblem.Create(httpContext, statusCode, result.Error);
            }

            // A replay of the same Idempotency-Key returns the first top-up with 200.
            return Results.Json(
                result.Value,
                statusCode: result.Value!.IsReplay ? StatusCodes.Status200OK : StatusCodes.Status201Created);
        })
        .WithName("CreateWalletTopup")
        .WithSummary("Yêu cầu nạp tiền vào ví qua cổng VNPay")
        .Produces<WalletTopupResultDto>(StatusCodes.Status201Created)
        .Produces<WalletTopupResultDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status409Conflict)
        .ProducesProblem(StatusCodes.Status401Unauthorized);

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
                return BusinessProblem.Create(httpContext, statusCode, result.Error);
            }

            return Results.Ok(result.Value);
        })
        .WithName("GetWalletTransactions")
        .WithSummary("Xem lịch sử giao dịch ví của khách hàng")
        .Produces<WalletTransactionsPagedDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden);

        // Tra cuu trang thai mot giao dich nap tien vao vi
        group.MapGet("/top-up/{topupId}", async (
            string topupId,
            HttpContext httpContext,
            ICurrentUser currentUser,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            if (!Guid.TryParse(currentUser.SubjectId, out var userId))
            {
                return Results.Unauthorized();
            }

            var result = await sender.Send(new GetWalletTopupStatusQuery(userId, topupId), cancellationToken);
            if (!result.IsSuccess)
            {
                // Another user's top-up is reported as not found too.
                var statusCode = result.Error!.Code.Contains("not_found", StringComparison.OrdinalIgnoreCase)
                    ? StatusCodes.Status404NotFound
                    : StatusCodes.Status400BadRequest;
                return BusinessProblem.Create(httpContext, statusCode, result.Error);
            }

            return Results.Ok(result.Value);
        })
        .WithName("GetWalletTopupStatus")
        .WithSummary("Tra cứu trạng thái một giao dịch nạp tiền vào ví")
        .Produces<WalletTopupStatusDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status401Unauthorized);

        return app;
    }
}
