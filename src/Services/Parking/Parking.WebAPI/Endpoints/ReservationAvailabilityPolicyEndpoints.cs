using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using ParkingSystem.BuildingBlocks.Identity;
using ParkingSystem.Parking.Application.Features.ReservationAvailabilityPolicy.Commands.ConfigureReservationAvailabilityPolicy;
using ParkingSystem.Parking.Application.Features.ReservationAvailabilityPolicy.Queries.GetReservationAvailabilityPolicy;
using ParkingSystem.Parking.WebAPI.DTOs;
using ParkingSystem.Parking.WebAPI.Security;

namespace ParkingSystem.Parking.WebAPI.Endpoints;

public static class ReservationAvailabilityPolicyEndpoints
{
    private const string RoutePattern = "/api/parking/lots/{lotId:guid}/reservation-availability";

    public static IEndpointRouteBuilder MapReservationAvailabilityPolicyEndpoints(this IEndpointRouteBuilder app)
    {
        MapPutEndpoints(app, RoutePattern);
        MapGetEndpoints(app, RoutePattern);

        return app;
    }

    private static void MapPutEndpoints(IEndpointRouteBuilder app, string pattern)
    {
        app.MapPut(pattern, async (
            Guid lotId,
            ConfigureReservationAvailabilityPolicyRequest request,
            ICurrentUser currentUser,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            if (!Guid.TryParse(currentUser.SubjectId, out var currentUserId) || currentUserId == Guid.Empty)
            {
                return Results.Unauthorized();
            }

            var command = new ConfigureReservationAvailabilityPolicyCommand(lotId, request?.ReservationEnabled, currentUserId);
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
                    reservationEnabled = result.Value.ReservationEnabled,
                    isConfigured = result.Value.IsConfigured,
                    updatedAtUtc = result.Value.UpdatedAtUtc
                },
                isSuccess = true,
                statusCode = StatusCodes.Status200OK,
                message = "Configure reservation availability policy successfully"
            });
        })
        .RequireAuthorization(ParkingAuthorization.ParkingLotOwnerPolicy)
        .WithName("ConfigureReservationAvailabilityPolicy")
        .WithSummary("Cau hinh trang thai nhan dat cho cua bai xe");
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

            var query = new GetReservationAvailabilityPolicyQuery(lotId, currentUserId);
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
                    reservationEnabled = result.Value.ReservationEnabled,
                    isConfigured = result.Value.IsConfigured,
                    updatedAtUtc = result.Value.UpdatedAtUtc
                },
                isSuccess = true,
                statusCode = StatusCodes.Status200OK,
                message = "Get reservation availability policy successfully"
            });
        })
        .RequireAuthorization(ParkingAuthorization.ParkingLotOwnerPolicy)
        .WithName("GetReservationAvailabilityPolicy")
        .WithSummary("Lay trang thai nhan dat cho hien tai cua bai xe");
    }
}
