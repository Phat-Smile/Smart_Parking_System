using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using ParkingSystem.BuildingBlocks.Identity;
using ParkingSystem.Parking.Application.Features.Pricing.Commands.ConfigurePricing;
using ParkingSystem.Parking.Application.Features.Pricing.Queries.GetPricingPolicy;
using ParkingSystem.Parking.WebAPI.DTOs;
using ParkingSystem.Parking.WebAPI.Security;

namespace ParkingSystem.Parking.WebAPI.Endpoints;

public static class PricingEndpoints
{
    public static IEndpointRouteBuilder MapPricingEndpoints(this IEndpointRouteBuilder app)
    {
        // Tra cuu bang gia theo loai xe cho bai xe (D2-BE-3)
        app.MapGet("/api/parking/lots/{lotId:guid}/pricing/{vehicleType}", async (
            Guid lotId,
            string vehicleType,
            ICurrentUser currentUser,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            // Anonymous callers are allowed. A signed-in account is passed on so that an owner can read the price
            // of a lot that is not approved yet.
            Guid? callerId = Guid.TryParse(currentUser.SubjectId, out var subject) ? subject : null;
            var query = new GetPricingPolicyQuery(lotId, vehicleType, callerId);
            var result = await sender.Send(query, cancellationToken);

            if (!result.IsSuccess)
            {
                var errorCode = result.Error!.Code;
                var statusCode = errorCode.Contains("not_found", StringComparison.OrdinalIgnoreCase)
                    ? StatusCodes.Status404NotFound
                    : StatusCodes.Status400BadRequest;

                return Results.Json(
                    new { result = (object?)null, isSuccess = false, statusCode, message = result.Error.Message },
                    statusCode: statusCode);
            }

            return Results.Ok(new
            {
                result = result.Value,
                isSuccess = true,
                statusCode = StatusCodes.Status200OK,
                message = "Get pricing policy successfully"
            });
        })
        .AllowAnonymous()
        .WithName("GetPricingPolicy")
        .WithSummary("Tra cuu bang gia cho bai do xe theo loai xe")
        .Produces(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound);

        // Cau hinh bang gia theo loai xe cho bai xe
        app.MapPut("/api/parking/lots/{lotId:guid}/pricing/{vehicleType}", async (
            Guid lotId,
            string vehicleType,
            ConfigurePricingRequest request,
            ICurrentUser currentUser,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            if (!Guid.TryParse(currentUser.SubjectId, out var currentUserId))
            {
                return Results.Unauthorized();
            }

            var command = request.ToCommand(lotId, vehicleType, currentUserId);
            var result = await sender.Send(command, cancellationToken);

            if (!result.IsSuccess)
            {
                var errorCode = result.Error!.Code;
                var statusCode = errorCode.Contains("not_found", StringComparison.OrdinalIgnoreCase)
                    ? StatusCodes.Status404NotFound
                    : errorCode.Contains("conflict", StringComparison.OrdinalIgnoreCase)
                        ? StatusCodes.Status409Conflict
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
                    version = result.Value!.Version,
                    isNewVersionCreated = result.Value.IsNewVersionCreated
                },
                isSuccess = true,
                statusCode = StatusCodes.Status200OK,
                message = "Configure pricing successfully"
            });
        })
        .RequireAuthorization(ParkingAuthorization.ParkingLotOwnerPolicy)
        .WithName("ConfigurePricing")
        .WithSummary("Cau hinh bang gia cho bai do xe");

        return app;
    }
}
