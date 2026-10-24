using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using ParkingSystem.BuildingBlocks.Identity;
using ParkingSystem.Parking.Application.Features.DepositPolicy.Commands.ConfigureDepositPolicy;
using ParkingSystem.Parking.Application.Features.DepositPolicy.Queries.GetDepositPolicy;
using ParkingSystem.Parking.WebAPI.DTOs;
using ParkingSystem.Parking.WebAPI.Security;

namespace ParkingSystem.Parking.WebAPI.Endpoints;

public static class DepositPolicyEndpoints
{
    public static IEndpointRouteBuilder MapDepositPolicyEndpoints(this IEndpointRouteBuilder app)
    {
        // PUT routes (both standard /api/parking/lots/... and /api/v1/parking-lots/...)
        MapPutEndpoints(app, "/api/parking/lots/{lotId:guid}/deposit-policy");
        MapPutEndpoints(app, "/api/v1/parking-lots/{lotId:guid}/deposit-policy");

        // GET routes (both standard /api/parking/lots/... and /api/v1/parking-lots/...)
        MapGetEndpoints(app, "/api/parking/lots/{lotId:guid}/deposit-policy");
        MapGetEndpoints(app, "/api/v1/parking-lots/{lotId:guid}/deposit-policy");

        return app;
    }

    private static void MapPutEndpoints(IEndpointRouteBuilder app, string pattern)
    {
        app.MapPut(pattern, async (
            Guid lotId,
            ConfigureDepositPolicyRequest request,
            ICurrentUser currentUser,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            if (!Guid.TryParse(currentUser.SubjectId, out var currentUserId) || currentUserId == Guid.Empty)
            {
                return Results.Unauthorized();
            }

            var command = new ConfigureDepositPolicyCommand(lotId, request.HoldDepositVnd, currentUserId);
            var result = await sender.Send(command, cancellationToken);

            if (!result.IsSuccess)
            {
                var errorCode = result.Error!.Code;
                var statusCode = errorCode.Contains("not_found", StringComparison.OrdinalIgnoreCase)
                    ? StatusCodes.Status404NotFound
                    : errorCode.Contains("owner", StringComparison.OrdinalIgnoreCase) || errorCode.Contains("forbidden", StringComparison.OrdinalIgnoreCase)
                        ? StatusCodes.Status403Forbidden
                        : StatusCodes.Status400BadRequest;

                return Results.Json(
                    new { result = (object?)null, isSuccess = false, statusCode, message = result.Error.Message },
                    statusCode: statusCode);
            }

            return Results.Ok(new
            {
                result = new
                {
                    lotId = result.Value!.LotId,
                    holdDepositVnd = result.Value.HoldDepositVnd,
                    isConfigured = result.Value.IsConfigured
                },
                isSuccess = true,
                statusCode = StatusCodes.Status200OK,
                message = "Configure deposit policy successfully"
            });
        })
        .RequireAuthorization(ParkingAuthorization.ParkingLotOwnerPolicy)
        .WithName($"ConfigureDepositPolicy_{pattern.Replace('/', '_').Trim('_')}")
        .WithSummary("Cau hinh muc coc giu cho cho bai xe");
    }

    private static void MapGetEndpoints(IEndpointRouteBuilder app, string pattern)
    {
        app.MapGet(pattern, async (
            Guid lotId,
            ICurrentUser currentUser,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            if (!Guid.TryParse(currentUser.SubjectId, out var currentUserId) || currentUserId == Guid.Empty)
            {
                return Results.Unauthorized();
            }

            var query = new GetDepositPolicyQuery(lotId, currentUserId);
            var result = await sender.Send(query, cancellationToken);

            if (!result.IsSuccess)
            {
                var errorCode = result.Error!.Code;
                var statusCode = errorCode.Contains("not_found", StringComparison.OrdinalIgnoreCase)
                    ? StatusCodes.Status404NotFound
                    : errorCode.Contains("owner", StringComparison.OrdinalIgnoreCase) || errorCode.Contains("forbidden", StringComparison.OrdinalIgnoreCase)
                        ? StatusCodes.Status403Forbidden
                        : StatusCodes.Status400BadRequest;

                return Results.Json(
                    new { result = (object?)null, isSuccess = false, statusCode, message = result.Error.Message },
                    statusCode: statusCode);
            }

            return Results.Ok(new
            {
                result = new
                {
                    lotId = result.Value!.LotId,
                    holdDepositVnd = result.Value.HoldDepositVnd,
                    isConfigured = result.Value.IsConfigured
                },
                isSuccess = true,
                statusCode = StatusCodes.Status200OK,
                message = "Get deposit policy successfully"
            });
        })
        .RequireAuthorization(ParkingAuthorization.ParkingLotOwnerPolicy)
        .WithName($"GetDepositPolicy_{pattern.Replace('/', '_').Trim('_')}")
        .WithSummary("Lay muc coc giu cho hien tai cua bai xe");
    }
}
