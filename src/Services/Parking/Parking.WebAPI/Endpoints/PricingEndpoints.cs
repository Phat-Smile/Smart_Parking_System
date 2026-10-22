using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using ParkingSystem.BuildingBlocks.Identity;
using ParkingSystem.Parking.Application.Features.Pricing.Commands.ConfigurePricing;
using ParkingSystem.Parking.WebAPI.DTOs;

namespace ParkingSystem.Parking.WebAPI.Endpoints;

public static class PricingEndpoints
{
    public static IEndpointRouteBuilder MapPricingEndpoints(this IEndpointRouteBuilder app)
    {
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
        .RequireAuthorization()
        .WithName("ConfigurePricing")
        .WithSummary("Cau hinh bang gia cho bai do xe");

        return app;
    }
}
