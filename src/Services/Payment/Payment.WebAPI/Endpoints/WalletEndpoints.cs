using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using ParkingSystem.BuildingBlocks.Identity;
using ParkingSystem.Payment.Application.Features.Wallets.Queries.GetMyWallet;
using ParkingSystem.Payment.WebAPI.Common;

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

        return app;
    }
}
