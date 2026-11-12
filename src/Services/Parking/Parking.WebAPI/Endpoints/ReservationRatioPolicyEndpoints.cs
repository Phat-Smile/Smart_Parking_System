using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using ParkingSystem.BuildingBlocks.Identity;
using ParkingSystem.Parking.Application.Features.ReservationRatioPolicy.Commands.ConfigureReservationRatioPolicy;
using ParkingSystem.Parking.Application.Features.ReservationRatioPolicy.Queries.GetReservationRatioPolicy;
using ParkingSystem.Parking.WebAPI.DTOs;
using ParkingSystem.Parking.WebAPI.Security;

namespace ParkingSystem.Parking.WebAPI.Endpoints;

public static class ReservationRatioPolicyEndpoints
{
    private const string RoutePattern = "/api/parking/lots/{lotId:guid}/reservation-ratio-policy";

    public static IEndpointRouteBuilder MapReservationRatioPolicyEndpoints(this IEndpointRouteBuilder app)
    {
        MapPutEndpoints(app, RoutePattern);
        MapGetEndpoints(app, RoutePattern);

        return app;
    }

    private static void MapPutEndpoints(IEndpointRouteBuilder app, string pattern)
    {
        app.MapPut(pattern, async (
            Guid lotId,
            ConfigureReservationRatioPolicyRequest request,
            ICurrentUser currentUser,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            if (!Guid.TryParse(currentUser.SubjectId, out var currentUserId) || currentUserId == Guid.Empty)
            {
                return Results.Unauthorized();
            }

            var command = new ConfigureReservationRatioPolicyCommand(lotId, request?.ReservationRatioPercent, currentUserId);
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
                    reservationRatioPercent = result.Value.ReservationRatioPercent,
                    isConfigured = result.Value.IsConfigured,
                    updatedAtUtc = result.Value.UpdatedAtUtc
                },
                isSuccess = true,
                statusCode = StatusCodes.Status200OK,
                message = "Configure reservation ratio policy successfully"
            });
        })
        .RequireAuthorization(ParkingAuthorization.ParkingLotOwnerPolicy)
        .WithName("ConfigureReservationRatioPolicy")
        .WithSummary("Cau hinh ty le suat dat truoc cho bai xe");
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

            var query = new GetReservationRatioPolicyQuery(lotId, currentUserId);
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
                    reservationRatioPercent = result.Value.ReservationRatioPercent,
                    isConfigured = result.Value.IsConfigured,
                    updatedAtUtc = result.Value.UpdatedAtUtc
                },
                isSuccess = true,
                statusCode = StatusCodes.Status200OK,
                message = "Get reservation ratio policy successfully"
            });
        })
        .RequireAuthorization(ParkingAuthorization.ParkingLotOwnerPolicy)
        .WithName("GetReservationRatioPolicy")
        .WithSummary("Lay ty le suat dat truoc hien tai cua bai xe");
    }
}
